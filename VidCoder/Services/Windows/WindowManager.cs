using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using Microsoft.AnyContainer;
using ReactiveUI;
using VidCoder.Extensions;
using VidCoder.Model;
using VidCoder.Model.WindowPlacer;
using VidCoder.Resources;
using VidCoder.ViewModel;
using VidCoderCommon.Model;

namespace VidCoder.Services.Windows;

public class WindowManager : ReactiveObject, IWindowManager
{
	private static readonly Type MainViewModelType = typeof(MainViewModel);
	private const string WindowTypePrefix = "VidCoder.View.";
	private object mainViewModel;
	private Dictionary<object, Window> openWindows;

	static WindowManager()
	{
		Definitions = new List<WindowDefinition>
		{
			new WindowDefinition
			{
				ViewModelType = typeof(MainViewModel), 
				PlacementConfigKey = "MainWindowPlacement",
				InitialSizeOverride = () =>
				{
					Rect workArea = SystemParameters.WorkArea;

					double fillHeight = workArea.Height - 500;
					double minHeight = 548;

					double desiredHeight = Math.Max(fillHeight, minHeight);

					return new Size(0, desiredHeight);
				}
			},

			new WindowDefinition
			{
				ViewModelType = typeof(EncodingWindowViewModel), 
				InMenu = true,
				PlacementConfigKey = "EncodingDialogPlacement",
				IsOpenConfigKey = "EncodingWindowOpen", 
				InputGestureText = "Ctrl+N",
				MenuLabel = MainRes.EncodingSettingsMenuItem
			},

			new WindowDefinition
			{
				ViewModelType = typeof(PreviewWindowViewModel), 
				InMenu = true,
				PlacementConfigKey = "PreviewWindowPlacement",
				ManualPlacementRestore = true,
				IsOpenConfigKey = "PreviewWindowOpen", 
				InputGestureText = "Ctrl+P",
				MenuLabel = MainRes.PreviewMenuItem
			},

			new WindowDefinition
			{
				ViewModelType = typeof(PickerWindowViewModel), 
				InMenu = true,
				PlacementConfigKey = "PickerWindowPlacement",
				IsOpenConfigKey = "PickerWindowOpen", 
				InputGestureText = "Ctrl+I",
				MenuLabel = MainRes.PickerMenuItem
			},

			new WindowDefinition
			{
				ViewModelType = typeof(LogWindowViewModel), 
				InMenu = true,
				PlacementConfigKey = "LogWindowPlacement",
				IsOpenConfigKey = "LogWindowOpen", 
				InputGestureText = "Ctrl+L",
				MenuLabel = MainRes.LogMenuItem
			},

			new WindowDefinition
			{
				ViewModelType = typeof(EncodeDetailsWindowViewModel), 
				InMenu = true,
				PlacementConfigKey = "EncodeDetailsWindowPlacement",
				IsOpenConfigKey = "EncodeDetailsWindowOpen", 
				MenuLabel = MainRes.EncodeDetailsMenuItem,
				CanOpen = () => StaticResolver.Resolve<ProcessingService>().WhenAnyValue(x => x.Encoding)
			},

			new WindowDefinition
			{
				ViewModelType = typeof(ChapterMarkersDialogViewModel),
				PlacementConfigKey = "ChapterMarkersDialogPlacement"
			},

			new WindowDefinition
			{
				ViewModelType = typeof(QueueTitlesWindowViewModel),
				PlacementConfigKey = "QueueTitlesDialogPlacement2"
			},

			new WindowDefinition
			{
				ViewModelType = typeof(CompareWindowViewModel),
				PlacementConfigKey = "CompareWindowPlacement"
			},

			new WindowDefinition
			{
				ViewModelType = typeof(AddAutoPauseProcessDialogViewModel),
				PlacementConfigKey = "AddAutoPauseProcessDialogPlacement"
			},

			new WindowDefinition
			{
				ViewModelType = typeof(OptionsDialogViewModel),
				PlacementConfigKey = "OptionsDialogPlacement"
			},

			new WindowDefinition
			{
				ViewModelType = typeof(WatcherEditDialogViewModel),
				PlacementConfigKey = "WatcherEditDialogPlacement"
			}
		};

		if (Utilities.InstallType != VidCoderInstallType.Portable)
		{
			Definitions.Add(new WindowDefinition
			{
				ViewModelType = typeof(WatcherWindowViewModel),
				InMenu = true,
				PlacementConfigKey = "WatcherWindowPlacement",
				IsOpenConfigKey = "WatcherWindowOpen",
				InputGestureText = "Ctrl+W",
				MenuLabel = MainRes.WatcherMenuItem
			});
		}
	}

