using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
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
using VidCoder.Properties;
using VidCoder.ViewModel;

namespace VidCoder.View
{
	/// <summary>
	/// Interaction logic for AudioPanel.xaml
	/// </summary>
	public partial class AudioPanel : UserControl
	{
		private ObservableCollection<AudioEncodingViewModel> audioEncodings;

		public AudioPanel()
		{
			InitializeComponent();

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
			var audioPanelVM = e.NewValue as AudioPanelViewModel;
			this.audioEncodings = audioPanelVM.AudioEncodings;

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
	}
}
