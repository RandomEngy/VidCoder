using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Data.SQLite;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Reactive;
using System.Reactive.Linq;
using System.Resources;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Resources;
using System.Windows.Shell;
using DynamicData;
using Fluent;
using HandBrake.Interop.Interop.Json.Encode;
using HandBrake.Interop.Interop.Json.Scan;
using Microsoft.AnyContainer;
using ReactiveUI;
using Squirrel;
using VidCoder.Controls;
using VidCoder.Extensions;
using VidCoder.Model;
using VidCoder.Resources;
using VidCoder.Services;
using VidCoder.Services.Notifications;
using VidCoder.Services.Windows;
using VidCoder.ViewModel;
using VidCoderCommon;
using VidCoderCommon.Model;
using VidCoderCommon.Utilities;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using ListViewItem = System.Windows.Controls.ListViewItem;
using MenuItem = System.Windows.Controls.MenuItem;
using MessageBox = System.Windows.MessageBox;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Path = System.IO.Path;
using Point = System.Windows.Point;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace VidCoder.View;

public partial class Main : Window, IMainView
{
	private const string DiscMenuItemTag = "disc";

	private readonly NotifyIcon notifyIcon;

	private MainViewModel viewModel;
	private ProcessingService processingService = StaticResolver.Resolve<ProcessingService>();
	private OutputPathService outputVM = StaticResolver.Resolve<OutputPathService>();
	private StatusService statusService = StaticResolver.Resolve<StatusService>();
	private IToastNotificationService toastNotificationService = StaticResolver.Resolve<IToastNotificationService>();

	private bool tabsVisible = false;

	private readonly List<IDisposable> discOpenCommands = new List<IDisposable>();

	public static System.Windows.Threading.Dispatcher TheDispatcher;

	public event EventHandler<RangeFocusEventArgs> RangeControlGotFocus;