	public WindowManager()
	{
		this.openWindows = new Dictionary<object, Window>();
	}

	public static List<WindowDefinition> Definitions { get; private set; }

	/// <summary>
	/// Fires when a window opens.
	/// </summary>
	public event EventHandler<EventArgs<Type>> WindowOpened;

	/// <summary>
	/// Fires when a window closes.
	/// </summary>
	public event EventHandler<EventArgs<Type>> WindowClosed;

	/// <summary>
	/// Opens the viewmodel as a window.
	/// </summary>
	/// <param name="viewModel">The window's viewmodel.</param>
	/// <param name="ownerViewModel">The viewmodel of the owner window.</param>
	/// <param name="userInitiated">True if the user explicitly opened the window.</param>
	public Window OpenWindow(object viewModel, object ownerViewModel = null, bool userInitiated = true)
	{
		if (viewModel.GetType() == MainViewModelType)
		{
			this.mainViewModel = viewModel;
		}
		else if (ownerViewModel == null)
		{
			ownerViewModel = this.mainViewModel;
		}

		Window windowToOpen = this.PrepareWindowForOpen(viewModel, ownerViewModel, userInitiated, isDialog: false);
		windowToOpen.Show();
		return windowToOpen;
	}

	/// <summary>
	/// Opens the viewmodel as a dialog.
	/// </summary>
	/// <param name="viewModel">The dialog's viewmodel.</param>
	/// <param name="ownerViewModel">The viewmodel of the owner window.</param>
	public void OpenDialog(object viewModel, object ownerViewModel = null)
	{
		if (ownerViewModel == null)
		{
			ownerViewModel = this.mainViewModel;
		}

		Window windowToOpen = this.PrepareWindowForOpen(viewModel, ownerViewModel, userInitiated: true, isDialog: true);
		windowToOpen.ShowDialog();
	}

	/// <summary>
	/// Opens the viewmodel type as a dialog.
	/// </summary>
	/// <typeparam name="T">The type of the viewmodel.</typeparam>
	/// <param name="ownerViewModel">The viewmodel of the owner window.</param>
	public void OpenDialog<T>(object ownerViewModel = null)
		where T : class
	{
		this.OpenDialog(StaticResolver.Resolve<T>(), ownerViewModel);
	}

	/// <summary>
	/// Opens all tracked windows that are open according to config.
	/// </summary>
	/// <remarks>Call at app startup.</remarks>
	public void OpenTrackedWindows()
	{
		bool windowOpened = false;

		foreach (var definition in Definitions.Where(d => d.IsOpenConfigKey != null))
		{
			bool canOpen = true;
			if (definition.CanOpen != null)
			{
				IDisposable disposable = definition.CanOpen().Subscribe(value =>
				{
					canOpen = value;
				});

				disposable.Dispose();
			}

			if (canOpen && Config.Get<bool>(definition.IsOpenConfigKey))
			{
				this.OpenWindow(StaticResolver.Resolve(definition.ViewModelType), userInitiated: false);
				windowOpened = true;
			}
		}

		if (windowOpened)
		{
			this.Focus(this.mainViewModel);
		}
	}

