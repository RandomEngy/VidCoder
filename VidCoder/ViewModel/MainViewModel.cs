using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.ObjectModel;
using System.Windows;
using System.IO;
using System.Windows.Input;
using System.Windows.Shell;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;
using HandBrake.Interop;
using HandBrake.Interop.Model;
using HandBrake.Interop.Model.Encoding;
using HandBrake.Interop.SourceData;
using VidCoder.Messages;
using VidCoder.Model;
using VidCoder.Properties;
using VidCoder.Services;
using Microsoft.Practices.Unity;
using VidCoder.ViewModel.Components;

namespace VidCoder.ViewModel
{
	public class MainViewModel : ViewModelBase
	{
		private const double SecondsRangeBuffer = 0.25;

		private HandBrakeInstance scanInstance;

		private IUpdater updater = Unity.Container.Resolve<IUpdater>();
		private ILogger logger = Unity.Container.Resolve<ILogger>();
		private OutputPathViewModel outputPathVM;
		private PresetsViewModel presetsVM;
		private ProcessingViewModel processingVM;
		private WindowManagerViewModel windowManagerVM;

		private ObservableCollection<SourceOptionViewModel> sourceOptions;
		private ObservableCollection<SourceOptionViewModel> recentSourceOptions; 
		private bool sourceSelectionExpanded;
		private SourceOption selectedSource;

		private Title selectedTitle;
		private Title oldTitle;
		private List<int> angles;
		private int angle;

		private VideoRangeType rangeType;
		private double secondsRangeStart;
		private double secondsRangeEnd;
		private double secondsBaseline;
		private int framesRangeStart;
		private int framesRangeEnd;
		private int framesBaseline;
		private List<ChapterViewModel> startChapters;
		private List<ChapterViewModel> endChapters;
		private ChapterViewModel selectedStartChapter;
		private ChapterViewModel selectedEndChapter;

		private ObservableCollection<AudioChoiceViewModel> audioChoices;

		private Subtitles currentSubtitles;

		private bool useDefaultChapterNames;
		private List<string> customChapterNames;

		// sourceData is populated when a scan has finished and cleared out when a new scan starts.
		private VideoSource sourceData;

		private IDriveService driveService;
		private IList<DriveInformation> driveCollection;
		private bool scanningSource;
		private bool scanError;
		private double scanProgress;
		private bool scanCancelledFlag;

		private bool showTrayIcon;

		public event EventHandler<EventArgs<string>> AnimationStarted;
		public event EventHandler ScanCancelled;

		public MainViewModel()
		{
			Unity.Container.RegisterInstance(this);

			this.outputPathVM = Unity.Container.Resolve<OutputPathViewModel>();
			this.processingVM = Unity.Container.Resolve<ProcessingViewModel>();
			this.presetsVM = Unity.Container.Resolve<PresetsViewModel>();
			this.windowManagerVM = Unity.Container.Resolve<WindowManagerViewModel>();

			updater.CheckUpdates();

			this.JobCreationAvailable = false;

			this.sourceOptions = new ObservableCollection<SourceOptionViewModel>
			{
				new SourceOptionViewModel(new SourceOption { Type = SourceType.File }),
				new SourceOptionViewModel(new SourceOption { Type = SourceType.VideoFolder })
			};

			this.recentSourceOptions = new ObservableCollection<SourceOptionViewModel>();

			List<string> sourceHistory = SourceHistory.GetHistory();
			foreach (string recentSourcePath in sourceHistory)
			{
				if (File.Exists(recentSourcePath))
				{
					this.recentSourceOptions.Add(new SourceOptionViewModel(new SourceOption { Type = SourceType.File }, recentSourcePath));
				}
				else if (Directory.Exists(recentSourcePath))
				{
					this.recentSourceOptions.Add(new SourceOptionViewModel(new SourceOption { Type = SourceType.VideoFolder }, recentSourcePath));
				}
			}

			this.useDefaultChapterNames = true;

			this.driveService = Unity.Container.Resolve<IDriveService>();

			this.DriveCollection = this.driveService.GetDiscInformation();

			this.audioChoices = new ObservableCollection<AudioChoiceViewModel>();
			this.audioChoices.CollectionChanged += (o, e) => { Messenger.Default.Send(new AudioInputChangedMessage()); };

			Messenger.Default.Register<SelectedTitleChangedMessage>(
				this,
				message =>
					{
						DispatchService.BeginInvoke(() => { this.AddTrackCommand.RaiseCanExecuteChanged(); });
					});

			Messenger.Default.Register<HighlightedChapterChangedMessage>(
				this,
				message =>
					{
						this.RefreshRangePreview();
					});

			Messenger.Default.Register<RangeFocusMessage>(this, this.OnRangeControlGotFocus);
		}

		public HandBrakeInstance ScanInstance
		{
			get
			{
				return this.scanInstance;
			}
		}

		public void UpdateDriveCollection()
		{
			// Get new set of drives
			IList<DriveInformation> newDriveCollection = this.driveService.GetDiscInformation();

			// If our currently selected source is a DVD drive, see if it needs to be updated
			if (this.SelectedSource != null && this.SelectedSource.Type == SourceType.Dvd)
			{
				string currentRootDirectory = this.SelectedSource.DriveInfo.RootDirectory;
				bool currentDrivePresent = newDriveCollection.Any(driveInfo => driveInfo.RootDirectory == currentRootDirectory);

				if (this.SelectedSource.DriveInfo.Empty && currentDrivePresent)
				{
					// If we were empty and the drive is now here, update and scan it
					this.SelectedSource.DriveInfo.Empty = false;
					this.SelectedSource.DriveInfo.VolumeLabel = newDriveCollection.Single(driveInfo => driveInfo.RootDirectory == currentRootDirectory).VolumeLabel;
					this.SetSourceFromDvd(this.SelectedSource.DriveInfo);
					this.RaisePropertyChanged(() => this.HasVideoSource);
					this.RaisePropertyChanged(() => this.SourceText);
				}
				else if (!this.SelectedSource.DriveInfo.Empty && !currentDrivePresent)
				{
					// If we had an item and the drive is now gone, update and remove the video source.
					DriveInformation emptyDrive = this.SelectedSource.DriveInfo;
					emptyDrive.Empty = true;

					this.sourceData = null;
					this.SelectedTitle = null;
					this.RaisePropertyChanged(() => this.HasVideoSource);
					this.RaisePropertyChanged(() => this.SourceText);
				}
			}

			this.DriveCollection = newDriveCollection;
		}

		public bool SetSourceFromFile()
		{
			if (this.SourceSelectionExpanded)
			{
				this.SourceSelectionExpanded = false;
			}

			string videoFile = FileService.Instance.GetFileNameLoad(Settings.Default.LastInputFileFolder, "Load video file");

			if (videoFile != null)
			{
				this.SetSourceFromFile(videoFile);
				return true;
			}

			return false;
		}

		public void SetSourceFromFile(string videoFile)
		{
			Settings.Default.LastInputFileFolder = Path.GetDirectoryName(videoFile);
			Settings.Default.Save();

			this.SourceName = Utilities.GetSourceNameFile(videoFile);
			this.StartScan(videoFile);

			this.SourcePath = videoFile;
			this.SelectedSource = new SourceOption { Type = SourceType.File };
		}

		public bool SetSourceFromFolder()
		{
			if (this.SourceSelectionExpanded)
			{
				this.SourceSelectionExpanded = false;
			}

			string folderPath = FileService.Instance.GetFolderName(Settings.Default.LastVideoTSFolder, "Pick the DVD's VIDEO_TS folder or the Blu-ray's root folder.");

			// Make sure we get focus back after displaying the dialog.
			WindowManager.FocusWindow(this);

			if (folderPath != null)
			{
				this.SetSourceFromFolder(folderPath);
				return true;
			}

			return false;
		}

		public void SetSourceFromFolder(string videoFolder)
		{
			Settings.Default.LastVideoTSFolder = videoFolder;
			Settings.Default.Save();
			this.SourceName = Utilities.GetSourceNameFolder(videoFolder);

			this.StartScan(videoFolder);

			this.SourcePath = videoFolder;
			this.SelectedSource = new SourceOption { Type = SourceType.VideoFolder };
		}

		public bool SetSourceFromDvd(DriveInformation driveInfo)
		{
			if (driveInfo.Empty)
			{
				return false;
			}

			if (this.SourceSelectionExpanded)
			{
				this.SourceSelectionExpanded = false;
			}

			this.SourceName = driveInfo.VolumeLabel;

			if (driveInfo.DiscType == DiscType.Dvd)
			{
				this.SourcePath = Path.Combine(driveInfo.RootDirectory, "VIDEO_TS");
			}
			else
			{
				this.SourcePath = driveInfo.RootDirectory;
			}

			this.SelectedSource = new SourceOption { Type = SourceType.Dvd, DriveInfo = driveInfo };
			this.StartScan(this.SourcePath);

			return true;
		}

