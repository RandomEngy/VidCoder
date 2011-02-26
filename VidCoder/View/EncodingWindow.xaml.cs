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
using System.Windows.Shapes;
using System.ComponentModel;
using VidCoder.ViewModel;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using VidCoder.Properties;

namespace VidCoder.View
{
	/// <summary>
	/// Interaction logic for EncodingWindow.xaml
	/// </summary>
	public partial class EncodingWindow : Window
	{
		private ObservableCollection<AudioEncodingViewModel> audioEncodings;

		public EncodingWindow()
		{
			InitializeComponent();

			this.tabControl.SelectedIndex = Settings.Default.EncodingDialogLastTab;

			if (Settings.Default.ShowAudioTrackNameField)
			{
				var gridView = this.audioList.View as GridView;
				gridView.Columns.Insert(
					gridView.Columns.Count - 1,
					new GridViewColumn
					{
						Header = "Name",
						Width = 98,
						CellTemplate = this.Resources["NameTemplate"] as DataTemplate
					});
			}
		}

		private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
		{
			var encodingDialogVM = e.NewValue as EncodingViewModel;
			this.audioEncodings = encodingDialogVM.AudioEncodings;

			foreach (AudioEncodingViewModel encodingVM in this.audioEncodings)
			{
				encodingVM.PropertyChanged += this.encodingVM_PropertyChanged;
			}

			this.audioEncodings.CollectionChanged += this.audioEncodings_CollectionChanged;
		}


		private void audioEncodings_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			ResizeGridViewColumn(this.targetStreamColumn);

			if (e.Action == NotifyCollectionChangedAction.Add)
			{
				foreach (AudioEncodingViewModel encodingVM in e.NewItems)
				{
					encodingVM.PropertyChanged += this.encodingVM_PropertyChanged;
				}
			}
			else if (e.Action == NotifyCollectionChangedAction.Remove)
			{
				foreach (AudioEncodingViewModel encodingVM in e.OldItems)
				{
					encodingVM.PropertyChanged -= this.encodingVM_PropertyChanged;
				}
			}
		}

		private void encodingVM_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			switch (e.PropertyName)
			{
				case "TargetStreamIndex":
					ResizeGridViewColumn(this.targetStreamColumn);
					break;
				default:
					break;
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

		protected override void OnSourceInitialized(EventArgs e)
		{
			base.OnSourceInitialized(e);
			string placement = Settings.Default.EncodingDialogPlacement;
			if (string.IsNullOrEmpty(placement))
			{
				Rect workArea = SystemParameters.WorkArea;

				if (workArea.Width > Constants.TotalDefaultWidth && workArea.Height > Constants.TotalDefaultHeight)
				{
					double widthRemaining = workArea.Width - Constants.TotalDefaultWidth;
					double heightRemaining = workArea.Height - Constants.TotalDefaultHeight;

					this.Left = workArea.Left + widthRemaining / 2;
					this.Top = workArea.Top + heightRemaining / 2 + 452;
				}
			}
			else
			{
				this.SetPlacement(placement);
			}
		}

		private void Window_Closing(object sender, CancelEventArgs e)
		{
			Settings.Default.EncodingDialogPlacement = this.GetPlacement();
			Settings.Default.EncodingDialogLastTab = this.tabControl.SelectedIndex;
			Settings.Default.Save();
		}

		private void ToolBar_Loaded(object sender, RoutedEventArgs e)
		{
			var toolBar = sender as ToolBar;
			var overflowGrid = toolBar.Template.FindName("OverflowGrid", toolBar) as FrameworkElement;
			if (overflowGrid != null)
			{
				overflowGrid.Visibility = Visibility.Collapsed;
			}
		}
	}
}