	/// <summary>
	/// Closes all tracked windows.
	/// </summary>
	/// <remarks>Call on app exit.</remarks>
	public void CloseTrackedWindows()
	{
		using (SQLiteTransaction transaction = Database.Connection.BeginTransaction())
		{
			foreach (var definition in Definitions.Where(d => d.IsOpenConfigKey != null))
			{
				object viewModel = this.FindOpenWindowViewModel(definition.ViewModelType);
				if (viewModel != null)
				{
					this.CloseInternal(viewModel, userInitiated: false);
				}
			}

			transaction.Commit();
		}
	}

	/// <summary>
	/// Opens or focuses the viewmodel type's window.
	/// </summary>
	/// <param name="viewModelType">The type of the window viewmodel.</param>
	/// <param name="ownerViewModel">The owner view model (main view model).</param>
	public void OpenOrFocusWindow(Type viewModelType, object ownerViewModel = null)
	{
		object viewModel = this.FindOpenWindowViewModel(viewModelType);

		if (viewModel == null)
		{
			viewModel = StaticResolver.Resolve(viewModelType);
			if (ownerViewModel == null)
			{
				ownerViewModel = this.mainViewModel;
			}

			Window window = this.PrepareWindowForOpen(viewModel, ownerViewModel, userInitiated: true, isDialog: false);
			window.Show();
		}
		else
		{
			this.Focus(viewModel);
		}
	}

	/// <summary>
	/// Opens or focuses the viewmodel type's window.
	/// </summary>
	/// <typeparam name="T">The type of the window viewmodel.</typeparam>
	/// <param name="ownerViewModel">The owner view model (main view model).</param>
	/// <returns>The opened viewmodel.</returns>
	public T OpenOrFocusWindow<T>(object ownerViewModel = null) where T : class
	{
		T viewModel = this.FindOpenWindowViewModel(typeof(T)) as T;

		if (viewModel == null)
		{
			viewModel = StaticResolver.Resolve<T>();
			if (ownerViewModel == null)
			{
				ownerViewModel = this.mainViewModel;
			}

			Window window = this.PrepareWindowForOpen(viewModel, ownerViewModel, userInitiated: true, isDialog: false);
			window.Show();
		}
		else
		{
			this.Focus(viewModel);
		}

		return viewModel;
	}

	/// <summary>
	/// Finds an open window with the given viewmodel type.
	/// </summary>
	/// <typeparam name="T">The viewmodel type.</typeparam>
	/// <returns>The open window viewmodel, or null if is not open.</returns>
	public T Find<T>() where T : class
	{
		return this.FindOpenWindowViewModel(typeof(T)) as T;
	}

	/// <summary>
	/// Gets the view for the given viewmodel.
	/// </summary>
	/// <param name="viewModel">The viewmodel.</param>
	/// <returns>The view for the given viewmodel, or null if the window could not be found.</returns>
	public Window GetView(object viewModel)
	{
		Window window;
		if (this.openWindows.TryGetValue(viewModel, out window))
		{
			return window;
		}

		return null;
	}

	/// <summary>
	/// Creates a command to open a window.
	/// </summary>
	/// <param name="viewModelType">The type of window viewmodel to open.</param>
	/// <param name="openAsDialog">True to open as a dialog, false to open as a window.</param>
	/// <returns>The command.</returns>
	public ICommand CreateOpenCommand(Type viewModelType, bool openAsDialog = false)
	{
		var command = ReactiveCommand.Create(() =>
		{
			if (openAsDialog)
			{
				this.OpenDialog(StaticResolver.Resolve(viewModelType));
			}
			else
			{
				this.OpenOrFocusWindow(viewModelType);
			}
		});

		return command;
	}

	/// <summary>
	/// Focuses the window.
	/// </summary>
	/// <param name="viewModel">The viewmodel of the window to focus.</param>
	public void Focus(object viewModel)
	{
		this.openWindows[viewModel].Focus();
	}