		public void OnLoaded()
		{
			bool windowOpened = false;

			if (Settings.Default.EncodingWindowOpen)
			{
				this.WindowManagerVM.OpenEncodingWindow();
				windowOpened = true;
			}

			if (Settings.Default.PreviewWindowOpen)
			{
				this.WindowManagerVM.OpenPreviewWindow();
				windowOpened = true;
			}

			if (Settings.Default.LogWindowOpen)
			{
				this.WindowManagerVM.OpenLogWindow();
				windowOpened = true;
			}

			if (windowOpened)
			{
				WindowManager.FocusWindow(this);
			}
		}

		/// <summary>
		/// Returns true if we can close.
		/// </summary>
		/// <returns>True if the form can close.</returns>
		public bool OnClosing()
		{
			if (this.processingVM.Encoding)
			{
				MessageBoxResult result = Utilities.MessageBox.Show(
					this,
					"There are encodes in progress. Are you sure you want to quit?",
					"Encodes in progress",
					MessageBoxButton.YesNo,
					MessageBoxImage.Warning);

				if (result == MessageBoxResult.No)
				{
					return false;
				}
			}

			// If we're quitting, see if the encode is still going.
			if (this.processingVM.Encoding)
			{
				// If so, stop it.
				this.processingVM.CurrentJob.HandBrakeInstance.StopEncode();
			}

			ViewModelBase encodingWindow = WindowManager.FindWindow(typeof(EncodingViewModel));
			if (encodingWindow != null)
			{
				WindowManager.Close(encodingWindow);
			}

			ViewModelBase previewWindow = WindowManager.FindWindow(typeof(PreviewViewModel));
			if (previewWindow != null)
			{
				WindowManager.Close(previewWindow);
			}

			ViewModelBase logWindow = WindowManager.FindWindow(typeof(LogViewModel));
			if (logWindow != null)
			{
				WindowManager.Close(logWindow);
			}

			Settings.Default.EncodingWindowOpen = encodingWindow != null;
			Settings.Default.PreviewWindowOpen = previewWindow != null;
			Settings.Default.LogWindowOpen = logWindow != null;
			Settings.Default.Save();

			this.driveService.Close();
			this.ProcessingVM.CleanupHandBrakeInstances();
			HandBrakeInstance.DisposeGlobal();

			FileCleanup.CleanOldLogs();
			FileCleanup.CleanPreviewFileCache();

			this.updater.PromptToApplyUpdate();

			this.logger.Dispose();

			return true;
		}

		public string SourcePath { get; private set; }

		public string SourceName { get; private set; }

		public OutputPathViewModel OutputPathVM
		{
			get
			{
				return this.outputPathVM;
			}
		}

		public PresetsViewModel PresetsVM
		{
			get
			{
				return this.presetsVM;
			}
		}

		public ProcessingViewModel ProcessingVM
		{
			get
			{
				return this.processingVM;
			}
		}

		public WindowManagerViewModel WindowManagerVM
		{
			get
			{
				return this.windowManagerVM;
			}
		}

		public IList<DriveInformation> DriveCollection
		{
			get
			{
				return this.driveCollection;
			}

			set
			{
				this.driveCollection = value;
				this.UpdateDrives();
			}
		}

		public ObservableCollection<SourceOptionViewModel> SourceOptions
		{
			get
			{
				return this.sourceOptions;
			}
		}

		public ObservableCollection<SourceOptionViewModel> RecentSourceOptions
		{
			get
			{
				return this.recentSourceOptions;
			}
		} 

		public bool RecentSourcesVisible
		{
			get
			{
				return this.RecentSourceOptions.Count > 0;
			}
		}

		public bool SourceSelectionExpanded
		{
			get
			{
				return this.sourceSelectionExpanded;
			}

			set
			{
				this.sourceSelectionExpanded = value;
				this.RaisePropertyChanged(() => this.SourceSelectionExpanded);
				this.RaisePropertyChanged(() => this.SourceOptionsVisible);
			}
		}

		public bool SourceOptionsVisible
		{
			get
			{
				if (this.ScanningSource)
				{
					return false;
				}

				if (this.ScanError)
				{
					return false;
				}

				if (this.SelectedSource != null && this.SelectedSource.Type == SourceType.Dvd && this.SelectedSource.DriveInfo.Empty)
				{
					return false;
				}

				return this.sourceData == null;
			}
		}

		public bool SourcePicked
		{
			get
			{
				return this.sourceData != null || this.SelectedSource != null;
			}
		}

		public string SourceIcon
		{
			get
			{
				if (this.SelectedSource == null)
				{
					return null;
				}

				switch (this.SelectedSource.Type)
				{
					case SourceType.File:
						return "/Icons/video-file.png";
					case SourceType.VideoFolder:
						return "/Icons/folder.png";
					case SourceType.Dvd:
						if (this.SelectedSource.DriveInfo.DiscType == DiscType.Dvd)
						{
							return "/Icons/disc.png";
						}
						else
						{
							return "/Icons/bludisc.png";
						}
					default:
						break;
				}

				return null;
			}
		}

		public string SourceText
		{
			get
			{
				if (this.SelectedSource == null)
				{
					return string.Empty;
				}

				switch (this.SelectedSource.Type)
				{
					case SourceType.File:
						return this.SourcePath;
					case SourceType.VideoFolder:
						return this.SourcePath;
					case SourceType.Dvd:
						return this.SelectedSource.DriveInfo.DisplayText;
					default:
						break;
				}

				return string.Empty;
			}
		}

		public SourceOption SelectedSource
		{
			get
			{
				return this.selectedSource;
			}

			set
			{
				this.selectedSource = value;
				this.RaisePropertyChanged(() => this.SourcePicked);
				this.RaisePropertyChanged(() => this.SourceIcon);
				this.RaisePropertyChanged(() => this.SourceText);
			}
		}

		public bool ScanningSource
		{
			get
			{
				return this.scanningSource;
			}

			set
			{
				this.scanningSource = value;
				this.RaisePropertyChanged(() => this.SourceOptionsVisible);
				this.RaisePropertyChanged(() => this.HasVideoSource);
				this.RaisePropertyChanged(() => this.ScanningSource);

				this.ToggleSourceMenuCommand.RaiseCanExecuteChanged();
				this.OpenFileCommand.RaiseCanExecuteChanged();
				this.OpenFolderCommand.RaiseCanExecuteChanged();

				Messenger.Default.Send(new ScanningChangedMessage { Scanning = value });
			}
		}

		public bool ScanError
		{
			get
			{
				return this.scanError;
			}

			set
			{
				this.scanError = value;
				this.RaisePropertyChanged(() => this.ScanError);
				this.RaisePropertyChanged(() => this.SourceOptionsVisible);
			}
		}

		public double ScanProgress
		{
			get
			{
				return this.scanProgress;
			}

			set
			{
				this.scanProgress = value;
				this.RaisePropertyChanged(() => this.ScanProgress);
			}
		}

		public bool HasVideoSource
		{
			get
			{
				if (this.SelectedSource == null)
				{
					return false;
				}

				if (this.SelectedSource.Type == SourceType.Dvd && this.SelectedSource.DriveInfo.Empty)
				{
					return false;
				}

				if (this.ScanningSource)
				{
					return false;
				}

				if (this.sourceData == null)
				{
					return false;
				}

				if (this.sourceData.Titles.Count == 0)
				{
					return false;
				}

				return true;
			}
		}

		/// <summary>
		/// If true, EncodeJob objects can be safely obtained from this class.
		/// </summary>
		public bool JobCreationAvailable { get; set; }

		public List<Title> Titles
		{
			get
			{
				if (this.sourceData != null)
				{
					return this.sourceData.Titles;
				}

				return null;
			}
		}

