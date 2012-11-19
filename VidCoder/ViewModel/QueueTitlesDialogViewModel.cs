using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Media;
using GalaSoft.MvvmLight.Command;
using HandBrake.Interop.Model;
using HandBrake.Interop.Model.Encoding;
using HandBrake.Interop.SourceData;
using System.Collections.ObjectModel;
using VidCoder.Model;

using Microsoft.Practices.Unity;

namespace VidCoder.ViewModel
{
	using System.Data.SQLite;
	using LocalResources;

	public class QueueTitlesDialogViewModel : OkCancelDialogViewModel
	{
		private List<TitleSelectionViewModel> titles;
		private ObservableCollection<TitleSelectionViewModel> selectedTitles;
		private bool selectRange;
		private int startRange;
		private int endRange;
		private bool titleStartOverrideEnabled;
		private int titleStartOverride;

		private MainViewModel main;

		public QueueTitlesDialogViewModel(List<Title> allTitles)
		{
			this.main = Unity.Container.Resolve<MainViewModel>();

			this.selectedTitles = new ObservableCollection<TitleSelectionViewModel>();
			this.selectRange = Config.QueueTitlesUseRange;
			this.startRange = Config.QueueTitlesStartTime;
			this.endRange = Config.QueueTitlesEndTime;
			this.titleStartOverrideEnabled = Config.QueueTitlesUseTitleOverride;
			this.titleStartOverride = Config.QueueTitlesTitleOverride;
			this.nameOverrideEnabled = Config.QueueTitlesUseNameOverride;
			this.nameOverride = Config.QueueTitlesNameOverride;

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

			this.selectedTitles.CollectionChanged +=
				(sender, args) =>
			    {
					this.RaisePropertyChanged(() => this.TitleDetailsVisible);

					if (this.selectedTitles.Count == 1)
					{
						Title title = this.selectedTitles[0].Title;

						// Do preview
						var previewProfile =
							new VCProfile
							{
								CustomCropping = true,
								Cropping = new Cropping(),
								VideoEncoder = "x264",
								AudioEncodings = new List<AudioEncoding>()
							};

						var previewJob =
							new VCJob
							{
								RangeType = VideoRangeType.Chapters,
								ChapterStart = 1,
								ChapterEnd = title.Chapters.Count,
								Title = title.TitleNumber,
								EncodingProfile = previewProfile
							};

						this.PreviewImage = this.main.ScanInstance.GetPreview(previewJob.HbJob, 2);
						this.RaisePropertyChanged(() => this.TitleText);
					}
			    };
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

		public bool TitleDetailsVisible
		{
			get
			{
				return this.selectedTitles.Count == 1;
			}
		}

		public string TitleText
		{
			get
			{
				if (!this.TitleDetailsVisible)
				{
					return string.Empty;
				}

				return string.Format(QueueTitlesRes.TitleFormat, this.selectedTitles[0].Title.TitleNumber);
			}
		}

		private ImageSource previewImage;
		public ImageSource PreviewImage
		{
			get
			{
				return this.previewImage;
			}

			set
			{
				this.previewImage = value;
				this.RaisePropertyChanged(() => this.PreviewImage);
			}
		}

		public bool PlayAvailable
		{
			get
			{
				return Utilities.IsDvdFolder(this.main.SourcePath);
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

		private bool nameOverrideEnabled;
		public bool NameOverrideEnabled
		{
			get
			{
				return this.nameOverrideEnabled;
			}

			set
			{
				this.nameOverrideEnabled = value;
				this.RaisePropertyChanged(() => this.NameOverrideEnabled);
			}
		}

		private string nameOverride;
		public string NameOverride
		{
			get
			{
				return this.nameOverride;
			}

			set
			{
				this.nameOverride = value;
				this.RaisePropertyChanged(() => this.NameOverride);
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

		private RelayCommand playCommand;
		public RelayCommand PlayCommand
		{
			get
			{
				return this.playCommand ?? (this.playCommand = new RelayCommand(() =>
					{
						IVideoPlayer player = Players.Installed.FirstOrDefault(p => p.Id == Config.PreferredPlayer);
						if (player == null)
						{
							player = Players.Installed[0];
						}

						player.PlayTitle(this.main.SourcePath, this.selectedTitles[0].Title.TitleNumber);
					},
					() =>
					{
						return Players.Installed.Count > 0;
					}));
			}
		}

		public override void OnClosing()
		{
			using (SQLiteTransaction transaction = Database.ThreadLocalConnection.BeginTransaction())
			{
				Config.QueueTitlesUseRange = this.SelectRange;
				Config.QueueTitlesStartTime = this.StartRange;
				Config.QueueTitlesEndTime = this.EndRange;
				Config.QueueTitlesUseTitleOverride = this.TitleStartOverrideEnabled;
				Config.QueueTitlesTitleOverride = this.TitleStartOverride;
				Config.QueueTitlesUseNameOverride = this.NameOverrideEnabled;
				Config.QueueTitlesNameOverride = this.NameOverride;

				transaction.Commit();
			}

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