	/// <summary>
	/// Activates the window.
	/// </summary>
	/// <param name="viewModel">The viewmodel of the window to activate.</param>
	public void Activate(object viewModel)
	{
		this.openWindows[viewModel].Activate();
	}

	/// <summary>
	/// Closes the window.
	/// </summary>
	/// <param name="viewModel">The viewmodel of the window to close.</param>
	public void Close(object viewModel)
	{
		this.CloseInternal(viewModel, userInitiated: true);
	}

	/// <summary>
	/// Closes the window of the given type.
	/// </summary>
	/// <typeparam name="T">The viewmodel type of the window to close.</typeparam>
	/// <param name="userInitiated">True if the user specifically asked this window to close.</param>
	public void Close<T>(bool userInitiated) where T : class
	{
		object viewModel = this.FindOpenWindowViewModel(typeof (T));
		if (viewModel != null)
		{
			this.CloseInternal(viewModel, userInitiated);
		}
	}

	/// <summary>
	/// Gets a list of window positions.
	/// </summary>
	/// <param name="excludeWindow">The window to exclude.</param>
	/// <returns>A list of open window positions.</returns>
	public List<WindowPosition> GetOpenedWindowPositions(Window excludeWindow = null)
	{
		var result = new List<WindowPosition>();
		foreach (var definition in Definitions.Where(d => d.PlacementConfigKey != null))
		{
			object windowVM = this.FindOpenWindowViewModel(definition.ViewModelType);
			if (windowVM != null)
			{
				Window window = this.GetView(windowVM);
				if (window != null && window != excludeWindow)
				{
					result.Add(new WindowPosition
					{
						Position = new Rect(
							(int)window.Left,
							(int)window.Top,
							(int)window.ActualWidth,
							(int)window.ActualHeight),
						ViewModelType = definition.ViewModelType
					});
				}
			}
		}

		return result;
	}

	/// <summary>
	/// Suspends the AllowDrop property on all windows (used when a smaller drag/drop operation is starting).
	/// </summary>
	public void SuspendDropOnWindows()
	{
		foreach (Window window in this.openWindows.Values)
		{
			window.AllowDrop = false;
		}
	}

	/// <summary>
	/// Resumes the AllowDrop property on all windows (used when a smaller drag/drop operation is finished).
	/// </summary>
	public void ResumeDropOnWindows()
	{
		foreach (Window window in this.openWindows.Values)
		{
			window.AllowDrop = true;
		}
	}

	/// <summary>
	/// Gets or sets the last time the encoding window was resized.
	/// </summary>
	public DateTimeOffset LastEncodingWindowReize { get; set; } = DateTimeOffset.MinValue;

