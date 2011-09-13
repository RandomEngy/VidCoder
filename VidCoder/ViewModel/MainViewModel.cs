using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.ObjectModel;
using System.Windows;
using System.IO;
using System.Windows.Input;
using System.Windows.Shell;
using HandBrake.Interop;
using HandBrake.Interop.Model;
using HandBrake.Interop.Model.Encoding;
using HandBrake.Interop.SourceData;
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

		private VideoRangeType videoRangeType;
		private double secondsRangeStart;
		private double secondsRangeEnd;
		private int framesRangeStart;
		private int framesRangeEnd;
		private Chapter selectedStartChapter;
		private Chapter selectedEndChapter;

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

		private ICommand toggleSourceMenuCommand;
		private ICommand openSubtitlesDialogCommand;
		private ICommand openChaptersDialogCommand;
		private ICommand openAboutDialogCommand;
		private ICommand addTrackCommand;
		private ICommand cancelScanCommand;
		private ICommand openFileCommand;
		private ICommand openFolderCommand;
		private ICommand customizeQueueColumnsCommand;
		private ICommand importPresetCommand;
		private ICommand exportPresetCommand;
		private ICommand openOptionsCommand;
		private ICommand openHomepageCommand;
		private ICommand reportBugCommand;
		private ICommand exitCommand;

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
			if (!System.Windows.Threading.Dispatcher.CurrentDispatcher.CheckAccess())
			{
				System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(new Action(this.UpdateDriveCollection));
				return;
			}

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
					this.NotifyPropertyChanged("HasVideoSource");
					this.NotifyPropertyChanged("SourceText");
				}
				else if (!this.SelectedSource.DriveInfo.Empty && !currentDrivePresent)
				{
					// If we had an item and the drive is now gone, update and remove the video source.
					DriveInformation emptyDrive = this.SelectedSource.DriveInfo;
					emptyDrive.Empty = true;

					this.sourceData = null;
					this.SelectedTitle = null;
					this.NotifyPropertyChanged("HasVideoSource");
					this.NotifyPropertyChanged("SourceText");
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
			//DirectoryInfo parentDirectory = Directory.GetParent(videoFolder);
			//if (parentDirectory == null || parentDirectory.Root.FullName == parentDirectory.FullName)
			//{
			//    this.sourceName = "VideoFolder";
			//}
			//else
			//{
			//    this.sourceName = parentDirectory.Name;
			//}

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
				this.NotifyPropertyChanged("SourceSelectionExpanded");
				this.NotifyPropertyChanged("SourceOptionsVisible");
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
				this.NotifyPropertyChanged("SourcePicked");
				this.NotifyPropertyChanged("SourceIcon");
				this.NotifyPropertyChanged("SourceText");
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
				this.NotifyPropertyChanged("SourceOptionsVisible");
				this.NotifyPropertyChanged("HasVideoSource");
				this.NotifyPropertyChanged("ScanningSource");
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
				this.NotifyPropertyChanged("ScanError");
				this.NotifyPropertyChanged("SourceOptionsVisible");
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
				this.NotifyPropertyChanged("ScanProgress");
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
								AudioChoiceViewModel newVM = new AudioChoiceViewModel();
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
						AudioChoiceViewModel newVM = new AudioChoiceViewModel();
						newVM.SelectedIndex = 0;
						this.AudioChoices.Add(newVM);
					}

					this.UseDefaultChapterNames = true;
					this.SelectedStartChapter = this.selectedTitle.Chapters[0];
					this.SelectedEndChapter = this.selectedTitle.Chapters[this.selectedTitle.Chapters.Count - 1];

					this.SecondsRangeStart = 0;
					this.SecondsRangeEnd = this.selectedTitle.Duration.TotalSeconds;

					this.FramesRangeStart = 0;
					this.FramesRangeEnd = this.selectedTitle.Frames;

					this.angles = new List<int>();
					for (int i = 1; i <= this.selectedTitle.AngleCount; i++)
					{
						this.angles.Add(i);
					}

					this.angle = 1;

					this.NotifyPropertyChanged("Angles");
					this.NotifyPropertyChanged("Angle");
					this.NotifyPropertyChanged("AngleVisible");

					this.oldTitle = value;

					this.JobCreationAvailable = true;
				}

				var previewWindow = WindowManager.FindWindow(typeof(PreviewViewModel)) as PreviewViewModel;
				if (previewWindow != null)
				{
					previewWindow.RequestRefreshPreviews();
				}

				// Custom chapter names are thrown out when switching titles.
				this.CustomChapterNames = null;

				this.NotifyPropertyChanged("SubtitlesSummary");
				CommandManager.InvalidateRequerySuggested();

				this.OutputPathVM.GenerateOutputFileName();

				this.NotifyPropertyChanged("Chapters");
				this.NotifyPropertyChanged("MultipleChapters");
				this.NotifyPropertyChanged("SelectedTitle");
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

				PreviewViewModel previewWindow = WindowManager.FindWindow(typeof(PreviewViewModel)) as PreviewViewModel;
				if (previewWindow != null)
				{
					previewWindow.RequestRefreshPreviews();
				}

				this.NotifyPropertyChanged("Angle");
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
				this.NotifyPropertyChanged("CurrentSubtitles");
				this.NotifyPropertyChanged("SubtitlesSummary");
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
				this.NotifyPropertyChanged("UseDefaultChapterNames");
				this.NotifyPropertyChanged("ChapterMarkersSummary");
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
				this.NotifyPropertyChanged("CustomChapterNames");
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

		public VideoRangeType VideoRangeType
		{
			get
			{
				return this.videoRangeType;
			}

			set
			{
				this.videoRangeType = value;

				this.OutputPathVM.GenerateOutputFileName();
				this.NotifyPropertyChanged("VideoRangeType");
				this.NotifyPropertyChanged("ChaptersRangeVisible");
				this.NotifyPropertyChanged("SecondsRangeVisible");
				this.NotifyPropertyChanged("FramesRangeVisible");
				this.NotifyPropertyChanged("VideoRangeSummary");
				this.ReportLengthChanged();
			}
		}

		public List<Chapter> Chapters
		{
			get
			{
				if (this.SourceData != null)
				{
					return this.selectedTitle.Chapters;
				}

				return null;
			}
		}

		public bool ChaptersRangeVisible
		{
			get
			{
				return this.VideoRangeType == VideoRangeType.Chapters;
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

				if (this.secondsRangeEnd < this.secondsRangeStart + SecondsRangeBuffer)
				{
					this.secondsRangeEnd = this.secondsRangeStart + SecondsRangeBuffer;
					this.NotifyPropertyChanged("SecondsRangeEnd");
				}

				this.OutputPathVM.GenerateOutputFileName();

				this.NotifyPropertyChanged("SecondsRangeStart");
				this.NotifyPropertyChanged("VideoRangeSummary");
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

				if (this.secondsRangeStart > this.secondsRangeEnd - SecondsRangeBuffer)
				{
					this.secondsRangeStart = this.secondsRangeEnd - SecondsRangeBuffer;
					this.NotifyPropertyChanged("SecondsRangeStart");
				}

				this.OutputPathVM.GenerateOutputFileName();

				this.NotifyPropertyChanged("SecondsRangeEnd");
				this.NotifyPropertyChanged("VideoRangeSummary");
				this.ReportLengthChanged();
			}
		}

		public bool SecondsRangeVisible
		{
			get
			{
				return this.VideoRangeType == VideoRangeType.Seconds;
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

				if (this.framesRangeEnd <= this.framesRangeStart)
				{
					this.framesRangeEnd = this.framesRangeStart + 1;
					this.NotifyPropertyChanged("FramesRangeEnd");
				}

				this.OutputPathVM.GenerateOutputFileName();

				this.NotifyPropertyChanged("FramesRangeStart");
				this.NotifyPropertyChanged("VideoRangeSummary");
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

				if (this.framesRangeStart >= this.framesRangeEnd)
				{
					this.framesRangeStart = this.framesRangeEnd - 1;
					this.NotifyPropertyChanged("FramesRangeStart");
				}

				this.OutputPathVM.GenerateOutputFileName();

				this.NotifyPropertyChanged("FramesRangeEnd");
				this.NotifyPropertyChanged("VideoRangeSummary");
				this.ReportLengthChanged();
			}
		}

		public bool FramesRangeVisible
		{
			get
			{
				return this.VideoRangeType == VideoRangeType.Frames;
			}
		}

		public Chapter SelectedStartChapter
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
					this.SelectedEndChapter = this.selectedStartChapter;
				}

				this.OutputPathVM.GenerateOutputFileName();

				this.NotifyPropertyChanged("SelectedStartChapter");
				this.NotifyPropertyChanged("VideoRangeSummary");
				this.ReportLengthChanged();
			}
		}

		public Chapter SelectedEndChapter
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
					this.SelectedStartChapter = this.selectedEndChapter;
				}

				this.OutputPathVM.GenerateOutputFileName();

				this.NotifyPropertyChanged("SelectedEndChapter");
				this.NotifyPropertyChanged("VideoRangeSummary");
				this.ReportLengthChanged();
			}
		}

		public string VideoRangeSummary
		{
			get
			{
				if (this.HasVideoSource)
				{
					TimeSpan selectedTime = this.SelectedTime;
					string timeString = string.Format("{0:00}:{1:00}:{2:00}", selectedTime.Hours, selectedTime.Minutes, selectedTime.Seconds);

					switch (this.VideoRangeType)
					{
						case VideoRangeType.Chapters:
							return "of " + this.SelectedTitle.Chapters.Count + " (" + timeString + ")";
						case VideoRangeType.Seconds:
						case VideoRangeType.Frames:
							return "(" + timeString + ")";
					}
				}

				return string.Empty;
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

		public ICommand AddTrackCommand
		{
			get
			{
				if (this.addTrackCommand == null)
				{
					this.addTrackCommand = new RelayCommand(
						param =>
						{
							AudioChoiceViewModel newAudioChoice = new AudioChoiceViewModel();
							newAudioChoice.SelectedIndex = this.GetFirstUnusedAudioTrack();
							this.AudioChoices.Add(newAudioChoice);
						},
						param =>
						{
							if (this.SelectedTitle == null)
							{
								return true;
							}

							return this.SelectedTitle.AudioTracks.Count > 0;
						});
				}

				return this.addTrackCommand;
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

		public string QueuedTabHeader
		{
			get
			{
				if (this.ProcessingVM.EncodeQueue.Count == 0)
				{
					return "Queued";
				}

				return "Queued (" + this.ProcessingVM.EncodeQueue.Count + ")";
			}
		}

		public string CompletedTabHeader
		{
			get
			{
				return "Completed (" + this.ProcessingVM.CompletedJobs.Count + ")";
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
				this.NotifyPropertyChanged("ShowTrayIcon");
			}
		}

		public string TrayIconToolTip
		{
			get
			{
				return "VidCoder " + Utilities.CurrentVersion;
			}
		}

		public ICommand ToggleSourceMenuCommand
		{
			get
			{
				if (this.toggleSourceMenuCommand == null)
				{
					this.toggleSourceMenuCommand = new RelayCommand(param =>
					{
						this.SourceSelectionExpanded = !this.SourceSelectionExpanded;
					},
					param =>
					{
						return !this.ScanningSource;
					});
				}

				return this.toggleSourceMenuCommand;
			}
		}

		public ICommand OpenSubtitlesDialogCommand
		{
			get
			{
				if (this.openSubtitlesDialogCommand == null)
				{
					this.openSubtitlesDialogCommand = new RelayCommand(param =>
					{
						SubtitleDialogViewModel subtitleViewModel = new SubtitleDialogViewModel(this.CurrentSubtitles);
						subtitleViewModel.Closing = () =>
						{
							if (subtitleViewModel.DialogResult)
							{
								this.CurrentSubtitles = subtitleViewModel.ChosenSubtitles;
							}
						};

						WindowManager.OpenDialog(subtitleViewModel, this);
					});
				}

				return this.openSubtitlesDialogCommand;
			}
		}

		public ICommand OpenChaptersDialogCommand
		{
			get
			{
				if (this.openChaptersDialogCommand == null)
				{
					this.openChaptersDialogCommand = new RelayCommand(param =>
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
					});
				}

				return this.openChaptersDialogCommand;
			}
		}

		public ICommand OpenAboutDialogCommand
		{
			get
			{
				if (this.openAboutDialogCommand == null)
				{
					this.openAboutDialogCommand = new RelayCommand(param =>
					{
						WindowManager.OpenDialog(new AboutDialogViewModel(), this);
					});
				}

				return this.openAboutDialogCommand;
			}
		}

		public ICommand CancelScanCommand
		{
			get
			{
				if (this.cancelScanCommand == null)
				{
					this.cancelScanCommand = new RelayCommand(param =>
					{
						this.scanCancelledFlag = true;
						this.ScanInstance.StopScan();
					});
				}

				return this.cancelScanCommand;
			}
		}

		public ICommand OpenFileCommand
		{
			get
			{
				if (this.openFileCommand == null)
				{
					this.openFileCommand = new RelayCommand(param =>
					{
						this.SetSourceFromFile();
					},
					param =>
					{
						return !this.ScanningSource;
					});
				}

				return this.openFileCommand;
			}
		}

		public ICommand OpenFolderCommand
		{
			get
			{
				if (this.openFolderCommand == null)
				{
					this.openFolderCommand = new RelayCommand(param =>
					{
						this.SetSourceFromFolder();
					},
					param =>
					{
						return !this.ScanningSource;
					});
				}

				return this.openFolderCommand;
			}
		}

		public ICommand CustomizeQueueColumnsCommand
		{
			get
			{
				if (this.customizeQueueColumnsCommand == null)
				{
					this.customizeQueueColumnsCommand = new RelayCommand(param =>
					{
						// Send a request that the view save the column sizes
						this.NotifyPropertyChanged("QueueColumnsSaveRequest");

						// Show the queue columns dialog
						var queueDialog = new QueueColumnsViewModel();
						WindowManager.OpenDialog(queueDialog, this);

						if (queueDialog.DialogResult)
						{
							// Apply new columns
							Settings.Default.QueueColumns = queueDialog.NewColumns;
							Settings.Default.Save();
							this.NotifyPropertyChanged("QueueColumns");
						}
					});
				}

				return this.customizeQueueColumnsCommand;
			}
		}

		public ICommand ImportPresetCommand
		{
			get
			{
				if (this.importPresetCommand == null)
				{
					this.importPresetCommand = new RelayCommand(param =>
					{
						string presetFileName = FileService.Instance.GetFileNameLoad(null, "Import preset file", "xml", "XML Files|*.xml");
						if (presetFileName != null)
						{
							Unity.Container.Resolve<IPresetImportExport>().ImportPreset(presetFileName);
						}
					});
				}

				return this.importPresetCommand;
			}
		}

		public ICommand ExportPresetCommand
		{
			get
			{
				if (this.exportPresetCommand == null)
				{
					this.exportPresetCommand = new RelayCommand(param =>
					{
						Unity.Container.Resolve<IPresetImportExport>().ExportPreset(this.presetsVM.SelectedPreset.Preset);
					});
				}

				return this.exportPresetCommand;
			}
		}

		public ICommand OpenOptionsCommand
		{
			get
			{
				if (this.openOptionsCommand == null)
				{
					this.openOptionsCommand = new RelayCommand(param =>
					{
						var optionsVM = new OptionsDialogViewModel(this.updater);
						WindowManager.OpenDialog(optionsVM, this);
						if (optionsVM.DialogResult)
						{
							this.OutputPathVM.GenerateOutputFileName();
						}
					});
				}

				return this.openOptionsCommand;
			}
		}

		public ICommand OpenHomepageCommand
		{
			get
			{
				if (this.openHomepageCommand == null)
				{
					this.openHomepageCommand = new RelayCommand(param =>
					{
						FileService.Instance.LaunchUrl("http://vidcoder.codeplex.com/");
					});
				}

				return this.openHomepageCommand;
			}
		}

		public ICommand ReportBugCommand
		{
			get
			{
				if (this.reportBugCommand == null)
				{
					this.reportBugCommand = new RelayCommand(param =>
					{
						FileService.Instance.LaunchUrl("http://vidcoder.codeplex.com/WorkItem/Create.aspx");
					});
				}

				return this.reportBugCommand;
			}
		}

		public ICommand ExitCommand
		{
			get
			{
				if (this.exitCommand == null)
				{
					this.exitCommand = new RelayCommand(param =>
					{
						WindowManager.Close(this);
					});
				}

				return this.exitCommand;
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
				// Break out values to find where the reported null reference crashes are coming from.
				SourceType type = this.SelectedSource.Type;

				string outputPath = this.OutputPathVM.OutputPath;

				EncodingProfile encodingProfile = this.PresetsVM.SelectedPreset.Preset.EncodingProfile.Clone();

				int title = this.SelectedTitle.TitleNumber;

				int startChapter = this.SelectedStartChapter.ChapterNumber;

				int endChapter = this.SelectedEndChapter.ChapterNumber;

				return new EncodeJob
				{
					SourceType = type,
					SourcePath = this.SourcePath,
					OutputPath = outputPath,
					EncodingProfile = encodingProfile,
					Title = title,
					Angle = this.Angle,
					RangeType = this.VideoRangeType,
					ChapterStart = startChapter,
					ChapterEnd = endChapter,
					SecondsStart = this.SecondsRangeStart,
					SecondsEnd = this.SecondsRangeEnd,
					FramesStart = this.FramesRangeStart,
					FramesEnd = this.FramesRangeEnd,
					ChosenAudioTracks = this.GetChosenAudioTracks(),
					Subtitles = this.CurrentSubtitles,
					UseDefaultChapterNames = this.UseDefaultChapterNames,
					CustomChapterNames = this.CustomChapterNames,
					Length = this.SelectedTime
				};
			}
		}

		/// <summary>
		/// Cleans up the given HandBrake instance if it's not being used anymore.
		/// </summary>
		/// <param name="instance">The instance to clean up.</param>
		public void CleanupHandBrakeInstance(HandBrakeInstance instance)
		{
			foreach (EncodeJobViewModel encodeJobVM in this.ProcessingVM.EncodeQueue)
			{
				if (instance == encodeJobVM.HandBrakeInstance)
				{
					return;
				}
			}

			if (instance == this.scanInstance)
			{
				return;
			}

			instance.Dispose();
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

		// Brings up specified queue item for editing, doing a scan if necessary.
		public void EditQueueJob(EncodeJobViewModel jobVM)
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

				var previewWindow = WindowManager.FindWindow(typeof(PreviewViewModel)) as PreviewViewModel;
				if (previewWindow != null)
				{
					previewWindow.RequestRefreshPreviews();
				}
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

			var queuePreset = new PresetViewModel(
				new Preset
				{
					IsBuiltIn = false,
					IsModified = false,
					IsQueue = true,
					Name = "Restored from Queue",
					EncodingProfile = jobVM.Job.EncodingProfile.Clone()
				});

			this.PresetsVM.InsertQueuePreset(queuePreset);

			// Since it's been sent back for editing, remove the queue item
			this.ProcessingVM.EncodeQueue.Remove(jobVM);

			this.PresetsVM.SaveUserPresets();
		}

		public void RefreshChapterMarkerUI()
		{
			this.NotifyPropertyChanged("ShowChapterMarkerUI");
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

				switch (this.VideoRangeType)
				{
					case VideoRangeType.Chapters:
						int selectionStart = this.SelectedTitle.Chapters.IndexOf(this.SelectedStartChapter);
						int selectionEnd = this.SelectedTitle.Chapters.IndexOf(this.SelectedEndChapter);

						for (int i = selectionStart; i <= selectionEnd; i++)
						{
							selectedTime += this.SelectedTitle.Chapters[i].Duration;
						}

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
				this.CleanupHandBrakeInstance(oldInstance);
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
						this.NotifyPropertyChanged("SourcePicked");

						if (this.ScanCancelled != null)
						{
							this.ScanCancelled(this, new EventArgs());
						}

						this.logger.Log("Scan cancelled");
					}
					else
					{
						this.SourceData = new VideoSource { Titles = this.scanInstance.Titles, FeatureTitle = this.scanInstance.FeatureTitle };

						if (jobVM != null)
						{
							this.ApplyEncodeJobChoices(jobVM);
						}

						if (jobVM == null && this.SelectedSource != null && (this.SelectedSource.Type == SourceType.File || this.SelectedSource.Type == SourceType.VideoFolder))
						{
							SourceHistory.AddToHistory(this.SourcePath);
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
				this.NotifyPropertyChanged("Titles");
				this.NotifyPropertyChanged("TitleVisible");
				this.NotifyPropertyChanged("HasVideoSource");
				this.NotifyPropertyChanged("SourceOptionsVisible");
			}
			else
			{
				this.sourceData = null;
				this.NotifyPropertyChanged("HasVideoSource");
				this.NotifyPropertyChanged("SourceOptionsVisible");
				this.NotifyPropertyChanged("CanEnqueue");
				this.NotifyPropertyChanged("SourcePicked");
				this.ScanError = true;
			}

			this.NotifyPropertyChanged("CanEnqueueMultipleTitles");
		}

		private void ClearVideoSource()
		{
			this.sourceData = null;
			this.SourceSelectionExpanded = false;
			this.NotifyPropertyChanged("HasVideoSource");
			this.NotifyPropertyChanged("SourceOptionsVisible");
			this.NotifyPropertyChanged("CanEnqueue");
			this.NotifyPropertyChanged("SourcePicked");
			PreviewViewModel.FindAndRefreshPreviews();
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
			EncodeJob job = jobVM.Job;

			// Title
			Title newTitle = this.sourceData.Titles.FirstOrDefault(t => t.TitleNumber == job.Title);
			if (newTitle == null)
			{
				newTitle = this.sourceData.Titles[0];
			}

			this.selectedTitle = newTitle;

			// Angle
			if (job.Angle <= this.selectedTitle.AngleCount)
			{
				this.angle = job.Angle;
			}
			else
			{
				this.angle = 0;
			}

			// Range
			this.videoRangeType = job.RangeType;
			switch (job.RangeType)
			{
				case VideoRangeType.Chapters:
					if (job.ChapterStart > this.selectedTitle.Chapters.Count ||
						job.ChapterEnd > this.selectedTitle.Chapters.Count)
					{
						this.selectedStartChapter = this.selectedTitle.Chapters[0];
						this.selectedEndChapter = this.selectedTitle.Chapters[this.selectedTitle.Chapters.Count - 1];
					}
					else
					{
						this.selectedStartChapter = this.selectedTitle.Chapters[job.ChapterStart - 1];
						this.selectedEndChapter = this.selectedTitle.Chapters[job.ChapterEnd - 1];
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

			// Encoding profile?!

			this.NotifyPropertyChanged("SourceIcon");
			this.NotifyPropertyChanged("SourceText");
			this.NotifyPropertyChanged("SelectedTitle");
			this.NotifyPropertyChanged("SelectedStartChapter");
			this.NotifyPropertyChanged("SelectedEndChapter");
			this.NotifyPropertyChanged("SecondsRangeStart");
			this.NotifyPropertyChanged("SecondsRangeEnd");
			this.NotifyPropertyChanged("FramesRangeStart");
			this.NotifyPropertyChanged("FramesRangeEnd");
			this.NotifyPropertyChanged("VideoRangeType");
			this.NotifyPropertyChanged("SubtitlesSummary");
			this.NotifyPropertyChanged("ChapterMarkersSummary");
			this.NotifyPropertyChanged("ShowChapterMarkerUI");
			this.NotifyPropertyChanged("Angle");
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
	}
}
