using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Input;
using System.Collections.ObjectModel;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;
using HandBrake.Interop.Model;
using HandBrake.Interop.Model.Encoding;
using HandBrake.Interop.SourceData;
using System.IO;
using Microsoft.Practices.Unity;
using VidCoder.Messages;
using VidCoder.Properties;
using VidCoder.Services;
using VidCoder.ViewModel.Components;

namespace VidCoder.ViewModel
{
	public class SubtitleDialogViewModel : OkCancelDialogViewModel
	{
		private ObservableCollection<SourceSubtitleViewModel> sourceSubtitles;
		private ObservableCollection<SrtSubtitleViewModel> srtSubtitles;

		private Container outputFormat;
		private bool defaultsEnabled;

		private bool textSubtitleWarningVisible;
		private bool burnedOverlapWarningVisible;

		private MainViewModel mainViewModel = Unity.Container.Resolve<MainViewModel>();
		private PresetsViewModel presetsViewModel = Unity.Container.Resolve<PresetsViewModel>();

		public SubtitleDialogViewModel(Subtitles currentSubtitles)
		{
			this.outputFormat = this.presetsViewModel.SelectedPreset.Preset.EncodingProfile.OutputFormat;

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
			foreach (Subtitle subtitle in this.mainViewModel.SelectedTitle.Subtitles)
			{
				if (!currentSubtitles.SourceSubtitles.Any(s => s.TrackNumber == subtitle.TrackNumber))
				{
					var newSubtitle = new SourceSubtitle
					    {
					        TrackNumber = subtitle.TrackNumber,
					        Default = false,
					        Forced = false,
					        BurnedIn = false
					    };

					this.sourceSubtitles.Add(new SourceSubtitleViewModel(this, newSubtitle));
				}
			}

			this.sourceSubtitles.CollectionChanged += this.SourceCollectionChanged;
			this.srtSubtitles.CollectionChanged += this.SrtCollectionChanged;

			//this.inputTrackChoices = new List<string>();
			//this.inputTrackChoices.Add("Foreign Audio Search (Bitmap)");

			//foreach (Subtitle subtitle in this.mainViewModel.SelectedTitle.Subtitles)
			//{
			//    this.inputTrackChoices.Add(subtitle.ToString());
			//}

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

		//public List<string> InputTrackChoices
		//{
		//    get { return this.inputTrackChoices; }
		//}

		public ObservableCollection<SourceSubtitleViewModel> SourceSubtitles
		{
			get { return this.sourceSubtitles; }
		}

		public ObservableCollection<SrtSubtitleViewModel> SrtSubtitles
		{
			get { return this.srtSubtitles; }
		}

		public Subtitles ChosenSubtitles
		{
			get
			{
				Subtitles subtitlesModel = new Subtitles();

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

		//private RelayCommand addSourceSubtitleCommand;
		//public RelayCommand AddSourceSubtitleCommand
		//{
		//    get
		//    {
		//        return this.addSourceSubtitleCommand ?? (this.addSourceSubtitleCommand = new RelayCommand(() =>
		//            {
		//                var newSubtitle = new SourceSubtitle
		//                {
		//                    TrackNumber = this.GetFirstUnusedTrack(),
		//                    Default = false,
		//                    Forced = false,
		//                    BurnedIn = false
		//                };

		//                this.sourceSubtitles.Add(new SourceSubtitleViewModel(this, newSubtitle));
		//                this.UpdateBoxes();
		//                this.UpdateWarningVisibility();
		//            }, () =>
		//            {
		//                // Disallow adding if there are no subtitles on the current title.
		//                if (this.mainViewModel.SelectedTitle == null || this.mainViewModel.SelectedTitle.Subtitles.Count == 0)
		//                {
		//                    return false;
		//                }

		//                return true;
		//            }));
		//    }
		//}

		private RelayCommand addSrtSubtitleCommand;
		public RelayCommand AddSrtSubtitleCommand
		{
			get
			{
				return this.addSrtSubtitleCommand ?? (this.addSrtSubtitleCommand = new RelayCommand(() =>
					{
						string srtFile = FileService.Instance.GetFileNameLoad(
							Settings.Default.RememberPreviousFiles ? Settings.Default.LastSrtFolder : null,
							"Add subtitles file",
							"srt",
							"SRT Files |*.srt");

						if (srtFile != null)
						{
							if (Settings.Default.RememberPreviousFiles)
							{
								Settings.Default.LastSrtFolder = Path.GetDirectoryName(srtFile);
								Settings.Default.Save();
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
				if (this.mainViewModel.SelectedTitle == null || this.mainViewModel.SelectedTitle.Subtitles.Count == 0)
				{
					return "No subtitles on the source video.";
				}

				return null;
			}
		}

		public Container OutputFormat
		{
			get
			{
				return this.outputFormat;
			}
		}

		public void RemoveSourceSubtitle(SourceSubtitleViewModel subtitleViewModel)
		{
			this.SourceSubtitles.Remove(subtitleViewModel);
			this.UpdateBoxes();
			this.UpdateWarningVisibility();
		}

		public void RemoveSrtSubtitle(SrtSubtitleViewModel subtitleViewModel)
		{
			this.SrtSubtitles.Remove(subtitleViewModel);
			this.UpdateWarningVisibility();
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
		}

		// If any tracks are burned in, disable and uncheck any "default" boxes
		public void UpdateBoxes()
		{
			bool anyBurned = this.SourceSubtitles.Any(sourceSub => sourceSub.BurnedIn);
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
			EncodingProfile profile = this.presetsViewModel.SelectedPreset.Preset.EncodingProfile;
			if (profile.OutputFormat == Container.Mp4 && profile.PreferredExtension == OutputExtension.Mp4)
			{
				foreach (SourceSubtitleViewModel sourceVM in this.SourceSubtitles)
				{
					if (sourceVM.SubtitleName.Contains("(Text)"))
					{
						textSubtitleVisible = true;
						break;
					}
				}
			}

			this.TextSubtitleWarningVisibile = textSubtitleVisible;

			bool anyBurned = false;
			foreach (SourceSubtitleViewModel sourceVM in this.SourceSubtitles)
			{
				if (sourceVM.BurnedIn)
				{
					anyBurned = true;
					break;
				}
			}

			int totalTracks = this.SourceSubtitles.Count + this.SrtSubtitles.Count;

			this.BurnedOverlapWarningVisible = anyBurned && totalTracks > 1;
		}

		//private int GetFirstUnusedTrack()
		//{
		//    List<int> unusedTracks = new List<int>();

		//    for (int i = 0; i < this.InputTrackChoices.Count; i++)
		//    {
		//        unusedTracks.Add(i);
		//    }

		//    foreach (SourceSubtitleViewModel sourceSubtitleVM in this.sourceSubtitles)
		//    {
		//        if (unusedTracks.Contains(sourceSubtitleVM.TrackNumber))
		//        {
		//            unusedTracks.Remove(sourceSubtitleVM.TrackNumber);
		//        }
		//    }

		//    if (unusedTracks.Count > 0)
		//    {
		//        return unusedTracks[0];
		//    }

		//    return 0;
		//}
	}
}