		public Title SelectedTitle
		{
			get
			{
				return this.selectedTitle;
			}

			set
			{
				if (value == null)
				{
					this.selectedTitle = null;
				}
				else
				{
					this.JobCreationAvailable = false;

					// Save old title, transfer from it when disc returns.
					this.selectedTitle = value;

					// Re-enable auto-naming when switching titles.
					if (this.oldTitle != null)
					{
						this.OutputPathVM.ManualOutputPath = false;
					}

					// Keep audio/subtitle choices if they match up in index and language.
					Subtitles oldSubtitles = this.CurrentSubtitles;
					this.CurrentSubtitles = new Subtitles { SourceSubtitles = new List<SourceSubtitle>(), SrtSubtitles = new List<SrtSubtitle>() };

					if (this.oldTitle != null)
					{
						var keptAudioChoices = new List<AudioChoiceViewModel>();
						foreach (AudioChoiceViewModel audioChoiceVM in this.AudioChoices)
						{
							if (audioChoiceVM.SelectedIndex < this.selectedTitle.AudioTracks.Count &&
								this.oldTitle.AudioTracks[audioChoiceVM.SelectedIndex].Language == this.selectedTitle.AudioTracks[audioChoiceVM.SelectedIndex].Language)
							{
								keptAudioChoices.Add(audioChoiceVM);
							}
						}

						this.AudioChoices.Clear();
						foreach (AudioChoiceViewModel audioChoiceVM in keptAudioChoices)
						{
							this.AudioChoices.Add(audioChoiceVM);
						}

						if (oldSubtitles != null)
						{
							if (this.selectedTitle.Subtitles.Count > 0)
							{
								// Keep source subtitles when changing title, but not specific SRT files.
								var keptSourceSubtitles = new List<SourceSubtitle>();
								foreach (SourceSubtitle sourceSubtitle in oldSubtitles.SourceSubtitles)
								{
									if (sourceSubtitle.TrackNumber == 0)
									{
										keptSourceSubtitles.Add(sourceSubtitle);
									}
									else if (sourceSubtitle.TrackNumber - 1 < this.selectedTitle.Subtitles.Count &&
										this.oldTitle.Subtitles[sourceSubtitle.TrackNumber - 1].LanguageCode == this.selectedTitle.Subtitles[sourceSubtitle.TrackNumber - 1].LanguageCode)
									{
										keptSourceSubtitles.Add(sourceSubtitle);
									}
								}

								foreach (SourceSubtitle sourceSubtitle in keptSourceSubtitles)
								{
									this.CurrentSubtitles.SourceSubtitles.Add(sourceSubtitle);
								}
							}
						}
					}
					else
					{
						this.AudioChoices.Clear();
					}

					// If we have a preferred language specified, try to apply that language
					if (Settings.Default.NativeLanguageCode != "und")
					{
						// Only mess with the audio tracks if we're in dub mode and we're not carrying over previous selections
						if (Settings.Default.DubAudio && (this.AudioChoices.Count == 0 || this.AudioChoices.Count == 1 && this.AudioChoices[0].SelectedIndex == 0))
						{
							List<AudioTrack> nativeTracks = this.selectedTitle.AudioTracks.Where(track => track.LanguageCode == Settings.Default.NativeLanguageCode).ToList();
							if (nativeTracks.Count > 0)
							{
								this.AudioChoices.Clear();
								var newVM = new AudioChoiceViewModel();
								newVM.SelectedIndex = nativeTracks[0].TrackNumber - 1;
								this.AudioChoices.Add(newVM);
							}
						}

						// Try to apply the preferred language subtitle if no subtitles already exist
						if (this.CurrentSubtitles.SourceSubtitles.Count == 0 && this.CurrentSubtitles.SrtSubtitles.Count == 0)
						{
							List<Subtitle> nativeSubtitles = this.selectedTitle.Subtitles.Where(subtitle => subtitle.LanguageCode == "iso639-2: " + Settings.Default.NativeLanguageCode).ToList();
							if (nativeSubtitles.Count > 0)
							{
								this.CurrentSubtitles.SourceSubtitles.Add(new SourceSubtitle
								{
									BurnedIn = false,
									Default = true,
									Forced = false,
									TrackNumber = nativeSubtitles[0].TrackNumber
								});
							}
						}
					}

					if (this.selectedTitle.AudioTracks.Count > 0 && this.AudioChoices.Count == 0)
					{
						var newVM = new AudioChoiceViewModel();
						newVM.SelectedIndex = 0;
						this.AudioChoices.Add(newVM);
					}

					this.UseDefaultChapterNames = true;
					this.PopulateChapterSelectLists();

					// Soft update so as not to trigger range checking logic.
					this.selectedStartChapter = this.StartChapters[0];
					this.selectedEndChapter = this.EndChapters[this.EndChapters.Count - 1];
					this.RaisePropertyChanged(() => this.SelectedStartChapter);
					this.RaisePropertyChanged(() => this.SelectedEndChapter);

					this.secondsRangeStart = 0;
					this.secondsRangeEnd = this.selectedTitle.Duration.TotalSeconds;
					this.RaisePropertyChanged(() => this.SecondsRangeStart);
					this.RaisePropertyChanged(() => this.SecondsRangeEnd);

					this.framesRangeStart = 0;
					this.framesRangeEnd = this.selectedTitle.Frames;
					this.RaisePropertyChanged(() => this.FramesRangeStart);
					this.RaisePropertyChanged(() => this.FramesRangeEnd);

					this.PopulateAnglesList();

					this.angle = 1;

					this.RaisePropertyChanged(() => this.Angles);
					this.RaisePropertyChanged(() => this.Angle);
					this.RaisePropertyChanged(() => this.AngleVisible);
					this.RaisePropertyChanged(() => this.TotalChaptersText);

					this.oldTitle = value;

					this.JobCreationAvailable = true;
				}

				Messenger.Default.Send(new RefreshPreviewMessage());

				// Custom chapter names are thrown out when switching titles.
				this.CustomChapterNames = null;

				this.RaisePropertyChanged(() => this.SubtitlesSummary);
				Messenger.Default.Send(new SelectedTitleChangedMessage());

				this.OutputPathVM.GenerateOutputFileName();

				this.RaisePropertyChanged(() => this.StartChapters);
				this.RaisePropertyChanged(() => this.EndChapters);
				this.RaisePropertyChanged(() => this.SelectedTitle);

				this.RefreshRangePreview();
			}
		}

		public List<int> Angles
		{
			get
			{
				return this.angles;
			}
		}

		public bool AngleVisible
		{
			get
			{
				return this.SelectedTitle != null && this.SelectedTitle.AngleCount > 1;
			}
		}

		public int Angle
		{
			get
			{
				return this.angle;
			}

			set
			{
				this.angle = value;

				Messenger.Default.Send(new RefreshPreviewMessage());
				this.RaisePropertyChanged(() => this.Angle);
			}
		}

		public Subtitles CurrentSubtitles
		{
			get
			{
				return this.currentSubtitles;
			}

			set
			{
				this.currentSubtitles = value;
				this.RaisePropertyChanged(() => this.CurrentSubtitles);
				this.RaisePropertyChanged(() => this.SubtitlesSummary);
			}
		}

		public string SubtitlesSummary
		{
			get
			{
				if (!this.HasVideoSource || this.CurrentSubtitles == null || (this.CurrentSubtitles.SourceSubtitles.Count == 0 && this.CurrentSubtitles.SrtSubtitles.Count == 0))
				{
					return "(None)";
				}

				List<string> trackSummaries = new List<string>();

				foreach (SourceSubtitle sourceSubtitle in this.CurrentSubtitles.SourceSubtitles)
				{
					if (sourceSubtitle.TrackNumber == 0)
					{
						trackSummaries.Add("Foriegn Audio Search");
					}
					else
					{
						trackSummaries.Add(this.SelectedTitle.Subtitles[sourceSubtitle.TrackNumber - 1].Language);
					}
				}

				foreach (SrtSubtitle srtSubtitle in this.currentSubtitles.SrtSubtitles)
				{
					trackSummaries.Add(Language.Decode(srtSubtitle.LanguageCode));
				}

				if (trackSummaries.Count > 3)
				{
					return trackSummaries.Count + " tracks";
				}

				StringBuilder summaryBuilder = new StringBuilder();
				for (int i = 0; i < trackSummaries.Count; i++)
				{
					summaryBuilder.Append(trackSummaries[i]);

					if (i != trackSummaries.Count - 1)
					{
						summaryBuilder.Append(", ");
					}
				}

				return summaryBuilder.ToString();
			}
		}

		public bool UseDefaultChapterNames
		{
			get
			{
				return this.useDefaultChapterNames;
			}

			set
			{
				this.useDefaultChapterNames = value;
				this.RaisePropertyChanged(() => this.UseDefaultChapterNames);
				this.RaisePropertyChanged(() => this.ChapterMarkersSummary);
			}
		}

		public List<string> CustomChapterNames
		{
			get
			{
				return this.customChapterNames;
			}

			set
			{
				this.customChapterNames = value;
				this.RaisePropertyChanged(() => this.CustomChapterNames);
			}
		}

