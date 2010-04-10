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
using System.Windows.Shapes;
using VidCoder.ViewModel;
using VidCoder.Model;
using System.Collections.ObjectModel;
using System.ComponentModel;
using VidCoder.Services;
using VidCoder.Properties;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using HandBrake.Interop;

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

        private Storyboard presetGlowStoryboard;

        public static MainWindow TheWindow;

        public MainWindow()
        {
            InitializeComponent();

            this.DataContextChanged += OnDataContextChanged;
            DispatchService.DispatchObject = this.Dispatcher;
            TheWindow = this;

            this.presetGlowEffect.Opacity = 0.0;

            NameScope.SetNameScope(this, new NameScope());
            this.RegisterName("PresetGlowEffect", this.presetGlowEffect);

            DoubleAnimation presetGlowFadeUp = new DoubleAnimation
            {
                From = 0.0,
                To = 1.0,
                Duration = new Duration(TimeSpan.FromSeconds(0.1))
            };

            DoubleAnimation presetGlowFadeDown = new DoubleAnimation
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
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            this.viewModel = this.DataContext as MainViewModel;
            this.viewModel.PropertyChanged += this.ViewModelPropertyChanged;
            this.viewModel.AnimationStarted += this.ViewModelAnimationStarted;

            this.sourceOptions = new ObservableCollection<SourceOptionViewModel>
            {
                new SourceOptionViewModel(new SourceOption { Type = SourceType.None, Image = "", Text = "Choose a video source." }),
                new SourceOptionViewModel(new SourceOption { Type = SourceType.File, Image = "/Icons/avi.png", Text = "Video File" }),
                new SourceOptionViewModel(new SourceOption { Type = SourceType.VideoFolder, Image = "/Icons/folder.png", Text = "DVD/VIDEO_TS Folder" })
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

        private void ViewModelPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "DriveCollection")
            {
                this.UpdateDrives();
            }
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
                        SourceOptionViewModel newSourceOptionVM = new SourceOptionViewModel(new SourceOption { Type = SourceType.Dvd, Image = "/Icons/disc.png", DriveInfo = drive });

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
            ComboBox sendingBox = sender as ComboBox;
            SourceOptionViewModel selectedItem = sendingBox.SelectedItem as SourceOptionViewModel;

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

        private void Window_Drop(object sender, DragEventArgs e)
        {
            DataObject data = e.Data as DataObject;
            if (data != null && data.ContainsFileDropList())
            {
                System.Collections.Specialized.StringCollection fileList = data.GetFileDropList();
                if (fileList.Count > 0)
                {
                    this.manualSelectionChange = true;
                    this.sourceBox.SelectedItem = this.sourceOptions.Single(item => item.SourceOption.Type == SourceType.File);
                    this.RemoveDeadOptions();
                    this.viewModel.SetSourceFromFile(fileList[0]);
                    System.Diagnostics.Debug.WriteLine(fileList[0]);
                }
            }
        }

        private void Window_PreviewDragOver(object sender, DragEventArgs e)
        {
            DataObject data = e.Data as DataObject;
            if (data != null && data.ContainsFileDropList())
            {
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
            }
        }
    }
}
