using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using HandBrake.ApplicationServices.Interop;
using HandBrake.ApplicationServices.Interop.Json.Scan;
using HandBrake.ApplicationServices.Interop.Model.Encoding;
using VidCoder.Services;
using VidCoder.ViewModel.Components;
using VidCoderCommon.Model;

namespace VidCoder.ViewModel
{
	using Resources;

	public class SubtitleDialogViewModel : OkCancelDialogViewModel
	{
		private ObservableCollection<SourceSubtitleViewModel> sourceSubtitles;
		private ObservableCollection<SrtSubtitleViewModel> srtSubtitles;

		private HBContainer container;
		private bool defaultsEnabled;

		private bool textSubtitleWarningVisible;
		private bool burnedOverlapWarningVisible;

		private MainViewModel mainViewModel = Ioc.Container.GetInstance<MainViewModel>();
		private PresetsViewModel presetsViewModel = Ioc.Container.GetInstance<PresetsViewModel>();

		public SubtitleDialogViewModel(VCSubtitles currentSubtitles)
		{
			this.container = HandBrakeEncoderHelpers.GetContainer(this.presetsViewModel.SelectedPreset.Preset.EncodingProfile.ContainerName);

			this.sourceSubtitles = new ObservableCollection<SourceSubtitleViewModel>();
			this.srtSubtitles = new ObservableCollection<SrtSubtitleViewModel>();

			if (currentSubtitles != null)
			{
				if (!currentSubtitles.SourceSubtitles.Any(s => s.TrackNumber == 0))
				{
					this.sourceSubtitles.Add(
						new SourceSubtitleViewModel(
							this,
							new SourceSubtitle
								{
									TrackNumber = 0,
									BurnedIn = false,
									Forced = false,
									Default = false
								}));
				}

				foreach (SourceSubtitle subtitle in currentSubtitles.SourceSubtitles)
				{
					this.sourceSubtitles.Add(new SourceSubtitleViewModel(this, subtitle.Clone()) { Selected = true });
				}
				
				foreach (SrtSubtitle subtitle in currentSubtitles.SrtSubtitles)
				{
					this.srtSubtitles.Add(new SrtSubtitleViewModel(this, subtitle.Clone()));
				}
			}

			// Fill in remaining unselected source subtitles
			for (int i = 1; i <= this.mainViewModel.SelectedTitle.SubtitleList.Count; i++)
			{
				if (!currentSubtitles.SourceSubtitles.Any(s => s.TrackNumber == i))
				{
					var newSubtitle = new SourceSubtitle
					{
						TrackNumber = i,
						Default = false,
						Forced = false,
						BurnedIn = false
					};

					this.sourceSubtitles.Add(new SourceSubtitleViewModel(this, newSubtitle));
				}
			}

			this.sourceSubtitles.CollectionChanged += this.SourceCollectionChanged;
			this.srtSubtitles.CollectionChanged += this.SrtCollectionChanged;

			this.UpdateBoxes();
		}

		private void SourceCollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
		{
			this.RaisePropertyChanged(() => this.HasSourceSubtitles);
			this.RaisePropertyChanged(() => this.AddSourceSubtitleToolTip);
		}

		private void SrtCollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
		{
			this.RaisePropertyChanged(() => this.HasSrtSubtitles);
		}

		public ObservableCollection<SourceSubtitleViewModel> SourceSubtitles
		{
			get { return this.sourceSubtitles; }
		}

		public ObservableCollection<SrtSubtitleViewModel> SrtSubtitles
		{
			get { return this.srtSubtitles; }
		}

		public VCSubtitles ChosenSubtitles
		{
			get
			{
				VCSubtitles subtitlesModel = new VCSubtitles();

				List<SrtSubtitle> srtSubtitlesModel = new List<SrtSubtitle>();
				foreach (SrtSubtitleViewModel srtSubtitleModel in this.srtSubtitles)
				{
					srtSubtitlesModel.Add(srtSubtitleModel.Subtitle);
				}

				List<SourceSubtitle> sourceSubtitlesModel = new List<SourceSubtitle>();
				foreach (SourceSubtitleViewModel sourceSubtitleModel in this.sourceSubtitles)
				{
					if (sourceSubtitleModel.Selected)
					{
						sourceSubtitlesModel.Add(sourceSubtitleModel.Subtitle);
					}
				}

				subtitlesModel.SrtSubtitles = srtSubtitlesModel;
				subtitlesModel.SourceSubtitles = sourceSubtitlesModel;

				return subtitlesModel;
			}
		}