		public string ChapterMarkersSummary
		{
			get
			{
				if (this.UseDefaultChapterNames)
				{
					return "Default";
				}

				return "Custom";
			}
		}

		public bool ShowChapterMarkerUI
		{
			get
			{
				return this.presetsVM.SelectedPreset != null && this.presetsVM.SelectedPreset.Preset.EncodingProfile.IncludeChapterMarkers;
			}
		}

		public bool TitleVisible
		{
			get
			{
				return this.HasVideoSource && this.SourceData.Titles.Count > 1;
			}
		}

		public VideoRangeType RangeType
		{
			get
			{
				return this.rangeType;
			}

			set
			{
				this.rangeType = value;

				this.OutputPathVM.GenerateOutputFileName();
				this.RaisePropertyChanged(() => this.RangeType);
				this.RaisePropertyChanged(() => this.ChaptersRangeVisible);
				this.RaisePropertyChanged(() => this.SecondsRangeVisible);
				this.RaisePropertyChanged(() => this.FramesRangeVisible);
				this.RaisePropertyChanged(() => this.TotalChaptersText);
				this.RefreshRangePreview();
				this.ReportLengthChanged();
			}
		}

		public List<ChapterViewModel> StartChapters
		{
			get
			{
				return this.startChapters;
			}
		} 

		public List<ChapterViewModel> EndChapters
		{
			get
			{
				return this.endChapters;
			}
		} 

		public bool ChaptersRangeVisible
		{
			get
			{
				return this.RangeType == VideoRangeType.Chapters;
			}
		}

		public double SecondsRangeStart
		{
			get
			{
				return this.secondsRangeStart;
			}

			set
			{
				double maxSeconds = this.SelectedTitle.Duration.TotalSeconds;

				this.secondsRangeStart = value;
				if (this.secondsRangeStart > maxSeconds - SecondsRangeBuffer)
				{
					this.secondsRangeStart = maxSeconds - SecondsRangeBuffer;
				}

				// Constrain from the baseline: what the other end was when we got focus
				double idealSecondsEnd = this.secondsBaseline;
				if (idealSecondsEnd < this.secondsRangeStart + SecondsRangeBuffer)
				{
					idealSecondsEnd = this.secondsRangeStart + SecondsRangeBuffer;
				}

				if (idealSecondsEnd != this.secondsRangeEnd)
				{
					this.secondsRangeEnd = idealSecondsEnd;
					this.RaisePropertyChanged(() => this.SecondsRangeEnd);
				}

				this.OutputPathVM.GenerateOutputFileName();

				this.RaisePropertyChanged(() => this.SecondsRangeStart);
				this.RefreshRangePreview();
				this.ReportLengthChanged();
			}
		}

		public double SecondsRangeEnd
		{
			get
			{
				return this.secondsRangeEnd;
			}

			set
			{
				double maxSeconds = this.SelectedTitle.Duration.TotalSeconds;

				this.secondsRangeEnd = value;
				if (this.secondsRangeEnd > maxSeconds)
				{
					this.secondsRangeEnd = maxSeconds;
				}

				// Constrain from the baseline: what the other end was when we got focus
				double idealSecondsStart = this.secondsBaseline;
				if (idealSecondsStart > this.secondsRangeEnd - SecondsRangeBuffer)
				{
					idealSecondsStart = this.secondsRangeEnd - SecondsRangeBuffer;
				}

				if (idealSecondsStart != this.secondsRangeStart)
				{
					this.secondsRangeStart = idealSecondsStart;
					this.RaisePropertyChanged(() => this.SecondsRangeStart);
				}

				this.OutputPathVM.GenerateOutputFileName();

				this.RaisePropertyChanged(() => this.SecondsRangeEnd);
				this.RefreshRangePreview();
				this.ReportLengthChanged();
			}
		}

		public bool SecondsRangeVisible
		{
			get
			{
				return this.RangeType == VideoRangeType.Seconds;
			}
		}

		public int FramesRangeStart
		{
			get
			{
				return this.framesRangeStart;
			}

			set
			{
				int maxFrame = this.SelectedTitle.Frames;

				this.framesRangeStart = value;
				if (this.framesRangeStart >= maxFrame)
				{
					this.framesRangeStart = maxFrame - 1;
				}

				// Constrain from the baseline: what the other end was when we got focus
				int idealFramesEnd = this.framesBaseline;
				if (idealFramesEnd <= this.framesRangeStart)
				{
					idealFramesEnd = this.framesRangeStart + 1;
				}

				if (idealFramesEnd != this.framesRangeEnd)
				{
					this.framesRangeEnd = idealFramesEnd;
					this.RaisePropertyChanged(() => this.FramesRangeEnd);
				}

				this.OutputPathVM.GenerateOutputFileName();

				this.RaisePropertyChanged(() => this.FramesRangeStart);
				this.RefreshRangePreview();
				this.ReportLengthChanged();
			}
		}

		public int FramesRangeEnd
		{
			get
			{
				return this.framesRangeEnd;
			}

			set
			{
				int maxFrame = this.SelectedTitle.Frames;

				this.framesRangeEnd = value;
				if (this.framesRangeEnd > maxFrame)
				{
					this.framesRangeEnd = maxFrame;
				}

				// Constrain from the baseline: what the other end was when we got focus
				int idealFramesStart = this.framesBaseline;
				if (idealFramesStart >= this.framesRangeEnd)
				{
					idealFramesStart = this.framesRangeEnd - 1;
				}

				if (idealFramesStart != this.framesRangeStart)
				{
					this.framesRangeStart = idealFramesStart;
					this.RaisePropertyChanged(() => this.FramesRangeStart);
				}

				this.OutputPathVM.GenerateOutputFileName();

				this.RaisePropertyChanged(() => this.FramesRangeEnd);
				this.RefreshRangePreview();
				this.ReportLengthChanged();
			}
		}

		public bool FramesRangeVisible
		{
			get
			{
				return this.RangeType == VideoRangeType.Frames;
			}
		}

		public ChapterViewModel SelectedStartChapter
		{
			get
			{
				return this.selectedStartChapter;
			}

			set
			{
				this.selectedStartChapter = value;

				if (value == null)
				{
					return;
				}

				if (this.SelectedEndChapter != null && this.SelectedEndChapter.ChapterNumber < this.selectedStartChapter.ChapterNumber)
				{
					this.SelectedEndChapter = this.EndChapters.FirstOrDefault(c => c.ChapterNumber == this.selectedStartChapter.ChapterNumber);
				}

				this.OutputPathVM.GenerateOutputFileName();

				this.RaisePropertyChanged(() => this.SelectedStartChapter);
				this.RefreshRangePreview();
				this.ReportLengthChanged();
			}
		}

		public ChapterViewModel SelectedEndChapter
		{
			get
			{
				return this.selectedEndChapter;
			}

			set
			{
				this.selectedEndChapter = value;

				if (value == null)
				{
					return;
				}

				if (this.SelectedStartChapter != null && this.selectedEndChapter.ChapterNumber < this.SelectedStartChapter.ChapterNumber)
				{
					this.SelectedStartChapter = this.StartChapters.FirstOrDefault(c => c.ChapterNumber == this.selectedEndChapter.ChapterNumber);
				}

				this.OutputPathVM.GenerateOutputFileName();

				this.RaisePropertyChanged(() => this.SelectedEndChapter);
				this.RefreshRangePreview();
				this.ReportLengthChanged();
			}
		}

		public string TotalChaptersText
		{
			get
			{
				if (this.SelectedTitle != null && this.RangeType == VideoRangeType.Chapters)
				{
					return "of " + this.SelectedTitle.Chapters.Count;
				}

				return string.Empty;
			}
		}

		public TimeSpan RangePreviewStart
		{
			get
			{
				if (this.SelectedTitle == null)
				{
					return TimeSpan.Zero;
				}

				switch (this.RangeType)
				{
					case VideoRangeType.Chapters:
						if (this.SelectedStartChapter == null || this.SelectedEndChapter == null)
						{
							return TimeSpan.Zero;
						}

						int startChapter;
						ChapterViewModel highlightedStartChapter = this.StartChapters.FirstOrDefault(c => c.IsHighlighted);
						ChapterViewModel highlightedEndChapter = this.EndChapters.FirstOrDefault(c => c.IsHighlighted);

						if (highlightedStartChapter != null)
						{
							startChapter = highlightedStartChapter.ChapterNumber;
						}
						else
						{
							startChapter = this.SelectedStartChapter.ChapterNumber;
						}

						// If an end chapter is highlighted, automatically do adjustment of start chapter.
						if (highlightedEndChapter != null && highlightedEndChapter.ChapterNumber < startChapter)
						{
							startChapter = highlightedEndChapter.ChapterNumber;
						}

						if (startChapter == 1)
						{
							return TimeSpan.Zero;
						}

						return this.GetChapterRangeDuration(1, startChapter - 1);
					case VideoRangeType.Seconds:
						return TimeSpan.FromSeconds(this.SecondsRangeStart);
					case VideoRangeType.Frames:
						return TimeSpan.FromSeconds(this.FramesRangeStart / this.SelectedTitle.Framerate);
				}

				return TimeSpan.Zero;
			}
		}