	public Main()
	{
		Ioc.Container.RegisterSingleton<Main>(() => this);
		this.InitializeComponent();

		if (Environment.OSVersion.Version.Major < 10 && Config.Win7WarningDisplayedTimes < 2)
		{
			DispatchUtilities.BeginInvoke(async () =>
			{
				await Task.Delay(TimeSpan.FromSeconds(1));
				StaticResolver.Resolve<IMessageBoxService>().Show(MainRes.Win7DeprecationWarning);
			});
			Config.Win7WarningDisplayedTimes++;
		}

		try
		{
			var taskbarItemInfo = new TaskbarItemInfo();
			BindingOperations.SetBinding(taskbarItemInfo, TaskbarItemInfo.ProgressStateProperty, new System.Windows.Data.Binding("TaskBarProgressTracker.ProgressState"));
			BindingOperations.SetBinding(taskbarItemInfo, TaskbarItemInfo.ProgressValueProperty, new System.Windows.Data.Binding("TaskBarProgressTracker.ProgressFraction"));

			this.TaskbarItemInfo = taskbarItemInfo;
		}
		catch (Exception exception)
		{
			StaticResolver.Resolve<IAppLogger>().Log("Could not set TaskbarItemInfo: " + exception);
		}

		if (LanguageUtilities.ShouldBeRightToLeft)
		{
			this.FlowDirection = System.Windows.FlowDirection.RightToLeft;
		}

		this.sourceRow.Height = new GridLength(Config.SourcePaneHeightStar, GridUnitType.Star);
		this.queueRow.Height = new GridLength(Config.QueuePaneHeightStar, GridUnitType.Star);

		this.Activated += (sender, args) =>
		{
			DispatchUtilities.BeginInvoke(async () =>
			{
				// Need to yield here for some reason, otherwise the activation is blocked.
				await Task.Yield();
				this.toastNotificationService.Clear();
			});
		};

		this.notifyIcon = new NotifyIcon
		{
			Visible = false
		};
		this.notifyIcon.Click += (sender, args) => { this.EnsureVisible(); };
		this.notifyIcon.DoubleClick += (sender, args) => { this.EnsureVisible(); };

		StreamResourceInfo streamResourceInfo = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/VidCoder_icon.ico"));
		if (streamResourceInfo != null)
		{
			Stream iconStream = streamResourceInfo.Stream;
			this.notifyIcon.Icon = new Icon(iconStream);
		}

		this.RefreshQueueColumns();
		this.LoadCompletedColumnWidths();

#if DEBUG
		var debugDropDown = new DropDownButton { Header = "Debug" };

		var loadScanFromJsonItem = new Fluent.MenuItem {Header = "Load scan from JSON..."};
		loadScanFromJsonItem.Click += (sender, args) =>
		{
			DebugJsonDialog dialog = new DebugJsonDialog("Debug Scan JSON");
			dialog.ShowDialog();
			if (!string.IsNullOrWhiteSpace(dialog.Json))
			{
				try
				{
					var scanObject = JsonSerializer.Deserialize<JsonScanObject>(dialog.Json, JsonOptions.Plain);
					this.viewModel.UpdateFromNewVideoSource(new VideoSource { Titles = scanObject.TitleList, FeatureTitle = scanObject.MainFeature });
				}
				catch (Exception exception)
				{
					MessageBox.Show(this, "Could not parse scan JSON:" + Environment.NewLine + Environment.NewLine + exception.ToString());
				}
			}
		};
		debugDropDown.Items.Add(loadScanFromJsonItem);

		var queueFromJsonItem = new Fluent.MenuItem {Header = "Queue job from JSON..."};
		queueFromJsonItem.Click += (sender, args) =>
		{
			if (!this.viewModel.HasVideoSource)
			{
				StaticResolver.Resolve<IMessageBoxService>().Show("Must open source before adding queue job from JSON");
				return;
			}

			EncodeJobViewModel jobViewModel = this.viewModel.CreateEncodeJobVM();
			DebugJsonDialog dialog = new DebugJsonDialog("Debug Encode JSON");
			dialog.ShowDialog();

			if (!string.IsNullOrWhiteSpace(dialog.Json))
			{
				try
				{
					// Patch the current source and destination file onto the job
					JsonEncodeObject encodeObject = JsonSerializer.Deserialize<JsonEncodeObject>(dialog.Json, JsonOptions.Plain);
					encodeObject.Destination.File = jobViewModel.Job.FinalOutputPath;
					encodeObject.Source.Path = jobViewModel.Job.SourcePath;

					jobViewModel.DebugEncodeJsonOverride = JsonSerializer.Serialize(encodeObject, new JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull });
					//jobViewModel.Job.FinalOutputPath = encodeObject.Destination.File;
					//jobViewModel.Job.SourcePath = encodeObject.Source.Path;

					this.processingService.QueueJob(jobViewModel);
				}
				catch (Exception exception)
				{
					MessageBox.Show(this, "Could not parse encode JSON:" + Environment.NewLine + Environment.NewLine + exception.ToString());
				}
			}
		};
		debugDropDown.Items.Add(queueFromJsonItem);

		var throwExceptionItem = new Fluent.MenuItem { Header = "Throw exception" };
		throwExceptionItem.Click += (sender, args) =>
		{
			throw new InvalidOperationException("Rats.");
		};
		debugDropDown.Items.Add(throwExceptionItem);

		var addLogItem = new Fluent.MenuItem { Header = "Add 1 log item" };
		addLogItem.Click += (sender, args) =>
		{
			StaticResolver.Resolve<IAppLogger>().Log("This is a log item");
		};
		debugDropDown.Items.Add(addLogItem);

		var addTenLogItems = new Fluent.MenuItem { Header = "Add 10 log items" };
		addTenLogItems.Click += (sender, args) =>
		{
			for (int i = 0; i < 10; i++)
			{
				StaticResolver.Resolve<IAppLogger>().Log("This is a log item");
			}
		};
		debugDropDown.Items.Add(addTenLogItems);

		var addLongLogItem = new Fluent.MenuItem { Header = "Add long log item" };
		addLongLogItem.Click += (sender, args) =>
		{
			StaticResolver.Resolve<IAppLogger>().Log("This is a log item\r\nthat is split into multiple lines\r\nOh yes indeed");
		};
		debugDropDown.Items.Add(addLongLogItem);

		var doAnActionItem = new Fluent.MenuItem { Header = "Perform action" };
		doAnActionItem.Click += (sender, args) =>
		{
			// Nothing for now
		};
		debugDropDown.Items.Add(doAnActionItem);

		var doAnActionItem2 = new Fluent.MenuItem { Header = "Perform action 2" };
		doAnActionItem2.Click += (sender, args) =>
		{
			// Nothing for now
		};
		debugDropDown.Items.Add(doAnActionItem2);