	/// <summary>
	/// Prepares a window for opening.
	/// </summary>
	/// <param name="viewModel">The window viewmodel to use.</param>
	/// <param name="ownerViewModel">The owner viewmodel.</param>
	/// <param name="userInitiated">True if the user specifically asked this window to open,
	/// false if it being re-opened automatically on app start.</param>
	/// <param name="isDialog">True if the window is being opened as a dialog, false if it's being opened
	/// as a window.</param>
	/// <returns>The prepared window.</returns>
	private Window PrepareWindowForOpen(object viewModel, object ownerViewModel, bool userInitiated, bool isDialog)
	{
		Window windowToOpen = CreateWindow(viewModel.GetType());
		if (ownerViewModel != null)
		{
			windowToOpen.Owner = this.openWindows[ownerViewModel];
		}

		if (LanguageUtilities.ShouldBeRightToLeft)
		{
			windowToOpen.FlowDirection = FlowDirection.RightToLeft;
		}

		windowToOpen.DataContext = viewModel;
		windowToOpen.Closing += this.OnClosingHandler;

		WindowDefinition windowDefinition = GetWindowDefinition(viewModel);
		windowToOpen.SourceInitialized += (o, e) =>
		{
			// Add hook for WndProc messages
			((HwndSource)PresentationSource.FromVisual(windowToOpen)).AddHook(WindowPlacement.HookProc);

			// Restore placement
			if (windowDefinition != null && !windowDefinition.ManualPlacementRestore && windowDefinition.PlacementConfigKey != null)
			{
				string placementJson = Config.Get<string>(windowDefinition.PlacementConfigKey);
				if (isDialog)
				{
					windowToOpen.SetPlacementJson(placementJson);
				}
				else
				{
					if (windowDefinition.InitialSizeOverride != null && string.IsNullOrEmpty(placementJson))
					{
						Size? initialSizeOverride = windowDefinition.InitialSizeOverride();
						if (initialSizeOverride.Value.Width > 0)
						{
							windowToOpen.Width = initialSizeOverride.Value.Width;
						}

						if (initialSizeOverride.Value.Height > 0)
						{
							windowToOpen.Height = initialSizeOverride.Value.Height;
						}
					}

					windowToOpen.PlaceDynamic(placementJson);
				}
			}
		};

		this.openWindows.Add(viewModel, windowToOpen);

		if (userInitiated)
		{
			if (windowDefinition?.IsOpenConfigKey != null)
			{
				Config.Set(windowDefinition.IsOpenConfigKey, true);
			}
		}

		this.WindowOpened?.Invoke(this, new EventArgs<Type>(viewModel.GetType()));

		if (!isDialog)
		{
			windowToOpen.RegisterGlobalHotkeys();
			windowToOpen.AllowDrop = true;
			windowToOpen.PreviewDragOver += OnPreviewDragOver;
			windowToOpen.PreviewDrop += OnPreviewDrop;
		}

		return windowToOpen;
	}

	private static void OnPreviewDragOver(object sender, DragEventArgs dragEventArgs)
	{
		if (StaticResolver.Resolve<MainViewModel>().VideoSourceState == VideoSourceState.Scanning)
		{
			var data = dragEventArgs.Data as DataObject;
			if (data != null)
			{
				dragEventArgs.Effects = DragDropEffects.None;
				dragEventArgs.Handled = true;
			}

			return;
		}

		Utilities.SetDragIcon(dragEventArgs);
	}

	private static void OnPreviewDrop(object sender, DragEventArgs dragEventArgs)
	{
		if (StaticResolver.Resolve<MainViewModel>().VideoSourceState == VideoSourceState.Scanning)
		{
			return;
		}

		var data = dragEventArgs.Data as DataObject;
		if (data != null && data.ContainsFileDropList())
		{
			StringCollection itemList = data.GetFileDropList();
			var listView = dragEventArgs.Source as ListView;
			bool alwaysQueue = listView?.Name == "queueView";

			StaticResolver.Resolve<MainViewModel>().HandlePaths(itemList.Cast<string>().ToList(), alwaysQueue);
		}
	}

	/// <summary>
	/// Closes the window.
	/// </summary>
	/// <param name="viewModel">The viewmodel of the window to close.</param>
	/// <param name="userInitiated">True if the user specifically asked this window to close.</param>
	private void CloseInternal(object viewModel, bool userInitiated)
	{
		if (!this.openWindows.ContainsKey(viewModel))
		{
			return;
		}

		Window window = this.openWindows[viewModel];

		if (!userInitiated)
		{
			window.Closing -= this.OnClosingHandler;
			if (!this.OnClosing(window, userInitiated: false))
			{
				return;
			}
		}

		window.Close();
	}

	/// <summary>
	/// Fires when a window is closing.
	/// </summary>
	/// <param name="sender">The sending window.</param>
	/// <param name="e">The cancellation event args.</param>
	/// <remarks>This should only fire when the user has specifically asked for the window to
	/// close.</remarks>
	private void OnClosingHandler(object sender, CancelEventArgs e)
	{
		var closingWindow = (Window)sender;
		if (!this.OnClosing(closingWindow, userInitiated: true))
		{
			e.Cancel = true;
		}
	}

