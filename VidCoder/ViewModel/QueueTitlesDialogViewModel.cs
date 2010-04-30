using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HandBrake.SourceData;
using System.Collections.ObjectModel;
using VidCoder.Properties;

namespace VidCoder.ViewModel
{
    public class QueueTitlesDialogViewModel : OkCancelDialogViewModel
    {
        private List<TitleSelectionViewModel> titles;
        private ObservableCollection<TitleSelectionViewModel> selectedTitles;
        private bool selectRange;
        private int startRange;
        private int endRange;

        public QueueTitlesDialogViewModel(List<Title> allTitles)
        {
            this.selectedTitles = new ObservableCollection<TitleSelectionViewModel>();
            this.startRange = Settings.Default.QueueTitlesStartTime;
            this.endRange = Settings.Default.QueueTitlesEndTime;

            this.titles = new List<TitleSelectionViewModel>();
            foreach (Title title in allTitles)
            {
                var titleVM = new TitleSelectionViewModel(title, this);
                this.titles.Add(titleVM);
            }
        }

        public List<TitleSelectionViewModel> Titles
        {
            get
            {
                return this.titles;
            }
        }

        public ObservableCollection<TitleSelectionViewModel> SelectedTitles
        {
            get
            {
                return this.selectedTitles;
            }
        }

        public bool SelectRange
        {
            get
            {
                return this.selectRange;
            }

            set
            {
                this.selectRange = value;
                if (value)
                {
                    this.SetSelectedFromRange();
                }
                else
                {
                    foreach (TitleSelectionViewModel titleVM in this.titles)
                    {
                        if (titleVM.Selected)
                        {
                            titleVM.SetSelected(false);
                        }
                    }
                }

                this.NotifyPropertyChanged("SelectRange");
            }
        }

        public int StartRange
        {
            get
            {
                return this.startRange;
            }

            set
            {
                this.startRange = value;
                this.NotifyPropertyChanged("StartRange");
            }
        }

        public int EndRange
        {
            get
            {
                return this.endRange;
            }

            set
            {
                this.endRange = value;
                this.NotifyPropertyChanged("EndRange");
            }
        }

        public List<Title> CheckedTitles
        {
            get
            {
                List<Title> checkedTitles = new List<Title>();
                foreach (TitleSelectionViewModel titleVM in this.titles)
                {
                    if (titleVM.Selected)
                    {
                        checkedTitles.Add(titleVM.Title);
                    }
                }

                return checkedTitles;
            }
        }

        public override void OnClosing()
        {
            Settings.Default.QueueTitlesStartTime = this.StartRange;
            Settings.Default.QueueTitlesEndTime = this.EndRange;
            Settings.Default.Save();
            base.OnClosing();
        }

        public void HandleCheckChanged(TitleSelectionViewModel changedTitleVM, bool newValue)
        {
            if (this.SelectedTitles.Contains(changedTitleVM))
            {
                foreach (TitleSelectionViewModel titleVM in this.SelectedTitles)
                {
                    if (titleVM != changedTitleVM && titleVM.Selected != newValue)
                    {
                        titleVM.SetSelected(newValue);
                    }
                }
            }
        }

        private void SetSelectedFromRange()
        {
            TimeSpan lowerBound = TimeSpan.FromMinutes(this.StartRange);
            TimeSpan upperBound = TimeSpan.FromMinutes(this.EndRange);

            foreach (TitleSelectionViewModel titleVM in this.titles)
            {
                if (titleVM.Title.Duration >= lowerBound && titleVM.Title.Duration <= upperBound)
                {
                    if (!titleVM.Selected)
                    {
                        titleVM.SetSelected(true);
                    }
                }
                else if (titleVM.Selected)
                {
                    titleVM.SetSelected(false);
                }
            }
        }
    }
}
