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
using VidCoder.Extensions;
using VidCoder.ViewModel;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Collections.Specialized;
using ReactiveUI;

namespace VidCoder.View
{
	/// <summary>
	/// Interaction logic for TestDialog.xaml
	/// </summary>
	public partial class SubtitleDialog : Window, IDialog
	{
		private ReactiveList<SourceSubtitleViewModel> sourceSubtitles;
		private ReactiveList<SrtSubtitleViewModel> srtSubtitles;

		public SubtitleDialog()
		{
			InitializeComponent();
		}

		private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
		{
			var subtitleDialogVM = e.NewValue as SubtitleDialogViewModel;
			this.sourceSubtitles = subtitleDialogVM.SourceSubtitles;
			this.srtSubtitles = subtitleDialogVM.SrtSubtitles;

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
		}

		private void sourceSubtitles_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			ResizeGridViewColumn(this.nameColumn);

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
			if (e.PropertyName == "Selected")
			{
				ResizeGridViewColumn(this.nameColumn);
				ResizeGridViewColumn(this.sourceDefaultColumn);
				ResizeGridViewColumn(this.sourceForcedColumn);
				ResizeGridViewColumn(this.sourceBurnedColumn);
			}
		}

		private void srtVM_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == "CharacterCode")
			{
				ResizeGridViewColumn(this.srtCharCodeColumn);
			}
			else if (e.PropertyName == "LanguageCode")
			{
				ResizeGridViewColumn(this.srtLanguageColumn);
			}
		}

		private void SourceItemClick(object sender, MouseButtonEventArgs e)
		{
			var listItem = sender as ListViewItem;
			var subtitleVM = listItem.DataContext as SourceSubtitleViewModel;
			subtitleVM.Selected = !subtitleVM.Selected;
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