		public TimeSpan RangePreviewEnd
		{
			get
			{
				if (this.SelectedTitle == null)
				{
					return TimeSpan.Zero;
				}

				switch (this.RangeType)
				{
					case VideoRangeType.Chapters:
						if (this.SelectedStartChapter == null || this.SelectedEndChapter == null)
						{
							return TimeSpan.Zero;
						}

						int endChapter;
						ChapterViewModel highlightedStartChapter = this.StartChapters.FirstOrDefault(c => c.IsHighlighted);
						ChapterViewModel highlightedEndChapter = this.EndChapters.FirstOrDefault(c => c.IsHighlighted);
						if (highlightedEndChapter != null)
						{
							endChapter = highlightedEndChapter.ChapterNumber;
						}
						else
						{
							endChapter = this.SelectedEndChapter.ChapterNumber;
						}

						// If a start chapter is highlighted, automatically do adjustment of end chapter
						if (highlightedStartChapter != null && highlightedStartChapter.ChapterNumber > endChapter)
						{
							endChapter = highlightedStartChapter.ChapterNumber;
						}

						return this.GetChapterRangeDuration(1, endChapter);
					case VideoRangeType.Seconds:
						return TimeSpan.FromSeconds(this.SecondsRangeEnd);
					case VideoRangeType.Frames:
						return TimeSpan.FromSeconds(this.FramesRangeEnd / this.SelectedTitle.Framerate);
				}

				return TimeSpan.Zero;
			}
		}

		public string RangePreviewText
		{
			get
			{
				return this.RangePreviewStart.ToString(Utilities.TimeFormat) + " - " + this.RangePreviewEnd.ToString(Utilities.TimeFormat);
			}
		}

		public string RangePreviewLengthText
		{
			get
			{
				TimeSpan duration = TimeSpan.Zero;
				TimeSpan start = this.RangePreviewStart;
				TimeSpan end = this.RangePreviewEnd;

				if (start < end)
				{
					duration = end - start;
				}

				return "Length: " + duration.ToString(Utilities.TimeFormat);
			}
		}

		public ObservableCollection<AudioChoiceViewModel> AudioChoices
		{
			get
			{
				return this.audioChoices;
			}
		}

		public List<AudioTrack> InputTracks
		{
			get
			{
				if (this.SourceData != null)
				{
					return this.SelectedTitle.AudioTracks;
				}

				return null;
			}
		}

		public VideoSource SourceData
		{
			get
			{
				return this.sourceData;
			}

			set
			{
				this.sourceData = value;
				this.UpdateFromNewVideoSource();
			}
		}

		public bool ShowTrayIcon
		{
			get
			{
				return this.showTrayIcon;
			}

			set
			{
				this.showTrayIcon = value;
				this.RaisePropertyChanged(() => this.ShowTrayIcon);
			}
		}

		public string TrayIconToolTip
		{
			get
			{
				return "VidCoder " + Utilities.CurrentVersion;
			}
		}

		private RelayCommand addTrackCommand;
		public RelayCommand AddTrackCommand
		{
			get
			{
				return this.addTrackCommand ?? (this.addTrackCommand = new RelayCommand(() =>
					{
						var newAudioChoice = new AudioChoiceViewModel();
						newAudioChoice.SelectedIndex = this.GetFirstUnusedAudioTrack();
						this.AudioChoices.Add(newAudioChoice);
					},
					() =>
					{
						if (this.SelectedTitle == null)
						{
							return true;
						}

						return this.SelectedTitle.AudioTracks.Count > 0;
					}));
			}
		}

		private RelayCommand toggleSourceMenuCommand;
		public RelayCommand ToggleSourceMenuCommand
		{
			get
			{
				return this.toggleSourceMenuCommand ?? (this.toggleSourceMenuCommand = new RelayCommand(() =>
					{
						this.SourceSelectionExpanded = !this.SourceSelectionExpanded;
					},
					() =>
					{
						return !this.ScanningSource;
					}));
			}
		}

		private RelayCommand openSubtitlesDialogCommand;
		public RelayCommand OpenSubtitlesDialogCommand
		{
			get
			{
				return this.openSubtitlesDialogCommand ?? (this.openSubtitlesDialogCommand = new RelayCommand(() =>
					{
						var subtitleViewModel = new SubtitleDialogViewModel(this.CurrentSubtitles);
						subtitleViewModel.Closing = () =>
						{
							if (subtitleViewModel.DialogResult)
							{
								this.CurrentSubtitles = subtitleViewModel.ChosenSubtitles;
							}
						};

						WindowManager.OpenDialog(subtitleViewModel, this);
					}));
			}
		}

		private RelayCommand openChaptersDialogCommand;
		public RelayCommand OpenChaptersDialogCommand
		{
			get
			{
				return this.openChaptersDialogCommand ?? (this.openChaptersDialogCommand = new RelayCommand(() =>
					{
						var chaptersVM = new ChapterMarkersDialogViewModel(this.SelectedTitle.Chapters.Count, this.CustomChapterNames, this.UseDefaultChapterNames);
						chaptersVM.Closing = () =>
						{
							if (chaptersVM.DialogResult)
							{
								this.UseDefaultChapterNames = chaptersVM.UseDefaultNames;
								if (!chaptersVM.UseDefaultNames)
								{
									this.CustomChapterNames = chaptersVM.ChapterNamesList;
								}
							}
						};

						WindowManager.OpenDialog(chaptersVM, this);
					}));
			}
		}

		private RelayCommand openAboutDialogCommand;
		public RelayCommand OpenAboutDialogCommand
		{
			get
			{
				return this.openAboutDialogCommand ?? (this.openAboutDialogCommand = new RelayCommand(() =>
					{
						WindowManager.OpenDialog(new AboutDialogViewModel(), this);
					}));
			}
		}

		private RelayCommand cancelScanCommand;
		public RelayCommand CancelScanCommand
		{
			get
			{
				return this.cancelScanCommand ?? (this.cancelScanCommand = new RelayCommand(() =>
					{
						this.scanCancelledFlag = true;
						this.ScanInstance.StopScan();
					}));
			}
		}

		private RelayCommand openFileCommand;
		public RelayCommand OpenFileCommand
		{
			get
			{
				return this.openFileCommand ?? (this.openFileCommand = new RelayCommand(() =>
					{
						this.SetSourceFromFile();
					},
					() =>
					{
						return !this.ScanningSource;
					}));
			}
		}

		private RelayCommand openFolderCommand;
		public RelayCommand OpenFolderCommand
		{
			get
			{
				return this.openFolderCommand ?? (this.openFolderCommand = new RelayCommand(() =>
					{
						this.SetSourceFromFolder();
					},
					() =>
					{
						return !this.ScanningSource;
					}));
			}
		}

		private RelayCommand customizeQueueColumnsCommand;
		public RelayCommand CustomizeQueueColumnsCommand
		{
			get
			{
				return this.customizeQueueColumnsCommand ?? (this.customizeQueueColumnsCommand = new RelayCommand(() =>
					{
						// Send a request that the view save the column sizes
						Messenger.Default.Send(new SaveQueueColumnsMessage());

						// Show the queue columns dialog
						var queueDialog = new QueueColumnsViewModel();
						WindowManager.OpenDialog(queueDialog, this);

						if (queueDialog.DialogResult)
						{
							// Apply new columns
							Settings.Default.QueueColumns = queueDialog.NewColumns;
							Settings.Default.Save();
							Messenger.Default.Send(new ApplyQueueColumnsMessage());
						}
					}));
			}
		}

		private RelayCommand importPresetCommand;
		public RelayCommand ImportPresetCommand
		{
			get
			{
				return this.importPresetCommand ?? (this.importPresetCommand = new RelayCommand(() =>
					{
						string presetFileName = FileService.Instance.GetFileNameLoad(null, "Import preset file", "xml", "XML Files|*.xml");
						if (presetFileName != null)
						{
							Unity.Container.Resolve<IPresetImportExport>().ImportPreset(presetFileName);
						}
					}));
			}
		}

