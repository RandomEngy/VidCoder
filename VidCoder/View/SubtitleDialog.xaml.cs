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
using VidCoder.ViewModel;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Collections.Specialized;

namespace VidCoder.View
{
    /// <summary>
    /// Interaction logic for TestDialog.xaml
    /// </summary>
    public partial class SubtitleDialog : Window, IDialog
    {
        private ObservableCollection<SourceSubtitleViewModel> sourceSubtitles;
        private ObservableCollection<SrtSubtitleViewModel> srtSubtitles;

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
            ResizeGridViewColumn(this.sourceTrackColumn);

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
            if (e.PropertyName == "TrackNumber")
            {
                ResizeGridViewColumn(this.sourceTrackColumn);
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
            this.SetPlacement(Properties.Settings.Default.SubtitlesDialogPlacement);
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            Properties.Settings.Default.SubtitlesDialogPlacement = this.GetPlacement();
            Properties.Settings.Default.Save();
        }
    }
}