		public bool HasSourceSubtitles
		{
			get
			{
				return this.sourceSubtitles.Count > 0;
			}
		}

		public bool HasSrtSubtitles
		{
			get
			{
				return this.srtSubtitles.Count > 0;
			}
		}

		public bool TextSubtitleWarningVisibile
		{
			get
			{
				return this.textSubtitleWarningVisible;
			}

			set
			{
				this.textSubtitleWarningVisible = value;
				this.RaisePropertyChanged(() => this.TextSubtitleWarningVisibile);
			}
		}

		public bool BurnedOverlapWarningVisible
		{
			get
			{
				return this.burnedOverlapWarningVisible;
			}

			set
			{
				this.burnedOverlapWarningVisible = value;
				this.RaisePropertyChanged(() => this.BurnedOverlapWarningVisible);
			}
		}

		public bool DefaultsEnabled
		{
			get
			{
				return this.defaultsEnabled;
			}

			set
			{
				this.defaultsEnabled = value;
				this.RaisePropertyChanged(() => this.DefaultsEnabled);
			}
		}

		private RelayCommand addSrtSubtitleCommand;
		public RelayCommand AddSrtSubtitleCommand
		{
			get
			{
				return this.addSrtSubtitleCommand ?? (this.addSrtSubtitleCommand = new RelayCommand(() =>
					{
						string srtFile = FileService.Instance.GetFileNameLoad(
							Config.RememberPreviousFiles ? Config.LastSrtFolder : null,
							SubtitleRes.SrtFilePickerText,
							"srt",
							"SRT Files |*.srt");

						if (srtFile != null)
						{
							if (Config.RememberPreviousFiles)
							{
								Config.LastSrtFolder = Path.GetDirectoryName(srtFile);
							}

							SrtSubtitle newSubtitle = new SrtSubtitle { FileName = srtFile, Default = false, CharacterCode = "UTF-8", LanguageCode = "eng", Offset = 0 };
							this.srtSubtitles.Add(new SrtSubtitleViewModel(this, newSubtitle));
						}

						this.UpdateWarningVisibility();
					}));
			}
		}

		public string AddSourceSubtitleToolTip
		{
			get
			{
				if (this.mainViewModel.SelectedTitle == null || this.mainViewModel.SelectedTitle.SubtitleList.Count == 0)
				{
					return SubtitleRes.AddSourceNoSubtitlesToolTip;
				}

				return null;
			}
		}

		public HBContainer Container
		{
			get
			{
				return this.container;
			}
		}

		public void RemoveSourceSubtitle(SourceSubtitleViewModel subtitleViewModel)
		{
			this.SourceSubtitles.Remove(subtitleViewModel);
			this.UpdateBoxes();
			this.UpdateButtonVisibility();
			this.UpdateWarningVisibility();
		}

		public void DuplicateSourceSubtitle(SourceSubtitleViewModel sourceSubtitleViewModel)
		{
			sourceSubtitleViewModel.Selected = true;
			if (sourceSubtitleViewModel.BurnedIn)
			{
				sourceSubtitleViewModel.BurnedIn = false;
			}

			var newSubtitle = new SourceSubtitleViewModel(
				this,
				new SourceSubtitle
					{
						BurnedIn = false,
						Default = false,
						Forced = !sourceSubtitleViewModel.ForcedOnly,
						TrackNumber = sourceSubtitleViewModel.TrackNumber
					});
			newSubtitle.Selected = true;

			this.SourceSubtitles.Insert(
				this.SourceSubtitles.IndexOf(sourceSubtitleViewModel) + 1,
				newSubtitle);
			this.UpdateBoxes();
			this.UpdateButtonVisibility();
			this.UpdateWarningVisibility();
		}

		public void RemoveSrtSubtitle(SrtSubtitleViewModel subtitleViewModel)
		{
			this.SrtSubtitles.Remove(subtitleViewModel);
			this.UpdateWarningVisibility();
		}

		public bool HasMultipleSourceTracks(int trackNumber)
		{
			return this.SourceSubtitles.Count(s => s.TrackNumber == trackNumber) > 1;
		}

		public void ReportDefault(ViewModelBase viewModel)
		{
			foreach (SourceSubtitleViewModel sourceVM in this.SourceSubtitles)
			{
				if (sourceVM != viewModel)
				{
					sourceVM.Default = false;
				}
			}

			foreach (SrtSubtitleViewModel srtVM in this.SrtSubtitles)
			{
				if (srtVM != viewModel)
				{
					srtVM.Default = false;
				}
			}
		}