		private RelayCommand exportPresetCommand;
		public RelayCommand ExportPresetCommand
		{
			get
			{
				return this.exportPresetCommand ?? (this.exportPresetCommand = new RelayCommand(() =>
					{
						Unity.Container.Resolve<IPresetImportExport>().ExportPreset(this.presetsVM.SelectedPreset.Preset);
					}));
			}
		}

		private RelayCommand openOptionsCommand;
		public RelayCommand OpenOptionsCommand
		{
			get
			{
				return this.openOptionsCommand ?? (this.openOptionsCommand = new RelayCommand(() =>
					{
						var optionsVM = new OptionsDialogViewModel(this.updater);
						WindowManager.OpenDialog(optionsVM, this);
						if (optionsVM.DialogResult)
						{
							Messenger.Default.Send(new OutputFolderChangedMessage());
							this.OutputPathVM.GenerateOutputFileName();
						}
					}));
			}
		}

		private RelayCommand openHomepageCommand;
		public RelayCommand OpenHomepageCommand
		{
			get
			{
				return this.openHomepageCommand ?? (this.openHomepageCommand = new RelayCommand(() =>
					{
						FileService.Instance.LaunchUrl("http://vidcoder.codeplex.com/");
					}));
			}
		}

		private RelayCommand reportBugCommand;
		public RelayCommand ReportBugCommand
		{
			get
			{
				return this.reportBugCommand ?? (this.reportBugCommand = new RelayCommand(() =>
					{
						FileService.Instance.LaunchUrl("http://vidcoder.codeplex.com/WorkItem/Create.aspx");
					}));
			}
		}

		private RelayCommand exitCommand;
		public RelayCommand ExitCommand
		{
			get
			{
				return this.exitCommand ?? (this.exitCommand = new RelayCommand(() =>
					{
						WindowManager.Close(this);
					}));
			}
		}

		public void StartAnimation(string animationKey)
		{
			if (this.AnimationStarted != null)
			{
				this.AnimationStarted(this, new EventArgs<string>(animationKey));
			}
		}

		public EncodeJob EncodeJob
		{
			get
			{
				// Debugging code... annoying exception happens here.
				if (this.SelectedSource == null)
				{
					throw new InvalidOperationException("Source must be selected.");
				}

				if (this.SelectedTitle == null)
				{
					throw new InvalidOperationException("Title must be selected.");
				}

				SourceType type = this.SelectedSource.Type;

				string outputPath = this.OutputPathVM.OutputPath;

				EncodingProfile encodingProfile = this.PresetsVM.SelectedPreset.Preset.EncodingProfile.Clone();

				int title = this.SelectedTitle.TitleNumber;

				var job = new EncodeJob
				{
					SourceType = type,
					SourcePath = this.SourcePath,
					OutputPath = outputPath,
					EncodingProfile = encodingProfile,
					Title = title,
					Angle = this.Angle,
					RangeType = this.RangeType,
					ChosenAudioTracks = this.GetChosenAudioTracks(),
					Subtitles = this.CurrentSubtitles,
					UseDefaultChapterNames = this.UseDefaultChapterNames,
					CustomChapterNames = this.CustomChapterNames,
					Length = this.SelectedTime
				};

				switch (this.RangeType)
				{
					case VideoRangeType.Chapters:
						int chapterStart, chapterEnd;

						if (this.SelectedStartChapter == null || this.SelectedEndChapter == null)
						{
							chapterStart = 1;
							chapterEnd = this.SelectedTitle.Chapters.Count;

							this.logger.LogError("Chapter range not selected. Using all chapters.");
						}
						else
						{
							chapterStart = this.SelectedStartChapter.ChapterNumber;
							chapterEnd = this.SelectedEndChapter.ChapterNumber;
						}

						job.ChapterStart = chapterStart;
						job.ChapterEnd = chapterEnd;
						break;
					case VideoRangeType.Seconds:
						job.SecondsStart = this.SecondsRangeStart;
						job.SecondsEnd = this.SecondsRangeEnd;
						break;
					case VideoRangeType.Frames:
						job.FramesStart = this.FramesRangeStart;
						job.FramesEnd = this.FramesRangeEnd;
						break;
					default:
						throw new ArgumentOutOfRangeException();
				}

				return job;
			}
		}

		public VideoSourceMetadata GetVideoSourceMetadata()
		{
			var metadata = new VideoSourceMetadata
			{
				Name = this.SourceName
			};

			if (this.SelectedSource.Type == SourceType.Dvd)
			{
				metadata.DriveInfo = this.SelectedSource.DriveInfo;
			}

			return metadata;
		}

		public EncodeJobViewModel CreateEncodeJobVM()
		{
			var newEncodeJobVM = new EncodeJobViewModel(this.EncodeJob);
			newEncodeJobVM.HandBrakeInstance = this.scanInstance;
			newEncodeJobVM.VideoSource = this.SourceData;
			newEncodeJobVM.VideoSourceMetadata = this.GetVideoSourceMetadata();
			newEncodeJobVM.ManualOutputPath = this.OutputPathVM.ManualOutputPath;

			return newEncodeJobVM;
		}

		public void RemoveAudioChoice(AudioChoiceViewModel choice)
		{
			this.AudioChoices.Remove(choice);
		}

		// Brings up specified job for editing, doing a scan if necessary.
		public void EditJob(EncodeJobViewModel jobVM, bool isQueueItem = true)
		{
			EncodeJob job = jobVM.Job;

			if (this.PresetsVM.SelectedPreset.IsModified)
			{
				MessageBoxResult dialogResult = Utilities.MessageBox.Show(this, "Do you want to save changes to your current preset?", "Save current preset?", MessageBoxButton.YesNoCancel);
				if (dialogResult == MessageBoxResult.Yes)
				{
					this.PresetsVM.SavePreset();
				}
				else if (dialogResult == MessageBoxResult.No)
				{
					this.PresetsVM.RevertPreset(userInitiated: false);
				}
				else if (dialogResult == MessageBoxResult.Cancel)
				{
					return;
				}
			}

			if (jobVM.HandBrakeInstance != null && jobVM.HandBrakeInstance == this.ScanInstance)
			{
				this.ApplyEncodeJobChoices(jobVM);
				Messenger.Default.Send(new RefreshPreviewMessage());
			}
			else if (jobVM.VideoSource != null)
			{
				// Set current scan to cached scan
				this.scanInstance = jobVM.HandBrakeInstance;
				this.SourceData = jobVM.VideoSource;
				this.LoadVideoSourceMetadata(job, jobVM.VideoSourceMetadata);

				this.ApplyEncodeJobChoices(jobVM);
			}
			else
			{
				string jobPath = job.SourcePath;
				string jobRoot = Path.GetPathRoot(jobPath);

				// We need to reconstruct the source metadata since the program has shut down since the job
				// was created.
				var videoSourceMetadata = new VideoSourceMetadata();

				switch (job.SourceType)
				{
					case SourceType.Dvd:
						DriveInformation driveInfo = this.DriveCollection.FirstOrDefault(d => string.Compare(d.RootDirectory, jobRoot, StringComparison.OrdinalIgnoreCase) == 0);
						if (driveInfo == null)
						{
							Unity.Container.Resolve<IMessageBoxService>().Show("Cannot load for editing. The disc is no longer in the drive.");
							return;
						}

						videoSourceMetadata.Name = driveInfo.VolumeLabel;
						videoSourceMetadata.DriveInfo = driveInfo;
						break;
					case SourceType.File:
						videoSourceMetadata.Name = Utilities.GetSourceNameFile(job.SourcePath);
						break;
					case SourceType.VideoFolder:
						videoSourceMetadata.Name = Utilities.GetSourceNameFolder(job.SourcePath);
						break;
				}

				this.LoadVideoSourceMetadata(job, videoSourceMetadata);
				this.StartScan(job.SourcePath, jobVM);
			}

			string presetName = isQueueItem ? "Restored from Queue" : "Restored from Completed";

			var queuePreset = new PresetViewModel(
				new Preset
				{
					IsBuiltIn = false,
					IsModified = false,
					IsQueue = true,
					Name = presetName,
					EncodingProfile = jobVM.Job.EncodingProfile.Clone()
				});

			this.PresetsVM.InsertQueuePreset(queuePreset);

			if (isQueueItem)
			{
				// Since it's been sent back for editing, remove the queue item
				this.ProcessingVM.EncodeQueue.Remove(jobVM);
			}

			this.PresetsVM.SaveUserPresets();
		}

