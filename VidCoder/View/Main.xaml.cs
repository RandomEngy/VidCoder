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
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Resources;
using Fluent;
using HandBrake.Interop.Interop.Json.Encode;
using Newtonsoft.Json;
using ReactiveUI;
using Unity;
using Unity.Lifetime;
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
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using ListViewItem = System.Windows.Controls.ListViewItem;
using MenuItem = System.Windows.Controls.MenuItem;
using MessageBox = System.Windows.MessageBox;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Path = System.IO.Path;
using Point = System.Windows.Point;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace VidCoder.View
{
	public partial class Main : Window, IMainView
	{
		private const string DiscMenuItemTag = "disc";

		private readonly NotifyIcon notifyIcon;

		private MainViewModel viewModel;
		private ProcessingService processingService = Ioc.Get<ProcessingService>();
		private OutputPathService outputVM = Ioc.Get<OutputPathService>();
		private StatusService statusService = Ioc.Get<StatusService>();
		private IToastNotificationService toastNotificationService = Ioc.Get<IToastNotificationService>();

		private bool tabsVisible = false;

		public static System.Windows.Threading.Dispatcher TheDispatcher;

		public event EventHandler<RangeFocusEventArgs> RangeControlGotFocus;

		public Main()
		{
			Ioc.Container.RegisterInstance(typeof(Main), this, new ContainerControlledLifetimeManager());
			this.InitializeComponent();

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
			this.notifyIcon.Click += (sender, args) => { this.RestoreWindow(); };
			this.notifyIcon.DoubleClick += (sender, args) => { this.RestoreWindow(); };

			StreamResourceInfo streamResourceInfo = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/VidCoder_icon.ico"));
			if (streamResourceInfo != null)
			{
				Stream iconStream = streamResourceInfo.Stream;
				this.notifyIcon.Icon = new Icon(iconStream);
			}

			this.RefreshQueueColumns();
			this.LoadCompletedColumnWidths();

			if (CommonUtilities.DebugMode)
			{
				var debugDropDown = new DropDownButton { Header = "Debug" };
				var queueFromJsonItem = new Fluent.MenuItem {Header = "Queue job from JSON..."};

				queueFromJsonItem.Click += (sender, args) =>
				{
					if (!this.viewModel.HasVideoSource)
					{
						Ioc.Get<IMessageBoxService>().Show("Must open source before adding queue job from JSON");
						return;
					}

					EncodeJobViewModel jobViewModel = this.viewModel.CreateEncodeJobVM();
					DebugEncodeJsonDialog dialog = new DebugEncodeJsonDialog();
					dialog.ShowDialog();

					if (!string.IsNullOrWhiteSpace(dialog.EncodeJson))
					{
						try
						{
							JsonEncodeObject encodeObject = JsonConvert.DeserializeObject<JsonEncodeObject>(dialog.EncodeJson);

							jobViewModel.DebugEncodeJsonOverride = dialog.EncodeJson;
							jobViewModel.Job.OutputPath = encodeObject.Destination.File;
							jobViewModel.Job.SourcePath = encodeObject.Source.Path;

							this.processingService.Queue(jobViewModel);
						}
						catch (Exception exception)
						{
							MessageBox.Show(this, "Could not parse encode JSON:" + Environment.NewLine + Environment.NewLine + exception.ToString());
						}
					}
				};

				debugDropDown.Items.Add(queueFromJsonItem);
				this.toolsRibbonGroupBox.Items.Add(debugDropDown);
			}

			this.DataContextChanged += this.OnDataContextChanged;
			TheDispatcher = this.Dispatcher;

			this.statusText.Opacity = 0.0;

			NameScope.SetNameScope(this, new NameScope());
			this.RegisterName("StatusText", this.statusText);

			var storyboard = (Storyboard)this.FindResource("statusTextStoryboard");
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
		}

		public void RestoreWindow()
		{
			DispatchUtilities.BeginInvoke(() =>
			{
				this.Show();
				this.WindowState = this.RestoredWindowState;
			});
		}

		public WindowState RestoredWindowState { get; set; }

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
					ResizeGridViewColumn(this.sourceNameColumn);
					ResizeGridViewColumn(this.sourceDefaultColumn);
					ResizeGridViewColumn(this.sourceForcedColumn);
					ResizeGridViewColumn(this.sourceBurnedColumn);
					ResizeGridViewColumn(this.sourceRemoveDuplicateColumn);
					ResizeGridViewColumn(this.srtFileColumn);
					ResizeGridViewColumn(this.srtDefaultColumn);
					ResizeGridViewColumn(this.srtBurnedInColumn);
					ResizeGridViewColumn(this.srtCharCodeColumn);
					ResizeGridViewColumn(this.srtLanguageColumn);
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
			this.srtSubtitles = this.viewModel.SrtSubtitles;

			foreach (SourceSubtitleViewModel sourceVM in this.sourceSubtitles)
			{
				sourceVM.PropertyChanged += this.sourceVM_PropertyChanged;
			}

			foreach (SrtSubtitleViewModel srtVM in this.srtSubtitles)
			{
				srtVM.PropertyChanged += this.srtVM_PropertyChanged;
			}

			this.sourceSubtitles.CollectionChanged += this.sourceSubtitles_CollectionChanged;
			this.srtSubtitles.CollectionChanged += this.srtSubtitles_CollectionChanged;
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
						this.miniPreviewFrame.Width = desiredWidth;
					}
				});

		}

		public void ResizeAudioColumns()
		{
			ResizeGridViewColumn(this.audioSelectedColumn);
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
			this.SaveCompletedColumnWidths();
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

			// Add new discs
			foreach (DriveInformation driveInfo in this.viewModel.DriveCollection)
			{
				var menuItem = new Fluent.MenuItem
				{
					Header = string.Format(MainRes.OpenFormat, driveInfo.DisplayText),
					Tag = DiscMenuItemTag
				};

				menuItem.Click += (sender, args) =>
				{
					this.viewModel.SetSourceFromDvd(driveInfo);
				};

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

		private void OnPickerItemMouseUp(object sender, MouseEventArgs e)
		{
			this.pickerButton.IsDropDownOpen = false;
		}

		private void AudioItemClick(object sender, MouseButtonEventArgs e)
		{
			var listItem = sender as ListViewItem;
			var audioTrackViewModel = (AudioTrackViewModel)listItem.DataContext;
			audioTrackViewModel.Selected = !audioTrackViewModel.Selected;
		}

		private ReactiveList<SourceSubtitleViewModel> sourceSubtitles;
		private ReactiveList<SrtSubtitleViewModel> srtSubtitles;

		private void SourceSubtitleItemClick(object sender, MouseButtonEventArgs e)
		{
			var listItem = sender as ListViewItem;
			var subtitleVM = (SourceSubtitleViewModel)listItem.DataContext;
			subtitleVM.Selected = !subtitleVM.Selected;
		}

		private void sourceSubtitles_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			ResizeGridViewColumn(this.sourceNameColumn);

			if (e.Action == NotifyCollectionChangedAction.Add)
			{
				foreach (SourceSubtitleViewModel sourceVM in e.NewItems)
				{
					sourceVM.PropertyChanged += this.sourceVM_PropertyChanged;
				}
			}
			else if (e.Action == NotifyCollectionChangedAction.Remove)
			{
				foreach (SourceSubtitleViewModel sourceVM in e.OldItems)
				{
					sourceVM.PropertyChanged -= this.sourceVM_PropertyChanged;
				}
			}
		}

		private void srtSubtitles_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			ResizeGridViewColumn(this.srtCharCodeColumn);
			ResizeGridViewColumn(this.srtLanguageColumn);

			if (e.Action == NotifyCollectionChangedAction.Add)
			{
				foreach (SrtSubtitleViewModel srtVM in e.NewItems)
				{
					srtVM.PropertyChanged += this.srtVM_PropertyChanged;
				}
			}
			else if (e.Action == NotifyCollectionChangedAction.Remove)
			{
				foreach (SrtSubtitleViewModel srtVM in e.OldItems)
				{
					srtVM.PropertyChanged -= this.srtVM_PropertyChanged;
				}
			}
		}

		private void sourceVM_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			var vm = sender as SourceSubtitleViewModel;

			if (e.PropertyName == nameof(vm.Selected))
			{
				ResizeGridViewColumn(this.sourceNameColumn);
				ResizeGridViewColumn(this.sourceDefaultColumn);
				ResizeGridViewColumn(this.sourceForcedColumn);
				ResizeGridViewColumn(this.sourceBurnedColumn);
			}
		}

		private void srtVM_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			var vm = sender as SrtSubtitleViewModel;

			if (e.PropertyName == nameof(vm.CharacterCode))
			{
				ResizeGridViewColumn(this.srtCharCodeColumn);
			}
			else if (e.PropertyName == nameof(vm.LanguageCode))
			{
				ResizeGridViewColumn(this.srtLanguageColumn);
			}
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
			using (SQLiteTransaction transaction = Database.ThreadLocalConnection.BeginTransaction())
			{
				Config.SourcePaneHeightStar = this.sourceRow.Height.Value;
				Config.QueuePaneHeightStar = this.queueRow.Height.Value;

				transaction.Commit();
			}
		}

		private void Main_OnSizeChanged(object sender, SizeChangedEventArgs e)
		{
			this.UpdateSourceTextMaxLength();
		}

		private void VideoTitleAngle_OnSizeChanged(object sender, SizeChangedEventArgs e)
		{
			this.UpdateSourceTextMaxLength();
		}

		private void UpdateSourceTextMaxLength()
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
	}
}
