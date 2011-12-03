using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HandBrake.Interop.SourceData;
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
		private bool titleStartOverrideEnabled;
		private int titleStartOverride;

		public QueueTitlesDialogViewModel(List<Title> allTitles)
		{
			this.selectedTitles = new ObservableCollection<TitleSelectionViewModel>();
			this.selectRange = Settings.Default.QueueTitlesUseRange;
			this.startRange = Settings.Default.QueueTitlesStartTime;
			this.endRange = Settings.Default.QueueTitlesEndTime;
			this.titleStartOverrideEnabled = Settings.Default.QueueTitlesUseTitleOverride;
			this.titleStartOverride = Settings.Default.QueueTitlesTitleOverride;

			this.titles = new List<TitleSelectionViewModel>();
			foreach (Title title in allTitles)
			{
				var titleVM = new TitleSelectionViewModel(title, this);
				this.titles.Add(titleVM);
			}

			// Perform range selection if enabled.
			if (this.selectRange)
			{
				this.SetSelectedFromRange();
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

				this.RaisePropertyChanged(() => this.SelectRange);
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

				if (this.SelectRange)
				{
					this.SetSelectedFromRange();
				}

				this.RaisePropertyChanged(() => this.StartRange);
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

				if (this.SelectRange)
				{
					this.SetSelectedFromRange();
				}

				this.RaisePropertyChanged(() => this.EndRange);
			}
		}

		public bool TitleStartOverrideEnabled
		{
			get
			{
				return this.titleStartOverrideEnabled;
			}

			set
			{
				this.titleStartOverrideEnabled = value;
				this.RaisePropertyChanged(() => this.TitleStartOverrideEnabled);
			}
		}

		public int TitleStartOverride
		{
			get
			{
				return this.titleStartOverride;
			}

			set
			{
				this.titleStartOverride = value;
				this.RaisePropertyChanged(() => this.TitleStartOverride);
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
			Settings.Default.QueueTitlesUseRange = this.SelectRange;
			Settings.Default.QueueTitlesStartTime = this.StartRange;
			Settings.Default.QueueTitlesEndTime = this.EndRange;
			Settings.Default.QueueTitlesUseTitleOverride = this.TitleStartOverrideEnabled;
			Settings.Default.QueueTitlesTitleOverride = this.TitleStartOverride;
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