		public void RefreshChapterMarkerUI()
		{
			this.RaisePropertyChanged(() => this.ShowChapterMarkerUI);
		}

		public void RefreshTrayIcon(bool minimized)
		{
			this.ShowTrayIcon = Settings.Default.MinimizeToTray && minimized;
		}

		/// <summary>
		/// Get a list of audio tracks chosen by the user (1-based).
		/// </summary>
		/// <returns>List of audio tracks chosen by the user (1-based).</returns>
		public List<int> GetChosenAudioTracks()
		{
			var tracks = new List<int>();

			foreach (AudioChoiceViewModel audioChoiceVM in this.AudioChoices)
			{
				tracks.Add(audioChoiceVM.SelectedIndex + 1);
			}

			return tracks;
		}

		private TimeSpan SelectedTime
		{
			get
			{
				TimeSpan selectedTime = TimeSpan.Zero;

				switch (this.RangeType)
				{
					case VideoRangeType.Chapters:
						if (this.SelectedStartChapter == null || this.SelectedEndChapter == null)
						{
							return TimeSpan.Zero;
						}

						selectedTime = this.GetChapterRangeDuration(this.SelectedStartChapter.ChapterNumber, this.SelectedEndChapter.ChapterNumber);

						//int selectionStart = this.SelectedStartChapter.ChapterNumber;
						//int selectionEnd = this.SelectedEndChapter.ChapterNumber;

						//for (int i = selectionStart; i <= selectionEnd; i++)
						//{
						//    selectedTime += this.SelectedTitle.Chapters[i].Duration;
						//}

						break;
					case VideoRangeType.Seconds:
						if (this.SecondsRangeEnd >= this.SecondsRangeStart)
						{
							selectedTime = TimeSpan.FromSeconds(this.SecondsRangeEnd - this.SecondsRangeStart);
						}

						break;
					case VideoRangeType.Frames:
						if (this.FramesRangeEnd >= this.FramesRangeStart)
						{
							selectedTime = TimeSpan.FromSeconds(((double)(this.FramesRangeEnd - this.FramesRangeStart)) / this.SelectedTitle.Framerate);
						}

						break;
				}

				return selectedTime;
			}
		}

		/// <summary>
		/// Starts a scan of a video source
		/// </summary>
		/// <param name="path">The path to scan.</param>
		/// <param name="jobVM">The encode job choice to apply after the scan is finished.</param>
		private void StartScan(string path, EncodeJobViewModel jobVM = null)
		{
			this.ClearVideoSource();

			this.ScanProgress = 0;
			HandBrakeInstance oldInstance = this.scanInstance;
			if (oldInstance != null)
			{
				this.ProcessingVM.CleanupHandBrakeInstanceIfUnused(oldInstance);
			}

			this.logger.Log("Starting scan: " + path);

			this.scanInstance = new HandBrakeInstance();
			this.scanInstance.Initialize(Settings.Default.LogVerbosity);
			this.scanInstance.ScanProgress += (o, e) =>
			{
				this.ScanProgress = (e.CurrentTitle * 100) / e.Titles;
			};
			this.scanInstance.ScanCompleted += (o, e) =>
			{
				DispatchService.Invoke(() =>
				{
					this.ScanningSource = false;

					if (this.scanCancelledFlag)
					{
						this.SelectedSource = null;
						this.RaisePropertyChanged(() => this.SourcePicked);

						if (this.ScanCancelled != null)
						{
							this.ScanCancelled(this, new EventArgs());
						}

						this.logger.Log("Scan cancelled");
					}
					else
					{
						this.SourceData = new VideoSource { Titles = this.scanInstance.Titles, FeatureTitle = this.scanInstance.FeatureTitle };

						// If scan failed source data will be null.
						if (this.sourceData != null)
						{
							if (jobVM != null)
							{
								this.ApplyEncodeJobChoices(jobVM);
							}

							if (jobVM == null && this.SelectedSource != null &&
							    (this.SelectedSource.Type == SourceType.File || this.SelectedSource.Type == SourceType.VideoFolder))
							{
								SourceHistory.AddToHistory(this.SourcePath);
							}
						}

						this.logger.Log("Scan completed");
					}

					this.logger.Log("");
				});
			};

			this.ScanError = false;
			this.ScanningSource = true;
			this.scanCancelledFlag = false;
			this.scanInstance.StartScan(path, Settings.Default.PreviewCount);
		}

		private void UpdateFromNewVideoSource()
		{
			if (this.sourceData != null && this.sourceData.Titles.Count > 0)
			{
				this.ScanError = false;

				Title selectTitle = null;
				if (this.sourceData.FeatureTitle > 0)
				{
					selectTitle = this.sourceData.Titles.FirstOrDefault(title => title.TitleNumber == this.sourceData.FeatureTitle);

				}
				else
				{
					// Select the first title within 80% of the duration of the longest title.
					double maxSeconds = this.sourceData.Titles.Max(title => title.Duration.TotalSeconds);
					foreach (Title title in this.sourceData.Titles)
					{
						if (title.Duration.TotalSeconds >= maxSeconds * .8)
						{
							selectTitle = title;
							break;
						}
					}

					if (selectTitle == null)
					{
						selectTitle = this.sourceData.Titles[0];
					}
				}

				this.SelectedTitle = selectTitle;
				this.RaisePropertyChanged(() => this.Titles);
				this.RaisePropertyChanged(() => this.TitleVisible);
				this.RaisePropertyChanged(() => this.HasVideoSource);
				this.RaisePropertyChanged(() => this.SourceOptionsVisible);
			}
			else
			{
				this.sourceData = null;
				this.RaisePropertyChanged(() => this.HasVideoSource);
				this.RaisePropertyChanged(() => this.SourceOptionsVisible);
				this.RaisePropertyChanged(() => this.SourcePicked);
				this.ScanError = true;
			}

			DispatchService.BeginInvoke(() => Messenger.Default.Send(new VideoSourceChangedMessage()));
		}

		private void ClearVideoSource()
		{
			this.sourceData = null;
			this.SourceSelectionExpanded = false;
			this.RaisePropertyChanged(() => this.HasVideoSource);
			this.RaisePropertyChanged(() => this.SourceOptionsVisible);
			this.RaisePropertyChanged(() => this.SourcePicked);

			DispatchService.BeginInvoke(() => Messenger.Default.Send(new VideoSourceChangedMessage()));
			Messenger.Default.Send(new RefreshPreviewMessage());
		}

		private void LoadVideoSourceMetadata(EncodeJob job, VideoSourceMetadata metadata)
		{
			this.SourceName = metadata.Name;
			this.SourcePath = job.SourcePath;

			if (job.SourceType == SourceType.Dvd)
			{
				this.SelectedSource = new SourceOption { Type = SourceType.Dvd, DriveInfo = metadata.DriveInfo };
			}
			else
			{
				this.SelectedSource = new SourceOption { Type = job.SourceType };
			}
		}

