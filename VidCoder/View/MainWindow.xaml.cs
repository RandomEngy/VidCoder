using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Resources;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using GalaSoft.MvvmLight.Messaging;
using Hardcodet.Wpf.TaskbarNotification;
using VidCoder.Extensions;
using VidCoder.Messages;
using VidCoder.Model;
using VidCoder.Resources;
using VidCoder.Services;
using VidCoder.ViewModel;
using VidCoderCommon.Model;

namespace VidCoder.View
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		private MainViewModel viewModel;
		private ProcessingService processingService = Ioc.Container.GetInstance<ProcessingService>();
		private OutputPathService outputVM = Ioc.Container.GetInstance<OutputPathService>();

		private bool tabsVisible = false;

		private Storyboard presetGlowStoryboard;
		private Storyboard pickerGlowStoryboard;

		public static System.Windows.Threading.Dispatcher TheDispatcher;

		public MainWindow()
		{
			Ioc.Container.Register(() => this);
			this.InitializeComponent();

			this.RefreshQueueColumns();
			this.LoadCompletedColumnWidths();

			this.DataContextChanged += OnDataContextChanged;
			TheDispatcher = this.Dispatcher;

			this.presetGlowEffect.Opacity = 0.0;
			this.pickerGlowEffect.Opacity = 0.0;
			this.statusText.Opacity = 0.0;

			NameScope.SetNameScope(this, new NameScope());
			this.RegisterName("PresetGlowEffect", this.presetGlowEffect);
			this.RegisterName("PickerGlowEffect", this.pickerGlowEffect);
			this.RegisterName("StatusText", this.statusText);

			var storyboard = (Storyboard)this.FindResource("statusTextStoryboard");
			storyboard.Completed += (sender, args) =>
				{
					this.statusText.Visibility = Visibility.Collapsed;
				};

			var presetGlowFadeUp = new DoubleAnimation
			{
				From = 0.0,
				To = 1.0,
				Duration = new Duration(TimeSpan.FromSeconds(0.1))
			};

			var presetGlowFadeDown = new DoubleAnimation
			{
				From = 1.0,
				To = 0.0,
				BeginTime = TimeSpan.FromSeconds(0.1),
				Duration = new Duration(TimeSpan.FromSeconds(1.6))
			};

			this.presetGlowStoryboard = new Storyboard();
			this.presetGlowStoryboard.Children.Add(presetGlowFadeUp);
			this.presetGlowStoryboard.Children.Add(presetGlowFadeDown);

			Storyboard.SetTargetName(presetGlowFadeUp, "PresetGlowEffect");
			Storyboard.SetTargetProperty(presetGlowFadeUp, new PropertyPath("Opacity"));
			Storyboard.SetTargetName(presetGlowFadeDown, "PresetGlowEffect");
			Storyboard.SetTargetProperty(presetGlowFadeDown, new PropertyPath("Opacity"));

			var pickerGlowFadeUp = new DoubleAnimation
			{
				From = 0.0,
				To = 1.0,
				Duration = new Duration(TimeSpan.FromSeconds(0.1))
			};

			var pickerGlowFadeDown = new DoubleAnimation
			{
				From = 1.0,
				To = 0.0,
				BeginTime = TimeSpan.FromSeconds(0.1),
				Duration = new Duration(TimeSpan.FromSeconds(1.6))
			};

			this.pickerGlowStoryboard = new Storyboard();
			this.pickerGlowStoryboard.Children.Add(pickerGlowFadeUp);
			this.pickerGlowStoryboard.Children.Add(pickerGlowFadeDown);

			Storyboard.SetTargetName(pickerGlowFadeUp, "PickerGlowEffect");
			Storyboard.SetTargetProperty(pickerGlowFadeUp, new PropertyPath("Opacity"));
			Storyboard.SetTargetName(pickerGlowFadeDown, "PickerGlowEffect");
			Storyboard.SetTargetProperty(pickerGlowFadeDown, new PropertyPath("Opacity"));

			this.Loaded += (e, o) =>
			{
                this.RegisterGlobalHotkeys();
				this.RestoredWindowState = this.WindowState;
			};

			Messenger.Default.Register<StatusMessage>(this, this.ShowStatusMessage);

			Messenger.Default.Register<SaveQueueColumnsMessage>(
				this,
				message =>
					{
						this.SaveQueueColumns();
					});

			Messenger.Default.Register<ApplyQueueColumnsMessage>(
				this,
				message =>
					{
						this.RefreshQueueColumns();
					});
		}

		public WindowState RestoredWindowState { get; set; }

		public void HandleDrop(object sender, DragEventArgs e)
		{
			var data = e.Data as DataObject;
			if (data != null && data.ContainsFileDropList())
			{
				StringCollection itemList = data.GetFileDropList();
				if (itemList.Count > 0)
				{
					if (itemList.Count == 1)
					{
						string item = itemList[0];

						if (Path.GetExtension(item).ToLowerInvariant() == ".xml")
						{
							// It's a preset
							Ioc.Container.GetInstance<IPresetImportExport>().ImportPreset(itemList[0]);
						}
						else if (Utilities.IsDiscFolder(item))
						{
							// It's a disc folder or disc
							this.viewModel.SetSource(item);
						}
						else
						{
							// It is a video file or folder full of video files
							this.HandleDropAsPaths(itemList);
						}
					}
					else
					{
						// With multiple items, treat it as a list video files/disc folders or folders full of those items
						this.HandleDropAsPaths(itemList);
					}
				}
			}
		}

		// Takes a list of files/directories and tries to scan/queue them as files/disc folders
		private void HandleDropAsPaths(StringCollection itemList)
		{
			List<SourcePath> fileList = GetPathList(itemList);
			if (fileList.Count > 0)
			{
				if (fileList.Count == 1)
				{
					this.viewModel.SetSourceFromFile(fileList[0].Path);
				}
				else
				{
					this.processingService.QueueMultiple(fileList);
				}
			}
		}

		// Gets a file/video folder list from a list of files/directories
		private static List<SourcePath> GetPathList(StringCollection itemList)
		{
			var videoExtensions = new List<string>();
			string extensionsString = Config.VideoFileExtensions;
			string[] rawExtensions = extensionsString.Split(',', ';');
			foreach (string rawExtension in rawExtensions)
			{
				string extension = rawExtension.Trim();
				if (extension.Length > 0)
				{
					if (!extension.StartsWith("."))
					{
						extension = "." + extension;
					}

					videoExtensions.Add(extension);
				}
			}

			var pathList = new List<SourcePath>();
			foreach (string item in itemList)
			{
				var fileAttributes = File.GetAttributes(item);
				if ((fileAttributes & FileAttributes.Directory) == FileAttributes.Directory)
				{
					// Path is a directory
					if (Utilities.IsDiscFolder(item))
					{
						// If it's a disc folder, add it
						pathList.Add(new SourcePath { Path = item, SourceType = SourceType.VideoFolder });
					}
					else
					{
						string parentFolder = Path.GetDirectoryName(item);
						pathList.AddRange(
							Utilities.GetFilesOrVideoFolders(item, videoExtensions)
							.Select(p => new SourcePath
								{
									Path = p, 
									ParentFolder = parentFolder, 
									SourceType = SourceType.None
								}));
					}
				}
				else
				{
					// Path is a file
					pathList.Add(new SourcePath { Path = item, SourceType = SourceType.File });
				}
			}

			return pathList;
		}

		public void ShowBalloonMessage(string title, string message)
		{
			if (this.trayIcon.Visibility == Visibility.Visible)
			{
				this.trayIcon.ShowBalloonTip(title, message, BalloonIcon.Info);
			}
		}

		private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
		{
			this.viewModel = this.DataContext as MainViewModel;
			this.viewModel.PropertyChanged += this.ViewModelPropertyChanged;
			this.viewModel.AnimationStarted += this.ViewModelAnimationStarted;
			this.processingService.PropertyChanged += (sender2, e2) =>
			    {
					if (e2.PropertyName == "CompletedItemsCount")
					{
						this.RefreshQueueTabs();
					}
			    };

			this.RefreshQueueTabs();
		}

		private void ViewModelAnimationStarted(object sender, EventArgs<string> e)
		{
			if (e.Value == "PresetGlowHighlight")
			{
				this.presetGlowStoryboard.Begin(this);
			}
			else if (e.Value == "PickerGlowHighlight")
			{
				this.pickerGlowStoryboard.Begin(this);
			}
		}

		private void ViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == "RangeType")
			{
				//DispatchUtilities.BeginInvoke(() => this.rangeDetailsPopup.IsOpen = false);
			}
		}

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

		private void SaveCompletedColumnWidths()
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
				//this.tabsArea.Margin = new Thickness(6,6,6,0);

				this.tabsVisible = true;
				return;
			}

			if (this.processingService.CompletedItemsCount == 0 && this.tabsVisible)
			{
				this.queueTab.Visibility = Visibility.Collapsed;
				this.completedTab.Visibility = Visibility.Collapsed;
				this.clearCompletedQueueItemsButton.Visibility = Visibility.Collapsed;
				this.queueItemsTabControl.BorderThickness = new Thickness(0);
				//this.tabsArea.Margin = new Thickness(0,6,0,0);

				this.processingService.SelectedTabIndex = ProcessingService.QueuedTabIndex;

				this.tabsVisible = false;
				return;
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
					this.ShowStatusMessage(new StatusMessage { Message = MainRes.PlayingVideoMessage });
					FileService.Instance.PlayVideo(encodeResultVM.EncodeResult.Destination);
				}
				else
				{
					MessageBox.Show(string.Format(MainRes.FileDoesNotExist, resultFile));
				}
			}
		}

		private void Window_Closing(object sender, CancelEventArgs e)
		{
			if (!this.viewModel.OnClosing())
			{
				e.Cancel = true;
			}
			else
			{
				this.SaveQueueColumns();
				this.SaveCompletedColumnWidths();

				Config.MainWindowPlacement = this.GetPlacementXml();
			}
		}

		protected override void OnSourceInitialized(EventArgs e)
		{
			base.OnSourceInitialized(e);
			this.PlaceDynamic(Config.MainWindowPlacement);

			var source = PresentationSource.FromVisual(this) as HwndSource;
			source.AddHook(WndProc);
		}

		private void Window_PreviewDragOver(object sender, DragEventArgs e)
		{
			Utilities.SetDragIcon(e);
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
			Messenger.Default.Send(new RangeFocusMessage { GotFocus = true, RangeType = VideoRangeType.Seconds, Start = true });
		}

		private void EndTimeGotFocus(object sender, RoutedEventArgs e)
		{
			Messenger.Default.Send(new RangeFocusMessage { GotFocus = true, RangeType = VideoRangeType.Seconds, Start = false });
		}

		private void FramesStartGotFocus(object sender, RoutedEventArgs e)
		{
			Messenger.Default.Send(new RangeFocusMessage { GotFocus = true, RangeType = VideoRangeType.Frames, Start = true });
		}

		private void FramesEndGotFocus(object sender, RoutedEventArgs e)
		{
			Messenger.Default.Send(new RangeFocusMessage { GotFocus = true, RangeType = VideoRangeType.Frames, Start = false });
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

			this.outputVM.OldOutputPath = this.outputVM.OutputPath;
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

		private void ShowStatusMessage(StatusMessage message)
		{
			DispatchUtilities.BeginInvoke(() =>
			    {
			        this.statusTextBlock.Text = message.Message;
					this.statusText.Visibility = Visibility.Visible;
			        var storyboard = (Storyboard) this.FindResource("statusTextStoryboard");
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

			if (this.viewModel.SourceSelectionExpanded && !this.HitElement(this.sourceSelectionMenu, hitPoint))
			{
				this.viewModel.SourceSelectionExpanded = false;
			}
		}

		private void Window_StateChanged(object sender, EventArgs e)
		{
			if (this.WindowState == WindowState.Maximized || this.WindowState == WindowState.Normal)
			{
				this.RestoredWindowState = this.WindowState;
			}

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

		// Handle native window messages. 
		private IntPtr WndProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
		{
			if (message == NativeMethods.WM_SHOWME)
			{
				// This is a message from a second instance trying to start up. Bring window to foreground when this happens.
				this.Activate();
			}

			return IntPtr.Zero;
		}
	}
}