	/// <summary>
	/// Fires when a window is closing.
	/// </summary>
	/// <param name="window">The window.</param>
	/// <param name="userInitiated">True if the close was initated by the user, false if this
	/// was initiated by the system as part of app shutdown.</param>
	/// <returns>True if the window closed, false if it was stopped by the user.</returns>
	private bool OnClosing(Window window, bool userInitiated)
	{
		object viewModel = window.DataContext;
		var closableWindow = viewModel as IClosableWindow;
		if (closableWindow != null)
		{
			if (!closableWindow.OnClosing())
			{
				return false;
			}
		}

		WindowDefinition windowDefinition = GetWindowDefinition(viewModel);
		if (windowDefinition != null)
		{
			if (windowDefinition.PlacementConfigKey != null)
			{
				Config.Set(windowDefinition.PlacementConfigKey, window.GetPlacementJson());
			}

			if (userInitiated && windowDefinition.IsOpenConfigKey != null)
			{
				Config.Set(windowDefinition.IsOpenConfigKey, false);
			}
		}

		this.openWindows.Remove(viewModel);

		if (userInitiated)
		{
			window.Owner?.Activate();
		}

		this.WindowClosed?.Invoke(this, new EventArgs<Type>(viewModel.GetType()));

		return true;
	}

	/// <summary>
	/// Finds the viewmodel for an open window.
	/// </summary>
	/// <param name="viewModelType">The viewmodel's type.</param>
	/// <returns>The viewmodel, or null if it was not open.</returns>
	private object FindOpenWindowViewModel(Type viewModelType)
	{
		return this.openWindows.Keys.FirstOrDefault(k => k.GetType() == viewModelType);
	}

	/// <summary>
	/// Gets a tracked window definition from the given viewmodel.
	/// </summary>
	/// <param name="viewModel">The window viewmodel.</param>
	/// <returns>The tracked window definition for the viewmodel.</returns>
	private static WindowDefinition GetWindowDefinition(object viewModel)
	{
		Type viewModelType = viewModel.GetType();
		WindowDefinition definition = Definitions.FirstOrDefault(d => d.ViewModelType == viewModelType);
		if (definition != null)
		{
			return definition;
		}

		return null;
	}

	/// <summary>
	/// Creates a Window for the given viewmodel type.
	/// </summary>
	/// <param name="viewModelType">The type of viewmodel.</param>
	/// <returns>The created window.</returns>
	private static Window CreateWindow(Type viewModelType)
	{
		string typeName = viewModelType.Name;
		int backTickIndex = typeName.IndexOf('`');
		if (backTickIndex > 0)
		{
			typeName = typeName.Substring(0, backTickIndex);
		}

		string baseName;
		string suffix;

		if (typeName.EndsWith("DialogViewModel", StringComparison.Ordinal))
		{
			baseName = typeName.Substring(0, typeName.Length - "DialogViewModel".Length);
			suffix = "Dialog";
		}
		else if (typeName.EndsWith("WindowViewModel", StringComparison.Ordinal))
		{
			baseName = typeName.Substring(0, typeName.Length - "WindowViewModel".Length);
			suffix = "Window";
		}
		else if (typeName.EndsWith("ViewModel", StringComparison.Ordinal))
		{
			baseName = typeName.Substring(0, typeName.Length - "ViewModel".Length);
			suffix = string.Empty;
		}
		else
		{
			throw new ArgumentException("Window viewmodel type's name must end in 'ViewModel'");
		}

		Type windowType = Type.GetType(WindowTypePrefix + baseName + suffix);
		if (windowType == null)
		{
			windowType = Type.GetType(WindowTypePrefix + baseName);
		}

		if (windowType == null)
		{
			throw new ArgumentException("Could not find Window for " + typeName);
		}

		return (Window)Activator.CreateInstance(windowType);
	}
}