		// Applies the encode job choices to the viewmodel. Part of editing a queued item,
		// assumes a scan has been done first with the data available.
		private void ApplyEncodeJobChoices(EncodeJobViewModel jobVM)
		{
			if (this.sourceData == null || this.sourceData.Titles.Count == 0)
			{
				return;
			}

			EncodeJob job = jobVM.Job;

			// Title
			Title newTitle = this.sourceData.Titles.FirstOrDefault(t => t.TitleNumber == job.Title);
			if (newTitle == null)
			{
				newTitle = this.sourceData.Titles[0];
			}

			this.selectedTitle = newTitle;

			// Angle
			this.PopulateAnglesList();

			if (job.Angle <= this.selectedTitle.AngleCount)
			{
				this.angle = job.Angle;
			}
			else
			{
				this.angle = 0;
			}

			// Range
			this.PopulateChapterSelectLists();
			this.rangeType = job.RangeType;
			switch (job.RangeType)
			{
				case VideoRangeType.Chapters:
					if (job.ChapterStart > this.selectedTitle.Chapters.Count ||
						job.ChapterEnd > this.selectedTitle.Chapters.Count)
					{
						this.selectedStartChapter = this.StartChapters.FirstOrDefault(c => c.Chapter == this.selectedTitle.Chapters[0]);
						this.selectedEndChapter = this.EndChapters.FirstOrDefault(c => c.Chapter == this.selectedTitle.Chapters[this.selectedTitle.Chapters.Count - 1]);
					}
					else
					{
						this.selectedStartChapter = this.StartChapters.FirstOrDefault(c => c.Chapter == this.selectedTitle.Chapters[job.ChapterStart - 1]);
						this.selectedEndChapter = this.EndChapters.FirstOrDefault(c => c.Chapter == this.selectedTitle.Chapters[job.ChapterEnd - 1]);
					}

					break;
				case VideoRangeType.Seconds:
					if (job.SecondsStart < this.selectedTitle.Duration.TotalSeconds + 1 &&
						job.SecondsEnd < this.selectedTitle.Duration.TotalSeconds + 1)
					{
						this.secondsRangeStart = job.SecondsStart;
						this.secondsRangeEnd = job.SecondsEnd;
					}
					else
					{
						this.secondsRangeStart = 0;
						this.secondsRangeEnd = this.selectedTitle.Duration.TotalSeconds;
					}

					break;
				case VideoRangeType.Frames:
					if (job.FramesStart < this.selectedTitle.Frames + 1 &&
						job.FramesEnd < this.selectedTitle.Frames + 1)
					{
						this.framesRangeStart = job.FramesStart;
						this.framesRangeEnd = job.FramesEnd;
					}
					else
					{
						this.framesRangeStart = 0;
						this.framesRangeEnd = this.selectedTitle.Frames;
					}

					break;
				default:
					throw new ArgumentOutOfRangeException();
			}

			// Audio tracks
			this.AudioChoices.Clear();
			foreach (int chosenTrack in job.ChosenAudioTracks)
			{
				if (chosenTrack <= this.selectedTitle.AudioTracks.Count)
				{
					this.AudioChoices.Add(new AudioChoiceViewModel { SelectedIndex = chosenTrack - 1 });
				}
			}

			// Subtitles (standard+SRT)
			this.CurrentSubtitles.SourceSubtitles = new List<SourceSubtitle>();
			this.CurrentSubtitles.SrtSubtitles = new List<SrtSubtitle>();
			if (job.Subtitles.SourceSubtitles != null)
			{
				foreach (SourceSubtitle sourceSubtitle in job.Subtitles.SourceSubtitles)
				{
					if (sourceSubtitle.TrackNumber <= this.selectedTitle.Subtitles.Count)
					{
						this.CurrentSubtitles.SourceSubtitles.Add(sourceSubtitle);
					}
				}
			}

			if (job.Subtitles.SrtSubtitles != null)
			{
				foreach (SrtSubtitle srtSubtitle in job.Subtitles.SrtSubtitles)
				{
					this.CurrentSubtitles.SrtSubtitles.Add(srtSubtitle);
				}
			}

			// Custom chapter markers
			this.UseDefaultChapterNames = job.UseDefaultChapterNames;

			if (job.UseDefaultChapterNames)
			{
				this.CustomChapterNames = null;
			}
			else
			{
				if (this.selectedTitle.Chapters.Count == this.CustomChapterNames.Count)
				{
					this.CustomChapterNames = job.CustomChapterNames;
				}
			}

			// Output path
			this.OutputPathVM.OutputPath = job.OutputPath;
			this.OutputPathVM.ManualOutputPath = jobVM.ManualOutputPath;

			// Encode profile handled above this in EditJob

			this.RaisePropertyChanged(() => this.SourceIcon);
			this.RaisePropertyChanged(() => this.SourceText);
			this.RaisePropertyChanged(() => this.SelectedTitle);
			this.RaisePropertyChanged(() => this.SelectedStartChapter);
			this.RaisePropertyChanged(() => this.SelectedEndChapter);
			this.RaisePropertyChanged(() => this.StartChapters);
			this.RaisePropertyChanged(() => this.EndChapters);
			this.RaisePropertyChanged(() => this.SecondsRangeStart);
			this.RaisePropertyChanged(() => this.SecondsRangeEnd);
			this.RaisePropertyChanged(() => this.FramesRangeStart);
			this.RaisePropertyChanged(() => this.FramesRangeEnd);
			this.RaisePropertyChanged(() => this.RangeType);
			this.RaisePropertyChanged(() => this.ChaptersRangeVisible);
			this.RaisePropertyChanged(() => this.SecondsRangeVisible);
			this.RaisePropertyChanged(() => this.FramesRangeVisible);
			this.RaisePropertyChanged(() => this.SubtitlesSummary);
			this.RaisePropertyChanged(() => this.ChapterMarkersSummary);
			this.RaisePropertyChanged(() => this.ShowChapterMarkerUI);
			this.RaisePropertyChanged(() => this.Angle);
			this.RaisePropertyChanged(() => this.Angles);

			this.RefreshRangePreview();
		}

		private void PopulateChapterSelectLists()
		{
			this.startChapters = new List<ChapterViewModel>();
			this.endChapters = new List<ChapterViewModel>();

			foreach (Chapter chapter in this.selectedTitle.Chapters)
			{
				this.startChapters.Add(new ChapterViewModel(chapter));
				this.endChapters.Add(new ChapterViewModel(chapter));
			}
		}

		private void PopulateAnglesList()
		{
			this.angles = new List<int>();
			for (int i = 1; i <= this.selectedTitle.AngleCount; i++)
			{
				this.angles.Add(i);
			}
		}

		private void ReportLengthChanged()
		{
			var encodingWindow = WindowManager.FindWindow(typeof(EncodingViewModel)) as EncodingViewModel;
			if (encodingWindow != null)
			{
				encodingWindow.NotifyLengthChanged();
			}
		}

		private void UpdateDrives()
		{
			DispatchService.BeginInvoke(() =>
			{
				// Remove all source options which do not exist in the new collection
				for (int i = this.sourceOptions.Count - 1; i >= 0; i--)
				{
					if (this.sourceOptions[i].SourceOption.Type == SourceType.Dvd)
					{
						if (!this.DriveCollection.Any(driveInfo => driveInfo.RootDirectory == this.sourceOptions[i].SourceOption.DriveInfo.RootDirectory))
						{
							this.sourceOptions.RemoveAt(i);
						}
					}
				}

				// Update or add new options
				foreach (DriveInformation drive in this.DriveCollection)
				{
					SourceOptionViewModel currentOption = this.sourceOptions.SingleOrDefault(sourceOptionVM => sourceOptionVM.SourceOption.Type == SourceType.Dvd && sourceOptionVM.SourceOption.DriveInfo.RootDirectory == drive.RootDirectory);

					if (currentOption == null)
					{
						// The device is new, add it
						var newSourceOptionVM = new SourceOptionViewModel(new SourceOption { Type = SourceType.Dvd, DriveInfo = drive });

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
						currentOption.VolumeLabel = drive.VolumeLabel;
					}
				}
			});
		}

		private int GetFirstUnusedAudioTrack()
		{
			List<int> unusedTracks = new List<int>();

			for (int i = 0; i < this.SelectedTitle.AudioTracks.Count; i++)
			{
				unusedTracks.Add(i);
			}

			foreach (AudioChoiceViewModel audioChoiceVM in this.AudioChoices)
			{
				if (unusedTracks.Contains(audioChoiceVM.SelectedIndex))
				{
					unusedTracks.Remove(audioChoiceVM.SelectedIndex);
				}
			}

			if (unusedTracks.Count > 0)
			{
				return unusedTracks[0];
			}

			return 0;
		}

		private void OnRangeControlGotFocus(RangeFocusMessage message)
		{
			if (!message.GotFocus)
			{
				return;
			}

			if (message.RangeType == VideoRangeType.Seconds)
			{
				if (message.Start)
				{
					this.secondsBaseline = this.SecondsRangeEnd;
				}
				else
				{
					this.secondsBaseline = this.SecondsRangeStart;
				}
			}
			else if (message.RangeType == VideoRangeType.Frames)
			{
				if (message.Start)
				{
					this.framesBaseline = this.FramesRangeEnd;
				}
				else
				{
					this.framesBaseline = this.FramesRangeStart;
				}
			}
		}

		private void RefreshRangePreview()
		{
			this.RaisePropertyChanged(() => this.RangePreviewStart);
			this.RaisePropertyChanged(() => this.RangePreviewEnd);
			this.RaisePropertyChanged(() => this.RangePreviewText);
			this.RaisePropertyChanged(() => this.RangePreviewLengthText);
		}

		private TimeSpan GetChapterRangeDuration(int startChapter, int endChapter)
		{
			if (startChapter > endChapter ||
				endChapter > this.SelectedTitle.Chapters.Count ||
				startChapter < 1)
			{
				return TimeSpan.Zero;
			}

			TimeSpan rangeTime = TimeSpan.Zero;

			for (int i = startChapter; i <= endChapter; i++)
			{
				rangeTime += this.SelectedTitle.Chapters[i - 1].Duration;
			}

			return rangeTime;
		}
	}
}
