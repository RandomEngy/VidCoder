using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using VidCoder.ViewModel;
using VidCoder.Model;
using System.Collections.ObjectModel;
using System.ComponentModel;
using VidCoder.Services;
using VidCoder.Properties;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using HandBrake.Interop;
using System.Resources;
using System.IO;
using Microsoft.Practices.Unity;

namespace VidCoder.View
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		private MainViewModel viewModel;
		private bool manualSelectionChange;
		private int lastSelectedIndex;
		private ObservableCollection<SourceOptionViewModel> sourceOptions;

		private bool tabsVisible = false;

		private string oldOutputPath;

		private Storyboard presetGlowStoryboard;

		public static System.Windows.Threading.Dispatcher TheDispatcher;

		public MainWindow()
		{
			Unity.Container.RegisterInstance(this);

			InitializeComponent();

			this.RefreshQueueColumns();
			this.LoadCompletedColumnWidths();

			this.DataContextChanged += OnDataContextChanged;
			TheDispatcher = this.Dispatcher;

			this.presetGlowEffect.Opacity = 0.0;

			NameScope.SetNameScope(this, new NameScope());
			this.RegisterName("PresetGlowEffect", this.presetGlowEffect);

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

			this.KeyDown += this.MainWindow_KeyDown;
		}

		public void HandleDrop(object sender, DragEventArgs e)
		{
			var data = e.Data as DataObject;
			if (data != null && data.ContainsFileDropList())
			{
				System.Collections.Specialized.StringCollection fileList = data.GetFileDropList();
				if (fileList.Count > 0)
				{
					if (fileList.Count == 1)
					{
						if (Path.GetExtension(fileList[0]).ToLowerInvariant() == ".xml")
						{
							Unity.Container.Resolve<IPresetImportExport>().ImportPreset(fileList[0]);
						}
						else
						{
							this.manualSelectionChange = true;
							this.sourceBox.SelectedItem = this.sourceOptions.Single(item => item.SourceOption.Type == SourceType.File);
							this.RemoveDeadOptions();
							this.viewModel.SetSourceFromFile(fileList[0]);
							this.manualSelectionChange = false;
						}
					}
					else
					{
						var convertedFileList = fileList.Cast<string>().ToList();

						this.viewModel.QueueMultiple(convertedFileList);
					}
				}
			}
		}

		private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
		{
			this.viewModel = this.DataContext as MainViewModel;
			this.viewModel.PropertyChanged += this.ViewModelPropertyChanged;
			this.viewModel.AnimationStarted += this.ViewModelAnimationStarted;
			this.viewModel.ScanCancelled += this.ViewModelScanCancelled;

			this.sourceOptions = new ObservableCollection<SourceOptionViewModel>
			{
				new SourceOptionViewModel(new SourceOption { Type = SourceType.None }),
				new SourceOptionViewModel(new SourceOption { Type = SourceType.File }),
				new SourceOptionViewModel(new SourceOption { Type = SourceType.VideoFolder })
			};

			this.UpdateDrives();

			this.lastSelectedIndex = 0;
			this.sourceBox.ItemsSource = this.sourceOptions;
			this.sourceBox.SelectedIndex = 0;
		}

		private void ViewModelAnimationStarted(object sender, EventArgs<string> e)
		{
			if (e.Value == "PresetGlowHighlight")
			{
				this.presetGlowStoryboard.Begin(this);
			}
		}

		private void ViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == "DriveCollection")
			{
				this.UpdateDrives();
			}
			else if (e.PropertyName == "QueueColumns")
			{
				this.RefreshQueueColumns();
			}
			else if (e.PropertyName == "QueueColumnsSaveRequest")
			{
				this.SaveQueueColumns();
			}
			else if (e.PropertyName == "CompletedItemsCount")
			{
				this.RefreshQueueTabs();
			}
		}

		private void ViewModelScanCancelled(object sender, EventArgs e)
		{
			this.sourceOptions.Insert(0, new SourceOptionViewModel(new SourceOption { Type = SourceType.None }));
			this.sourceBox.SelectedIndex = 0;
		}

		// Updates UI with new drive options
		private void UpdateDrives()
		{
			if (!this.Dispatcher.CheckAccess())
			{
				this.Dispatcher.BeginInvoke(new Action(this.UpdateDrives));
			}
			else
			{
				// Remove all source options which do not exist in the new collection
				for (int i = this.sourceOptions.Count - 1; i >= 0; i--)
				{
					if (this.sourceOptions[i].SourceOption.Type == SourceType.Dvd)
					{
						if (!this.viewModel.DriveCollection.Any(driveInfo => driveInfo.RootDirectory == this.sourceOptions[i].SourceOption.DriveInfo.RootDirectory))
						{
							if (this.sourceBox.SelectedIndex != i)
							{
								this.sourceOptions.RemoveAt(i);
							}
						}
					}
				}

				// Update or add new options
				foreach (DriveInformation drive in this.viewModel.DriveCollection)
				{
					SourceOptionViewModel currentOption = this.sourceOptions.SingleOrDefault(sourceOptionVM => sourceOptionVM.SourceOption.Type == SourceType.Dvd && sourceOptionVM.SourceOption.DriveInfo.RootDirectory == drive.RootDirectory);

					if (currentOption == null)
					{
						// The device is new, add it
						var newSourceOptionVM = new SourceOptionViewModel(new SourceOption { Type = SourceType.Dvd, DriveInfo = drive });

						bool added = false;
						for (int i = 0; i < this.sourceOptions.Count; i++)
						{
							if (this.sourceOptions[i].SourceOption.Type == SourceType.Dvd && string.CompareOrdinal(drive.RootDirectory, this.sourceOptions[i].SourceOption.DriveInfo.RootDirectory) < 0)
							{
								this.sourceOptions.Insert(i, newSourceOptionVM);
								added = true;
								break;
							}
						}

						if (!added)
						{
							this.sourceOptions.Add(newSourceOptionVM);
						}
					}
					else
					{
						// The device existed already, update it
						if (drive.Empty)
						{
							currentOption.VolumeLabel = "(Empty)";
						}
						else
						{
							currentOption.VolumeLabel = drive.VolumeLabel;
						}
					}
				}
			}
		}

		private void sourceBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			bool reverted = false;
			var sendingBox = sender as ComboBox;
			var selectedItem = sendingBox.SelectedItem as SourceOptionViewModel;

			if (!this.manualSelectionChange)
			{
				reverted = !this.ExecuteSelectedItem();
			}

			if (!reverted)
			{
				this.viewModel.SelectedSource = selectedItem.SourceOption;
			}

			this.lastSelectedIndex = this.sourceBox.SelectedIndex;
			this.manualSelectionChange = false;
		}

		private bool ExecuteSelectedItem()
		{
			var selectedItem = this.sourceBox.SelectedItem as SourceOptionViewModel;
			bool executed = true;

			switch (selectedItem.SourceOption.Type)
			{
				case SourceType.File:
					if (!this.viewModel.SetSourceFromFile())
					{
						executed = false;
						this.RevertSelection();
					}
					else
					{
						this.RemoveDeadOptions();
					}
					break;
				case SourceType.VideoFolder:
					if (!this.viewModel.SetSourceFromFolder())
					{
						executed = false;
						this.RevertSelection();
					}
					else
					{
						this.RemoveDeadOptions();
					}
					break;
				case SourceType.Dvd:
					this.viewModel.SetSourceFromDvd(selectedItem.SourceOption.DriveInfo);
					this.RemoveDeadOptions();
					break;
				default:
					break;
			}

			return executed;
		}

		private void RevertSelection()
		{
			this.manualSelectionChange = true;
			this.sourceBox.SelectedIndex = this.lastSelectedIndex;
		}

		private void RemoveDeadOptions()
		{
			if (this.sourceOptions[0].SourceOption.Type == SourceType.None)
			{
				this.sourceOptions.RemoveAt(0);
			}

			SourceOptionViewModel emptyOption = this.sourceOptions.SingleOrDefault(sourceOptionVM => sourceOptionVM.SourceOption.Type == SourceType.Dvd && sourceOptionVM.SourceOption.DriveInfo.Empty);
			if (emptyOption != null)
			{
				this.sourceOptions.Remove(emptyOption);
			}
		}

		private void MainWindow_KeyDown(object sender, KeyEventArgs e)
		{
			bool control = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);
			bool alt = Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt);

			if (control)
			{
				switch (e.Key)
				{
					case Key.O:
						this.OpenFile();
						break;
					case Key.F:
						this.OpenFolder();
						break;
					default:
						break;
				}
			}
		}

		private void OpenFile()
		{
			if (this.viewModel.SetSourceFromFile())
			{
				this.manualSelectionChange = true;
				this.sourceBox.SelectedItem = this.sourceOptions.Single(item => item.SourceOption.Type == SourceType.File);
				this.RemoveDeadOptions();
				this.manualSelectionChange = false;
			}
		}

		private void OpenFolder()
		{
			if (this.viewModel.SetSourceFromFolder())
			{
				this.manualSelectionChange = true;
				this.sourceBox.SelectedItem = this.sourceOptions.Single(item => item.SourceOption.Type == SourceType.VideoFolder);
				this.RemoveDeadOptions();
				this.manualSelectionChange = false;
			}
		}

		private void RefreshQueueColumns()
		{
			this.queueGridView.Columns.Clear();
			ResourceManager resources = new ResourceManager("VidCoder.Properties.Resources", typeof(Resources).Assembly);

			List<Tuple<string, double>> columns = Utilities.ParseQueueColumnList(Settings.Default.QueueColumns);
			foreach (Tuple<string, double> column in columns)
			{
				GridViewColumn queueColumn = new GridViewColumn
				{
					Header = resources.GetString("QueueColumnName" + column.Item1),
					CellTemplate = this.Resources["QueueTemplate" + column.Item1] as DataTemplate,
					Width = column.Item2
				};

				this.queueGridView.Columns.Add(queueColumn);
			}

			GridViewColumn lastColumn = new GridViewColumn
			{
				CellTemplate = this.Resources["QueueRemoveTemplate"] as DataTemplate,
				Width = Settings.Default.QueueLastColumnWidth
			};
			this.queueGridView.Columns.Add(lastColumn);
		}

		private void SaveQueueColumns()
		{
			ResourceManager resources = new ResourceManager("VidCoder.Properties.Resources", typeof(Resources).Assembly);

			StringBuilder queueColumnsBuilder = new StringBuilder();
			List<Tuple<string, double>> columns = Utilities.ParseQueueColumnList(Settings.Default.QueueColumns);
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

			Settings.Default.QueueColumns = queueColumnsBuilder.ToString();
		}

		private void LoadCompletedColumnWidths()
		{
			string columnWidthsString = Settings.Default.CompletedColumnWidths;

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
			StringBuilder completedColumnsBuilder = new StringBuilder();
			for (int i = 0; i < this.completedGridView.Columns.Count; i++)
			{
				completedColumnsBuilder.Append(this.completedGridView.Columns[i].ActualWidth);

				if (i != this.completedGridView.Columns.Count - 1)
				{
					completedColumnsBuilder.Append("|");
				}
			}

			Settings.Default.CompletedColumnWidths = completedColumnsBuilder.ToString();
		}

		private void RefreshQueueTabs()
		{
			if (this.viewModel.CompletedItemsCount > 0 && !this.tabsVisible)
			{
				this.queueTab.Visibility = Visibility.Visible;
				this.completedTab.Visibility = Visibility.Visible;
				this.queueItemsTabControl.BorderThickness = new Thickness(1);
				this.tabsArea.Margin = new Thickness(6, 132, 6, 6);

				this.tabsVisible = true;
				return;
			}

			if (this.viewModel.CompletedItemsCount == 0 && this.tabsVisible)
			{
				this.queueTab.Visibility = Visibility.Collapsed;
				this.completedTab.Visibility = Visibility.Collapsed;
				this.queueItemsTabControl.BorderThickness = new Thickness(0);
				this.tabsArea.Margin = new Thickness(2, 128, 2, 2);

				this.viewModel.SelectedTabIndex = MainViewModel.QueuedTabIndex;

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
					FileService.Instance.LaunchFile(encodeResultVM.EncodeResult.Destination);
				}
				else
				{
					MessageBox.Show(resultFile + " does not exist.");
				}
			}
		}

		// Handle clicks that don't result in a selection change.
		private void OnSourceItemMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			var sendingItem = sender as ComboBoxItem;
			var clickedOption = sendingItem.DataContext as SourceOptionViewModel;

			SourceOption selectedOption = this.viewModel.SelectedSource;

			if (clickedOption == this.sourceBox.SelectedItem && (selectedOption.Type == SourceType.File || selectedOption.Type == SourceType.VideoFolder))
			{
				this.ExecuteSelectedItem();
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

				Settings.Default.MainWindowPlacement = this.GetPlacement();
				Settings.Default.Save();
			}
		}

		protected override void OnSourceInitialized(EventArgs e)
		{
			base.OnSourceInitialized(e);
			string placement = Settings.Default.MainWindowPlacement;

			if (string.IsNullOrEmpty(placement))
			{
				Rect workArea = SystemParameters.WorkArea;

				if (workArea.Width > Constants.TotalDefaultWidth && workArea.Height > Constants.TotalDefaultHeight)
				{
					double widthRemaining = workArea.Width - Constants.TotalDefaultWidth;
					double heightRemaining = workArea.Height - Constants.TotalDefaultHeight;

					this.Left = workArea.Left + widthRemaining / 2;
					this.Top = workArea.Top + heightRemaining / 2;
				}
			}
			else
			{
				this.SetPlacement(placement);
			}
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

		private void DestinationReadAreaMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			this.viewModel.EditingDestination = true;
			this.destinationEditBox.Focus();

			string path = this.viewModel.OutputPath;
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

			this.oldOutputPath = this.viewModel.OutputPath;
		}

		private void DestinationEditBoxLostFocus(object sender, RoutedEventArgs e)
		{
			this.StopEditing();
		}

		private void destinationEditBox_PreviewKeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Enter)
			{
				this.StopEditing();
			}
		}

		private void StopEditing()
		{
			this.viewModel.EditingDestination = false;
			this.viewModel.SetManualOutputPath(this.viewModel.OutputPath, this.oldOutputPath);
		}

		private void Window_MouseDown(object sender, MouseButtonEventArgs e)
		{
			if (this.viewModel.EditingDestination && !this.HitElement(this.destinationEditBox,  e.GetPosition(this)))
			{
				this.viewModel.EditingDestination = false;
			}
		}

		private bool HitElement(FrameworkElement element, Point clickedPoint)
		{
			Point relativePoint = this.destinationEditBox.TransformToAncestor(this).Transform(new Point(0, 0));

			return
				clickedPoint.X >= relativePoint.X && clickedPoint.X <= relativePoint.X + element.ActualWidth &&
				clickedPoint.Y >= relativePoint.Y && clickedPoint.Y <= relativePoint.Y + element.ActualHeight;
		}
	}
}