		var showDelayedStatusItem = new Fluent.MenuItem { Header = "Show a status message in 40 seconds" };
		showDelayedStatusItem.Click += async (sender, args) =>
		{
			await Task.Delay(TimeSpan.FromSeconds(40));
			StaticResolver.Resolve<StatusService>().Show("Some delayed message");
		};
		debugDropDown.Items.Add(showDelayedStatusItem);

		this.toolsRibbonGroupBox.Items.Add(debugDropDown);
#endif

		this.DataContextChanged += this.OnDataContextChanged;
		TheDispatcher = this.Dispatcher;

		this.statusText.Opacity = 0.0;

		NameScope.SetNameScope(this, new NameScope());
		this.RegisterName("StatusText", this.statusText);

		var storyboard = (Storyboard)this.FindResource("StatusTextStoryboard");
		storyboard.Completed += (sender, args) =>
			{
				this.statusText.Visibility = Visibility.Collapsed;
			};

		this.presetTreeViewContainer.PresetTreeView.OnHierarchyMouseUp += (sender, args) =>
		{
			this.presetButton.IsDropDownOpen = false;
		};

		this.presetButton.DropDownOpened += (sender, args) =>
		{
			var item = UIUtilities.FindDescendant<TreeViewItem>(this.presetTreeViewContainer.PresetTreeView, viewItem =>
			{
				return viewItem.Header == this.viewModel.PresetsService.SelectedPreset;
			});

			if (item != null)
			{
				UIUtilities.BringIntoView(item);
			}
		};

		this.Loaded += (e, o) =>
		{
			this.RestoredWindowState = this.WindowState;
		};

		this.statusService.MessageShown += (o, e) =>
		{
			this.ShowStatusMessage(e.Value);
		};

		this.queueView.SelectionChanged += this.QueueView_SelectionChanged;