		public void ReportBurned(SourceSubtitleViewModel subtitleViewModel)
		{
			foreach (SourceSubtitleViewModel sourceVM in this.SourceSubtitles)
			{
				if (sourceVM != subtitleViewModel)
				{
					sourceVM.BurnedIn = false;
				}
			}

			foreach (SrtSubtitleViewModel srtVM in this.SrtSubtitles)
			{
				srtVM.BurnedIn = false;
			}
		}

		public void ReportBurned(SrtSubtitleViewModel subtitleViewModel)
		{
			foreach (SourceSubtitleViewModel sourceVM in this.SourceSubtitles)
			{
				sourceVM.BurnedIn = false;
			}

			foreach (SrtSubtitleViewModel srtVM in this.SrtSubtitles)
			{
				if (srtVM != subtitleViewModel)
				{
					srtVM.BurnedIn = false;
				}
			}
		}

		// Update state of checked boxes after a change
		public void UpdateBoxes(SourceSubtitleViewModel updatedSubtitle = null)
		{
			this.DeselectDefaults();

			if (updatedSubtitle != null && updatedSubtitle.Selected)
			{
				if (!updatedSubtitle.CanPass)
				{
					// If we just selected a burn-in only subtitle, deselect all other subtitles.
					foreach (SourceSubtitleViewModel sourceSub in this.SourceSubtitles)
					{
						if (sourceSub != updatedSubtitle)
						{
							sourceSub.Deselect();
						}
					}
				}
				else
				{
					// We selected a soft subtitle. Deselect any burn-in-only subtitles.
					foreach (SourceSubtitleViewModel sourceSub in this.SourceSubtitles)
					{
						if (!sourceSub.CanPass)
						{
							sourceSub.Deselect();
						}
					}
				}
			}
		}

		private void DeselectDefaults()
		{
			bool anyBurned = this.SourceSubtitles.Any(sourceSub => sourceSub.BurnedIn) || this.SrtSubtitles.Any(sourceSub => sourceSub.BurnedIn);
			this.DefaultsEnabled = !anyBurned;

			if (!this.DefaultsEnabled)
			{
				foreach (SourceSubtitleViewModel sourceVM in this.SourceSubtitles)
				{
					sourceVM.Default = false;
				}

				foreach (SrtSubtitleViewModel srtVM in this.SrtSubtitles)
				{
					srtVM.Default = false;
				}
			}
		}

		public void UpdateWarningVisibility()
		{
			bool textSubtitleVisible = false;
			VCProfile profile = this.presetsViewModel.SelectedPreset.Preset.EncodingProfile;
			HBContainer profileContainer = HandBrakeEncoderHelpers.GetContainer(profile.ContainerName);
			if (profileContainer.DefaultExtension == "mp4" && profile.PreferredExtension == VCOutputExtension.Mp4)
			{
				foreach (SourceSubtitleViewModel sourceVM in this.SourceSubtitles)
				{
					if (sourceVM.Selected && sourceVM.SubtitleName.Contains("(Text)"))
					{
						textSubtitleVisible = true;
						break;
					}
				}
			}

			this.TextSubtitleWarningVisibile = textSubtitleVisible;

			bool anyBurned = false;
			int totalTracks = 0;
			foreach (SourceSubtitleViewModel sourceVM in this.SourceSubtitles)
			{
				if (sourceVM.Selected)
				{
					totalTracks++;
					if (sourceVM.BurnedIn)
					{
						anyBurned = true;
					}
				}
			}

			foreach (SrtSubtitleViewModel srtVM in this.SrtSubtitles)
			{
				totalTracks++;
				if (srtVM.BurnedIn)
				{
					anyBurned = true;
				}
			}

			this.BurnedOverlapWarningVisible = anyBurned && totalTracks > 1;
		}

		private void UpdateButtonVisibility()
		{
			foreach (SourceSubtitleViewModel sourceVM in this.SourceSubtitles)
			{
				sourceVM.UpdateButtonVisiblity();
			}
		}

		public SourceSubtitleTrack GetSubtitle(SourceSubtitleViewModel sourceSubtitleViewModel)
		{
			return this.mainViewModel.SelectedTitle.SubtitleList[sourceSubtitleViewModel.TrackNumber - 1];
		}
	}
}