		this.RefreshMaximizeRestoreButton();
	}

	private void QueueView_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		this.processingService.OnSelectedQueueItemsChanged();
	}

	public void RestoreWindow()
	{
		DispatchUtilities.BeginInvoke(() =>
		{
			this.Show();
			this.WindowState = this.RestoredWindowState;
		});
	}

	public void EnsureVisible()
	{
		DispatchUtilities.Invoke(() =>
		{
			this.RestoreWindow();
			DispatchUtilities.BeginInvoke(() =>
			{
				this.Activate();
			});
		});
	}

	public WindowState RestoredWindowState { get; set; }

	public void ReadTextToScreenReader(string text)
	{
		var peer = UIElementAutomationPeer.FromElement(this.screenReaderText);
		if (peer != null)
		{
			this.screenReaderText.Text = text;
			peer.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
		}
	}

	public void ShowBalloonMessage(string title, string message)
	{
		if (this.notifyIcon.Visible)
		{
			this.notifyIcon.ShowBalloonTip(5000, title, message, ToolTipIcon.Info);
		}
	}

	private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
	{
		this.viewModel = (MainViewModel)this.DataContext;
		this.viewModel.View = this;
		this.viewModel.WhenAnyValue(x => x.SubtitlesExpanded).Subscribe(expanded =>
		{
			if (expanded)
			{
				ResizeGridViewColumn(this.sourceSelectedColumn);
				ResizeGridViewColumn(this.sourceTrackSummaryColumn);
				ResizeGridViewColumn(this.sourceDefaultColumn);
				ResizeGridViewColumn(this.sourceForcedColumn);
				ResizeGridViewColumn(this.sourceBurnedColumn);
				ResizeGridViewColumn(this.sourceNameColumn);
				ResizeGridViewColumn(this.sourceRemoveDuplicateColumn);
				ResizeGridViewColumn(this.fileSubtitleFileColumn);
				ResizeGridViewColumn(this.fileSubtitleNameColumn);
				ResizeGridViewColumn(this.fileSubtitleDefaultColumn);
				ResizeGridViewColumn(this.fileSubtitleBurnedInColumn);
				ResizeGridViewColumn(this.fileSubtitleCharCodeColumn);
				ResizeGridViewColumn(this.fileSubtitleLanguageColumn);
			}
		});

		this.viewModel.WhenAnyValue(x => x.AudioExpanded).Subscribe(expanded =>
		{
			if (expanded)
			{
				this.ResizeAudioColumns();
			}
		});

		this.RefreshDiscMenuItems();

		this.notifyIcon.Text = this.viewModel.TrayIconToolTip;
		this.viewModel.WhenAnyValue(x => x.ShowTrayIcon).Subscribe(showTrayIcon =>
		{
			this.notifyIcon.Visible = showTrayIcon;
		});

		this.processingService.PropertyChanged += (sender2, e2) =>
		    {
				if (e2.PropertyName == nameof(this.processingService.CompletedItemsCount))
				{
					this.RefreshQueueTabs();
				}
		    };

		this.RefreshQueueTabs();

		this.sourceSubtitles = this.viewModel.SourceSubtitles;
		this.fileSubtitles = this.viewModel.FileSubtitles;

		var sourceSubtitlesObservable = this.sourceSubtitles.Connect();
		sourceSubtitlesObservable
			.WhenValueChanged(subtitle => subtitle.Selected)
			.Subscribe(_ =>
		{
			this.ResizeSourceSubtitleColumns();
		});

		var fileSubtitlesObservable = this.fileSubtitles.Connect();
		fileSubtitlesObservable
			.WhenValueChanged(subtitle => subtitle.CharacterCode)
			.Subscribe(_ =>
			{
				ResizeGridViewColumn(this.fileSubtitleCharCodeColumn);
			});

		fileSubtitlesObservable
			.WhenValueChanged(subtitle => subtitle.LanguageCode)
			.Subscribe(_ =>
			{
				ResizeGridViewColumn(this.fileSubtitleLanguageColumn);
			});

		sourceSubtitlesObservable.Subscribe(changeSet =>
		{
			ResizeGridViewColumn(this.sourceTrackSummaryColumn);
			ResizeGridViewColumn(this.sourceNameColumn);
		});

		fileSubtitlesObservable.Subscribe(changeSet =>
		{
			ResizeGridViewColumn(this.fileSubtitleCharCodeColumn);
			ResizeGridViewColumn(this.fileSubtitleLanguageColumn);
		});

		this.viewModel.OutputSizeService
			.WhenAnyValue(x => x.Size)
			.Subscribe(size =>
			{
				if (size != null)
				{
					// Update this.miniPreviewFrame width
					double aspectRatio = size.OutputAspectRatio;
					aspectRatio = Math.Min(aspectRatio, 3.0);

					double desiredWidth = this.miniPreviewFrame.ActualHeight * aspectRatio;
					this.miniPreviewFrame.Width = Math.Max(0, desiredWidth);
				}
			});

	}

	public void ResizeAudioColumns()
	{
		ResizeGridViewColumn(this.audioSelectedColumn);
		ResizeGridViewColumn(this.audioTrackSummaryColumn);
		ResizeGridViewColumn(this.audioBitrateColumn);
		ResizeGridViewColumn(this.audioSampleRateColumn);
		ResizeGridViewColumn(this.audioNameColumn);
		ResizeGridViewColumn(this.audioRemoveDuplicateColumn);
	}

	public double SourceAreaHeight => this.sourceScrollViewer.ActualHeight;

	void IMainView.SaveQueueColumns()
	{
		this.SaveQueueColumns();
	}

	private void SaveQueueColumns()
	{
		var queueColumnsBuilder = new StringBuilder();
		List<Tuple<string, double>> columns = Utilities.ParseQueueColumnList(Config.QueueColumns);
		for (int i = 0; i < columns.Count; i++)
		{
			queueColumnsBuilder.Append(columns[i].Item1);
			queueColumnsBuilder.Append(":");
			queueColumnsBuilder.Append(this.queueGridView.Columns[i].ActualWidth);

			if (i != columns.Count - 1)
			{
				queueColumnsBuilder.Append("|");
			}
		}

		Config.QueueColumns = queueColumnsBuilder.ToString();
	}

	void IMainView.ApplyQueueColumns()
	{
		this.RefreshQueueColumns();
	}

	public IList<EncodeJobViewModel> SelectedJobs => this.queueView.SelectedItems.Cast<EncodeJobViewModel>().ToList();

	private void RefreshQueueColumns()
	{
		this.queueGridView.Columns.Clear();
		var resources = new ResourceManager("VidCoder.Resources.CommonRes", typeof(CommonRes).Assembly);

		List<Tuple<string, double>> columns = Utilities.ParseQueueColumnList(Config.QueueColumns);
		foreach (Tuple<string, double> column in columns)
		{
			var queueColumn = new GridViewColumn
			{
				Header = resources.GetString("QueueColumnName" + column.Item1),
				CellTemplate = this.Resources["QueueTemplate" + column.Item1] as DataTemplate,
				Width = column.Item2
			};

			this.queueGridView.Columns.Add(queueColumn);
		}

		var lastColumn = new GridViewColumn
		{
			CellTemplate = this.Resources["QueueRemoveTemplate"] as DataTemplate,
			Width = Config.QueueLastColumnWidth
		};
		this.queueGridView.Columns.Add(lastColumn);
	}

	private void LoadCompletedColumnWidths()
	{
		string columnWidthsString = Config.CompletedColumnWidths;

		if (string.IsNullOrEmpty(columnWidthsString))
		{
			return;
		}

		string[] columnWidths = columnWidthsString.Split('|');
		for (int i = 0; i < this.completedGridView.Columns.Count; i++)
		{
			if (i < columnWidths.Length)
			{
				double width = 0;
				double.TryParse(columnWidths[i], out width);

				if (width > 0)
				{
					this.completedGridView.Columns[i].Width = width;
				}
			}
		}
	}

	void IMainView.SaveCompletedColumnWidths()
	{
		var completedColumnsBuilder = new StringBuilder();
		for (int i = 0; i < this.completedGridView.Columns.Count; i++)
		{
			completedColumnsBuilder.Append(this.completedGridView.Columns[i].ActualWidth);

			if (i != this.completedGridView.Columns.Count - 1)
			{
				completedColumnsBuilder.Append("|");
			}
		}

		Config.CompletedColumnWidths = completedColumnsBuilder.ToString();
	}

	private void RefreshQueueTabs()
	{
		if (this.processingService.CompletedItemsCount > 0 && !this.tabsVisible)
		{
			this.queueTab.Visibility = Visibility.Visible;
			this.completedTab.Visibility = Visibility.Visible;
			this.clearCompletedQueueItemsButton.Visibility = Visibility.Visible;
			this.queueItemsTabControl.BorderThickness = new Thickness(1);

			this.tabsVisible = true;
			return;
		}

		if (this.processingService.CompletedItemsCount == 0 && this.tabsVisible)
		{
			this.queueTab.Visibility = Visibility.Collapsed;
			this.completedTab.Visibility = Visibility.Collapsed;
			this.clearCompletedQueueItemsButton.Visibility = Visibility.Collapsed;
			this.queueItemsTabControl.BorderThickness = new Thickness(0);

			this.processingService.SelectedTabIndex = ProcessingService.QueuedTabIndex;

			this.tabsVisible = false;
			return;
		}
	}

	public void RefreshDiscMenuItems()
	{
		// Clear previous discs
		for (int i = this.openSourceButton.Items.Count - 1; i >= 0; i--)
		{
			var menuItem = this.openSourceButton.Items[i] as FrameworkElement;
			if (menuItem != null && menuItem.Tag != null && (string)menuItem.Tag == DiscMenuItemTag)
			{
				this.openSourceButton.Items.RemoveAt(i);
			}
		}

		int insertionIndex = 2;

		foreach (IDisposable oldCommand in this.discOpenCommands)
		{
			oldCommand.Dispose();
		}

		this.discOpenCommands.Clear();

		// Add new discs
		foreach (DriveInformation driveInfo in this.viewModel.DriveCollection)
		{
			var menuItem = new Fluent.MenuItem
			{
				Header = string.Format(MainRes.OpenFormat, driveInfo.DisplayText),
				Tag = DiscMenuItemTag,
				RecognizesAccessKey = false // We set this to stop Fluent Ribbon from removing underscores from the header text
			};

			ReactiveCommand<Unit, Unit> command = ReactiveCommand.Create(
				() =>
				{
					this.viewModel.SetSourceFromDvd(driveInfo);
				},
				this.viewModel.NotScanningObservable);
			this.discOpenCommands.Add(command);
			menuItem.Command = command;

			if (driveInfo.DiscType == DiscType.Dvd)
			{
				menuItem.Icon = "/Icons/disc.png";
			}
			else
			{
				menuItem.Icon = "/Icons/bludisc.png";
			}

			this.openSourceButton.Items.Insert(insertionIndex, menuItem);
			insertionIndex++;
		}
	}

	protected void HandleCompletedItemDoubleClick(object sender, MouseButtonEventArgs e)
	{
		var encodeResultVM = ((ListViewItem)sender).Content as EncodeResultViewModel;
		if (encodeResultVM.EncodeResult.Succeeded)
		{
			string resultFile = encodeResultVM.EncodeResult.Destination;

			if (File.Exists(resultFile))
			{
				this.ShowStatusMessage(MainRes.PlayingVideoMessage);
				FileService.Instance.PlayVideo(encodeResultVM.EncodeResult.Destination);
			}
			else
			{
				MessageBox.Show(string.Format(MainRes.FileDoesNotExist, resultFile));
			}
		}
	}

	private void ProgressMouseEnter(object sender, MouseEventArgs e)
	{
		this.encodeProgressDetailsPopup.IsOpen = true;
	}

	private void ProgressMouseLeave(object sender, MouseEventArgs e)
	{
		this.encodeProgressDetailsPopup.IsOpen = false;
	}

	private void StartTimeGotFocus(object sender, RoutedEventArgs e)
	{
		this.RangeControlGotFocus?.Invoke(this, new RangeFocusEventArgs { GotFocus = true, RangeType = VideoRangeType.Seconds, Start = true });
	}

	private void EndTimeGotFocus(object sender, RoutedEventArgs e)
	{
		this.RangeControlGotFocus?.Invoke(this, new RangeFocusEventArgs { GotFocus = true, RangeType = VideoRangeType.Seconds, Start = false });
	}

	private void FramesStartGotFocus(object sender, RoutedEventArgs e)
	{
		this.RangeControlGotFocus?.Invoke(this, new RangeFocusEventArgs { GotFocus = true, RangeType = VideoRangeType.Frames, Start = true });
	}

	private void FramesEndGotFocus(object sender, RoutedEventArgs e)
	{
		this.RangeControlGotFocus?.Invoke(this, new RangeFocusEventArgs { GotFocus = true, RangeType = VideoRangeType.Frames, Start = false });
	}

	private void DestinationReadCoverMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		this.destinationEditBox.Focus();
	}

	private void DestinationEditBoxGotFocus(object sender, RoutedEventArgs e)
	{
		this.outputVM.EditingDestination = true;

		string path = this.outputVM.OutputPath;
		string fileName = Path.GetFileName(path);

		if (fileName != null)
		{
			if (fileName == string.Empty)
			{
				this.destinationEditBox.Select(path.Length, 0);
			}
			else
			{
				int selectStart = path.Length - fileName.Length;

				string extension = Path.GetExtension(path);
				if (extension == string.Empty)
				{
					this.destinationEditBox.Select(selectStart, path.Length - selectStart);
				}
				else
				{
					this.destinationEditBox.Select(selectStart, path.Length - selectStart - extension.Length);
				}
			}
		}

		this.outputVM.OldOutputPath = path;
	}

	private void DestinationEditBoxLostFocus(object sender, RoutedEventArgs e)
	{
		this.StopEditing();
	}

	private void DestinationEditBoxPreviewKeyDown(object sender, KeyEventArgs e)
	{
		if (e.Key == Key.Enter)
		{
			this.StopEditing();
		}
	}

	private void OnWindowDeactivated(object sender, EventArgs e)
	{
		if (this.outputVM.EditingDestination)
		{
			this.StopEditing();
		}
	}

	private void StopEditing()
	{
		this.destinationEditBox.SelectionStart = 0;
		this.destinationEditBox.SelectionLength = 0;
		this.Dispatcher.BeginInvoke(new Action(() =>
		    {
				if (this.destinationEditBox.IsFocused)
				{
					this.outputPathBrowseButton.Focus();
				}
		    }));

		this.outputVM.EditingDestination = false;
		this.outputVM.SetManualOutputPath(this.outputVM.OutputPath, this.outputVM.OldOutputPath);
	}

	private void ShowStatusMessage(string message)
	{
		DispatchUtilities.BeginInvoke(() =>
		    {
		        this.statusTextBlock.Text = message;
				this.statusText.Visibility = Visibility.Visible;
		        var storyboard = (Storyboard) this.FindResource("StatusTextStoryboard");
				storyboard.Stop();
		        storyboard.Begin();
		    });
	}

	private void Window_MouseDown(object sender, MouseButtonEventArgs e)
	{
		Point hitPoint = e.GetPosition(this);

		if (this.outputVM.EditingDestination && !this.HitElement(this.destinationEditBox, hitPoint))
		{
			this.StopEditing();
		}
	}

	private void Window_StateChanged(object sender, EventArgs e)
	{
		if (this.WindowState == WindowState.Maximized || this.WindowState == WindowState.Normal)
		{
			this.RestoredWindowState = this.WindowState;
		}

		this.RefreshMaximizeRestoreButton();

		if (this.viewModel != null)
		{
			this.viewModel.RefreshTrayIcon(this.WindowState == WindowState.Minimized);
			if (this.viewModel.ShowTrayIcon)
			{
				this.Hide();
			}
		}
	}

	private bool HitElement(FrameworkElement element, Point clickedPoint)
	{
		Point relativePoint = this.destinationEditBox.TransformToAncestor(this).Transform(new Point(0, 0));

		return
			clickedPoint.X >= relativePoint.X && clickedPoint.X <= relativePoint.X + element.ActualWidth &&
			clickedPoint.Y >= relativePoint.Y && clickedPoint.Y <= relativePoint.Y + element.ActualHeight;
	}

	private void OnPickerItemMouseUp(object sender, MouseEventArgs e)
	{
		this.pickerButton.IsDropDownOpen = false;
	}

	private void AudioMouseDown(object sender, MouseButtonEventArgs e)
	{
		var listItem = (ListViewItem)sender;
		var audioTrackViewModel = (AudioTrackViewModel)listItem.DataContext;
		audioTrackViewModel.LastMouseDownTime = DateTimeOffset.UtcNow;
	}

	private void AudioMouseUp(object sender, MouseButtonEventArgs e)
	{
		var listItem = (ListViewItem)sender;
		var audioTrackViewModel = (AudioTrackViewModel)listItem.DataContext;

		// Ensure we aren't just picking up a stray mouse up after a dialog closed
		if (audioTrackViewModel.LastMouseDownTime != null && audioTrackViewModel.LastMouseDownTime > DateTimeOffset.UtcNow - TimeSpan.FromSeconds(3))
		{
			audioTrackViewModel.Selected = !audioTrackViewModel.Selected;
		}
	}

	private void OnAudioItemKeyPress(object sender, KeyEventArgs e)
	{
		if (e.Key == Key.Space || e.Key == Key.Enter)
		{
			var listItem = (ListViewItem)sender;
			var audioTrackViewModel = (AudioTrackViewModel)listItem.DataContext;
			audioTrackViewModel.Selected = !audioTrackViewModel.Selected;

			var audioCheckbox = UIUtilities.FindDescendant<System.Windows.Controls.CheckBox>(listItem);

			var peer = UIElementAutomationPeer.FromElement(audioCheckbox);
			peer?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
		}
	}

	private SourceList<SourceSubtitleViewModel> sourceSubtitles;
	private SourceList<FileSubtitleViewModel> fileSubtitles;

	private void SourceSubtitleMouseDown(object sender, MouseButtonEventArgs e)
	{
		var listItem = (ListViewItem)sender;
		var subtitleVM = (SourceSubtitleViewModel)listItem.DataContext;
		subtitleVM.LastMouseDownTime = DateTimeOffset.UtcNow;
	}

	private void SourceSubtitleMouseUp(object sender, MouseButtonEventArgs e)
	{
		var listItem = (ListViewItem)sender;
		var subtitleVM = (SourceSubtitleViewModel)listItem.DataContext;

		// Ensure we aren't just picking up a stray mouse up after a dialog closed
		if (subtitleVM.LastMouseDownTime != null && subtitleVM.LastMouseDownTime >= DateTimeOffset.UtcNow - TimeSpan.FromSeconds(3))
		{
			subtitleVM.Selected = !subtitleVM.Selected;
		}
	}

	private void OnSourceSubtitleKeyDown(object sender, KeyEventArgs e)
	{
		if (e.Key == Key.Space || e.Key == Key.Enter)
		{
			var listItem = (ListViewItem)sender;
			var subtitleViewModel = (SourceSubtitleViewModel)listItem.DataContext;
			subtitleViewModel.Selected = !subtitleViewModel.Selected;

			var subtitleCheckbox = UIUtilities.FindDescendant<System.Windows.Controls.CheckBox>(listItem);

			var peer = UIElementAutomationPeer.FromElement(subtitleCheckbox);
			peer?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
		}
	}

	private void ResizeSourceSubtitleColumns()
	{
		ResizeGridViewColumn(this.sourceTrackSummaryColumn);
		ResizeGridViewColumn(this.sourceDefaultColumn);
		ResizeGridViewColumn(this.sourceForcedColumn);
		ResizeGridViewColumn(this.sourceBurnedColumn);
		ResizeGridViewColumn(this.sourceNameColumn);
	}

	private static void ResizeGridViewColumn(GridViewColumn column)
	{
		if (double.IsNaN(column.Width))
		{
			column.Width = column.ActualWidth;
		}

		column.Width = double.NaN;
	}

	private void Main_OnClosing(object sender, CancelEventArgs e)
	{
		using (SQLiteTransaction transaction = Database.Connection.BeginTransaction())
		{
			Config.SourcePaneHeightStar = this.sourceRow.Height.Value;
			Config.QueuePaneHeightStar = this.queueRow.Height.Value;

			transaction.Commit();
		}
	}

	public void RefreshSummaryMaxSizes()
	{
		this.UpdateSourceTextMaxWidth();
		this.UpdateAudioSummaryMaxWidth();
		this.UpdateSubtitlesSummaryMaxWidth();
	}

	public void BringExternalSubtitlesIntoView()
	{
		this.fileSubtitleListView.BringIntoView();
	}

	private void Main_OnSizeChanged(object sender, SizeChangedEventArgs e)
	{
		this.RefreshSummaryMaxSizes();

		double totalHeightMinusQueue = 0;
		foreach (RowDefinition rowDefinition in this.contentGrid.RowDefinitions)
		{
			if (rowDefinition != this.queueRow)
			{
				totalHeightMinusQueue += rowDefinition.ActualHeight;
			}
		}

		double totalHeight = this.rootGrid.ActualHeight;

		double queueRowOldHeight = this.queueRow.ActualHeight;
		double queueRowDesiredHeight = totalHeight - totalHeightMinusQueue;

		if (queueRowDesiredHeight < queueRowOldHeight)
		{
			this.sourceRow.Height = new GridLength(this.sourceRow.ActualHeight, GridUnitType.Star);
			this.queueRow.Height = new GridLength(queueRowDesiredHeight, GridUnitType.Star);
		}
	}

	private void VideoTitleAngle_OnSizeChanged(object sender, SizeChangedEventArgs e)
	{
		this.UpdateSourceTextMaxWidth();
	}

	private void UpdateSourceTextMaxWidth()
	{
		double summaryWidth = this.videoSummaryColumn.ActualWidth;
		double titleAngleWidth = this.videoTitleAngle.ActualWidth;

		// 16 for image icon width, 8 for margin on path block.
		double maxPathWidth = summaryWidth - titleAngleWidth - 24;

		if (maxPathWidth > 0)
		{
			this.sourceText.SetManualMaxWidth(maxPathWidth); 
		}
	}

	private void UpdateAudioSummaryMaxWidth()
	{
		double summaryWidth = this.audioSummaryColumn.ActualWidth;
		if (summaryWidth > 40)
		{
			this.audioSummary.MaxWidth = summaryWidth - 40;
		}
	}

	private void UpdateSubtitlesSummaryMaxWidth()
	{
		double summaryWidth = this.subtitlesSummaryColumn.ActualWidth;
		if (summaryWidth > 40)
		{
			this.subtitlesSummary.MaxWidth = summaryWidth - 40;
		}
	}

	private void OnMinimizeButtonClick(object sender, RoutedEventArgs e)
	{
		this.WindowState = WindowState.Minimized;
	}

	private void OnMaximizeRestoreButtonClick(object sender, RoutedEventArgs e)
	{
		if (this.WindowState == WindowState.Maximized)
		{
			this.WindowState = WindowState.Normal;
		}
		else
		{
			this.WindowState = WindowState.Maximized;
		}
	}

	private void OnCloseButtonClick(object sender, RoutedEventArgs e)
	{
		this.Close();
	}

	private void RefreshMaximizeRestoreButton()
	{
		if (this.WindowState == WindowState.Maximized)
		{
			this.maximizeButton.Visibility = Visibility.Collapsed;
			this.restoreButton.Visibility = Visibility.Visible;
		}
		else
		{
			this.maximizeButton.Visibility = Visibility.Visible;
			this.restoreButton.Visibility = Visibility.Collapsed;
		}
	}
}
