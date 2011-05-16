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
using HandBrake.SourceData;
using VidCoder.Model;
using VidCoder.Properties;
using VidCoder.Services;
using System.Diagnostics;
using System.Globalization;
using Microsoft.Practices.Unity;
using System.Threading;
using System.Text.RegularExpressions;

namespace VidCoder.ViewModel
{
	public class MainViewModel : ViewModelBase
	{
		public const int QueuedTabIndex = 0;
		public const int CompletedTabIndex = 1;

		private const double SecondsRangeBuffer = 0.25;

		private HandBrakeInstance scanInstance;

		private IUpdater updater = Unity.Container.Resolve<IUpdater>();
		private ILogger logger = Unity.Container.Resolve<ILogger>();
		private IProcessAutoPause autoPause = Unity.Container.Resolve<IProcessAutoPause>();

		private ObservableCollection<SourceOptionViewModel> sourceOptions;
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
		private string sourcePath;
		private string sourceName;

		private IDriveService driveService;
		private IList<DriveInformation> driveCollection;
		private bool scanningSource;
		private bool scanError;
		private double scanProgress;
		private bool scanCancelledFlag;

		private string outputPath;
		private bool manualOutputPath;
		private bool editingDestination;

		private List<Preset> builtInPresets;
		private ObservableCollection<PresetViewModel> allPresets;
		private PresetViewModel selectedPreset;

		private int selectedTabIndex;
		private ObservableCollection<EncodeJobViewModel> encodeQueue;
		private bool encoding;
		private bool paused;
		private bool encodeStopped;
		private int totalTasks;
		private int taskNumber;
		private bool encodeSpeedDetailsAvailable;
		private Stopwatch elapsedQueueEncodeTime;
		private long pollCount = 0;
		private string estimatedTimeRemaining;
		private double currentFps;
		private double averageFps;
		private double completedQueueWork;
		private double totalQueueCost;
		private double overallEncodeProgressFraction;
		private TaskbarItemProgressState encodeProgressState;
		private ObservableCollection<EncodeResultViewModel> completedJobs;

		private bool encodingWindowOpen;
		private bool previewWindowOpen;
		private bool logWindowOpen;

		private ICommand toggleSourceMenuCommand;
		private ICommand openSubtitlesDialogCommand;
		private ICommand openChaptersDialogCommand;
		private ICommand openAboutDialogCommand;
		private ICommand addTrackCommand;
		private ICommand openEncodingWindowCommand;
		private ICommand openPreviewWindowCommand;
		private ICommand openLogWindowCommand;
		private ICommand pickDefaultOutputFolderCommand;
		private ICommand pickOutputPathCommand;
		private ICommand encodeCommand;
		private ICommand cancelScanCommand;
		private ICommand openFileCommand;
		private ICommand openFolderCommand;
		private ICommand addToQueueCommand;
		private ICommand queueFilesCommand;
		private ICommand queueTitlesCommand;
		private ICommand customizeQueueColumnsCommand;
		private ICommand clearCompletedCommand;
		private ICommand pauseCommand;
		private ICommand stopEncodeCommand;
		private ICommand importPresetCommand;
		private ICommand exportPresetCommand;
		private ICommand openOptionsCommand;
		private ICommand openHomepageCommand;
		private ICommand reportBugCommand;

		public event EventHandler<EventArgs<string>> AnimationStarted;
		public event EventHandler ScanCancelled;

		public MainViewModel()
		{
			Unity.Container.RegisterInstance(this);

			// Upgrade from previous user settings.
			if (Settings.Default.ApplicationVersion != Utilities.CurrentVersion)
			{
				Settings.Default.Upgrade();
				Settings.Default.ApplicationVersion = Utilities.CurrentVersion;
				Settings.Default.Save();
			}

			this.JobCreationAvailable = false;
			this.updater.HandlePendingUpdate();
			this.updater.CheckUpdates();

			this.sourceOptions = new ObservableCollection<SourceOptionViewModel>
			{
				new SourceOptionViewModel(new SourceOption { Type = SourceType.File }),
				new SourceOptionViewModel(new SourceOption { Type = SourceType.VideoFolder })
			};

			this.builtInPresets = Presets.BuiltInPresets;
			List<Preset> userPresets = Presets.UserPresets;

			this.allPresets = new ObservableCollection<PresetViewModel>();
			var unmodifiedPresets = userPresets.Where(preset => !preset.IsModified);
			Preset modifiedPreset = userPresets.FirstOrDefault(preset => preset.IsModified);
			int modifiedPresetIndex = -1;

			foreach (Preset userPreset in unmodifiedPresets)
			{
				PresetViewModel presetVM;
				if (modifiedPreset != null && modifiedPreset.Name == userPreset.Name)
				{
					modifiedPresetIndex = this.allPresets.Count;
					presetVM = new PresetViewModel(modifiedPreset);
					presetVM.OriginalProfile = userPreset.EncodingProfile;
				}
				else
				{
					presetVM = new PresetViewModel(userPreset);
				}

				this.allPresets.Add(presetVM);
			}

			foreach (Preset builtInPreset in this.builtInPresets)
			{
				PresetViewModel presetVM;
				if (modifiedPreset != null && modifiedPreset.Name == builtInPreset.Name)
				{
					presetVM = new PresetViewModel(modifiedPreset);
					presetVM.OriginalProfile = builtInPreset.EncodingProfile;
				}
				else
				{
					presetVM = new PresetViewModel(builtInPreset);
				}

				this.allPresets.Add(presetVM);
			}

			this.useDefaultChapterNames = true;

			this.encodeQueue = new ObservableCollection<EncodeJobViewModel>();
			this.encodeQueue.CollectionChanged += this.OnEncodeQueueChanged;
			List<EncodeJob> encodeJobs = EncodeJobsPersist.EncodeJobs;
			foreach (EncodeJob job in encodeJobs)
			{
				this.encodeQueue.Add(new EncodeJobViewModel(job));
			}

			this.autoPause.PauseEncoding += new EventHandler(this.AutoPauseEncoding);
			this.autoPause.ResumeEncoding += new EventHandler(this.AutoResumeEncoding);

			// Always select the modified preset if it exists.
			// Otherwise, choose the last selected preset.
			int presetIndex;
			if (modifiedPresetIndex >= 0)
			{
				presetIndex = modifiedPresetIndex;
			}
			else
			{
				presetIndex = Settings.Default.LastPresetIndex;
			}

			if (presetIndex >= this.allPresets.Count)
			{
				presetIndex = 0;
			}

			this.SelectedPreset = this.allPresets[presetIndex];

			this.completedJobs = new ObservableCollection<EncodeResultViewModel>();

			this.driveService = Unity.Container.Resolve<IDriveService>();

			this.DriveCollection = this.driveService.GetDriveInformation();

			this.audioChoices = new ObservableCollection<AudioChoiceViewModel>();
		}

		public HandBrakeInstance ScanInstance
		{
			get
			{
				return this.scanInstance;
			}
		}

		public EncodeJobViewModel CurrentJob
		{
			get
			{
				return this.EncodeQueue[0];
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
			IList<DriveInformation> newDriveCollection = this.driveService.GetDriveInformation();

			// If our current drive is not present
			if (this.SelectedSource.Type == SourceType.Dvd)
			{
				string currentRootDirectory = this.SelectedSource.DriveInfo.RootDirectory;
				bool currentDrivePresent = newDriveCollection.Any(driveInfo => driveInfo.RootDirectory == currentRootDirectory);

				if (this.SelectedSource.DriveInfo.Empty && currentDrivePresent)
				{
					this.SelectedSource.DriveInfo.Empty = false;
					this.SelectedSource.DriveInfo.VolumeLabel = newDriveCollection.Single(driveInfo => driveInfo.RootDirectory == currentRootDirectory).VolumeLabel;
					this.SetSourceFromDvd(this.SelectedSource.DriveInfo);
					this.NotifyPropertyChanged("HasVideoSource");
				}
				else if (!this.SelectedSource.DriveInfo.Empty && !currentDrivePresent)
				{
					DriveInformation emptyDrive = this.SelectedSource.DriveInfo;
					emptyDrive.Empty = true;

					newDriveCollection.Add(emptyDrive);

					this.sourceData = null;
					this.SelectedTitle = null;
					this.NotifyPropertyChanged("HasVideoSource");
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

			string videoFile = FileService.Instance.GetFileNameLoad(Settings.Default.LastInputFileFolder, null, null);

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

			this.sourceName = Path.GetFileNameWithoutExtension(videoFile);
			this.StartScan(videoFile);

			this.sourcePath = videoFile;
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
				Settings.Default.LastVideoTSFolder = folderPath;
				Settings.Default.Save();
				DirectoryInfo parentDirectory = Directory.GetParent(folderPath);
				if (parentDirectory == null || parentDirectory.Root.FullName == parentDirectory.FullName)
				{
					this.sourceName = "VideoFolder";
				}
				else
				{
					this.sourceName = parentDirectory.Name;
				}

				this.StartScan(folderPath);

				this.sourcePath = folderPath;
				this.SelectedSource = new SourceOption { Type = SourceType.VideoFolder };

				return true;
			}

			return false;
		}

		public bool SetSourceFromDvd(DriveInformation driveInfo)
		{
			if (this.SourceSelectionExpanded)
			{
				this.SourceSelectionExpanded = false;
			}

			this.sourceName = driveInfo.VolumeLabel;

			if (driveInfo.DiscType == DiscType.Dvd)
			{
				this.sourcePath = Path.Combine(driveInfo.RootDirectory, "VIDEO_TS");
			}
			else
			{
				this.sourcePath = driveInfo.RootDirectory;
			}

			this.SelectedSource = new SourceOption { Type = SourceType.Dvd, DriveInfo = driveInfo };
			this.StartScan(this.sourcePath);

			return true;
		}

		public void OnLoaded()
		{
			bool windowOpened = false;

			if (Settings.Default.EncodingWindowOpen)
			{
				this.OpenEncodingWindow();
				windowOpened = true;
			}

			if (Settings.Default.PreviewWindowOpen)
			{
				this.OpenPreviewWindow();
				windowOpened = true;
			}

			if (Settings.Default.LogWindowOpen)
			{
				this.OpenLogWindow();
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
			if (this.Encoding)
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
			if (this.Encoding)
			{
				// If so, stop it.
				this.CurrentJob.HandBrakeInstance.StopEncode();
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

			this.CleanOldLogs();
			this.CleanPreviewFileCache();

			this.updater.PromptToApplyUpdate();

			this.logger.Dispose();

			return true;
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
						return this.sourcePath;
					case SourceType.VideoFolder:
						return this.sourcePath;
					case SourceType.Dvd:
						return this.SelectedSource.DriveInfo.RootDirectory + " - " + this.SelectedSource.DriveInfo.VolumeLabel;
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
						this.manualOutputPath = false;
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

				PreviewViewModel previewWindow = WindowManager.FindWindow(typeof(PreviewViewModel)) as PreviewViewModel;
				if (previewWindow != null)
				{
					previewWindow.RequestRefreshPreviews();
				}

				// Custom chapter names are thrown out when switching titles.
				this.CustomChapterNames = null;

				this.NotifyPropertyChanged("SubtitlesSummary");
				CommandManager.InvalidateRequerySuggested();
				this.GenerateOutputFileName();

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
				return this.SelectedPreset != null && this.SelectedPreset.Preset.EncodingProfile.IncludeChapterMarkers;
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

				this.GenerateOutputFileName();
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

				this.GenerateOutputFileName();

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

				this.GenerateOutputFileName();

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

				this.GenerateOutputFileName();

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

				this.GenerateOutputFileName();

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

				this.GenerateOutputFileName();

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

				this.GenerateOutputFileName();

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

		public bool OutputFolderChosen
		{
			get
			{
				return !Settings.Default.AutoNameOutputFiles || !string.IsNullOrEmpty(Settings.Default.AutoNameOutputFolder);
			}
		}

		public string OutputPath
		{
			get
			{
				return this.outputPath;
			}

			set
			{
				this.outputPath = value;
				this.NotifyPropertyChanged("OutputPath");
			}
		}

		public bool EditingDestination
		{
			get
			{
				return this.editingDestination;
			}

			set
			{
				this.editingDestination = value;
				this.NotifyPropertyChanged("EditingDestination");
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

		public ObservableCollection<PresetViewModel> AllPresets
		{
			get
			{
				return this.allPresets;
			}
		}

		public PresetViewModel SelectedPreset
		{
			get
			{
				return this.selectedPreset;
			}

			set
			{
				if (value == null)
				{
					return;
				}

				PresetViewModel previouslySelectedPreset = this.selectedPreset;
				bool changeSelectedPreset = true;

				if (this.selectedPreset != null && this.selectedPreset.Preset.IsModified)
				{
					MessageBoxResult dialogResult = Utilities.MessageBox.Show(this, "Do you want to save changes to your current preset?", "Save current preset?", MessageBoxButton.YesNoCancel);
					if (dialogResult == MessageBoxResult.Yes)
					{
						this.SavePreset();
					}
					else if (dialogResult == MessageBoxResult.No)
					{
						this.RevertPreset(userInitiated: false);
					}
					else if (dialogResult == MessageBoxResult.Cancel)
					{
						// Queue up an action to switch back to this preset.
						int currentPresetIndex = this.AllPresets.IndexOf(this.selectedPreset);

						DispatchService.BeginInvoke(() =>
						{
							this.SelectedPreset = this.AllPresets[currentPresetIndex];
						});

						changeSelectedPreset = false;
					}
				}

				this.selectedPreset = value;

				if (changeSelectedPreset)
				{
					this.GenerateOutputFileName();

					var encodingWindow = WindowManager.FindWindow(typeof(EncodingViewModel)) as EncodingViewModel;
					if (encodingWindow != null)
					{
						encodingWindow.EditingPreset = this.selectedPreset.Preset;
					}

					var previewWindow = WindowManager.FindWindow(typeof(PreviewViewModel)) as PreviewViewModel;
					if (previewWindow != null)
					{
						previewWindow.RequestRefreshPreviews();
					}

					this.NotifyPropertyChanged("SelectedPreset");
				}

				this.NotifyPropertyChanged("ShowChapterMarkerUI");
				Properties.Settings.Default.LastPresetIndex = this.AllPresets.IndexOf(this.selectedPreset);
			}
		}

		public int SelectedTabIndex
		{
			get
			{
				return this.selectedTabIndex;
			}

			set
			{
				this.selectedTabIndex = value;
				this.NotifyPropertyChanged("SelectedTabIndex");
			}
		}

		public string QueuedTabHeader
		{
			get
			{
				if (this.EncodeQueue.Count == 0)
				{
					return "Queued";
				}

				return "Queued (" + this.EncodeQueue.Count + ")";
			}
		}

		public string CompletedTabHeader
		{
			get
			{
				return "Completed (" + this.CompletedJobs.Count + ")";
			}
		}

		public ObservableCollection<EncodeJobViewModel> EncodeQueue
		{
			get
			{
				return this.encodeQueue;
			}
		}

		public ObservableCollection<EncodeResultViewModel> CompletedJobs
		{
			get
			{
				return this.completedJobs;
			}
		}

		public int CompletedItemsCount
		{
			get
			{
				return this.completedJobs.Count();
			}
		}

		public bool CanEnqueue
		{
			get
			{
				return this.HasVideoSource && Utilities.IsValidFullPath(this.OutputPath);
			}
		}

		public string EnqueueToolTip
		{
			get
			{
				if (string.IsNullOrEmpty(Settings.Default.AutoNameOutputFolder))
				{
					return "Please choose a destination directory before adding items to the queue.";
				}

				return null;
			}
		}

		public bool CanEncode
		{
			get
			{
				return this.EncodeQueue.Count > 0 || this.CanEnqueue;
			}
		}

		public string EncodeToolTip
		{
			get
			{
				if (string.IsNullOrEmpty(Settings.Default.AutoNameOutputFolder))
				{
					return "Please choose a destination directory before encoding.";
				}

				return null;
			}
		}

		public bool Encoding
		{
			get
			{
				return this.encoding;
			}

			set
			{
				this.encoding = value;

				if (value)
				{
					Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.BelowNormal;
					SystemSleepManagement.PreventSleep();
					this.elapsedQueueEncodeTime = Stopwatch.StartNew();
				}
				else
				{
					Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.Normal;
					this.EncodeSpeedDetailsAvailable = false;
					SystemSleepManagement.AllowSleep();
					this.elapsedQueueEncodeTime.Stop();
				}

				this.NotifyPropertyChanged("PauseVisible");
				this.NotifyPropertyChanged("Encoding");
				this.NotifyPropertyChanged("EncodeButtonText");
			}
		}

		public bool Paused
		{
			get
			{
				return this.paused;
			}

			set
			{
				this.paused = value;

				if (this.elapsedQueueEncodeTime != null)
				{
					if (value)
					{
						this.elapsedQueueEncodeTime.Stop();
					}
					else
					{
						this.elapsedQueueEncodeTime.Start();
					}
				}

				this.NotifyPropertyChanged("PauseVisible");
				this.NotifyPropertyChanged("ProgressBarColor");
				this.NotifyPropertyChanged("Paused");
			}
		}

		public string EncodeButtonText
		{
			get
			{
				if (this.Encoding)
				{
					return "Resume";
				}
				else
				{
					return "Encode";
				}
			}
		}

		public bool PauseVisible
		{
			get
			{
				return this.Encoding && !this.Paused;
			}
		}

		public bool CanEnqueueMultipleTitles
		{
			get
			{
				return !string.IsNullOrEmpty(Settings.Default.AutoNameOutputFolder) && this.HasVideoSource && this.SourceData.Titles.Count > 1;
			}
		}

		public string TaskDetails
		{
			get
			{
				return this.taskNumber + "/" + this.totalTasks;
			}
		}

		public bool EncodeSpeedDetailsAvailable
		{
			get
			{
				return this.encodeSpeedDetailsAvailable;
			}

			set
			{
				this.encodeSpeedDetailsAvailable = value;
				this.NotifyPropertyChanged("EncodeSpeedDetailsAvailable");
			}
		}

		public string EstimatedTimeRemaining
		{
			get
			{
				return this.estimatedTimeRemaining;
			}

			set
			{
				this.estimatedTimeRemaining = value;
				this.NotifyPropertyChanged("EstimatedTimeRemaining");
			}
		}

		public double CurrentFps
		{
			get
			{
				return this.currentFps;
			}

			set
			{
				this.currentFps = value;
				this.NotifyPropertyChanged("CurrentFps");
			}
		}

		public double AverageFps
		{
			get
			{
				return this.averageFps;
			}

			set
			{
				this.averageFps = value;
				this.NotifyPropertyChanged("AverageFps");
			}
		}

		public double OverallEncodeProgressFraction
		{
			get
			{
				return this.overallEncodeProgressFraction;
			}

			set
			{
				this.overallEncodeProgressFraction = value;
				this.NotifyPropertyChanged("OverallEncodeProgressPercent");
				this.NotifyPropertyChanged("OverallEncodeProgressFraction");
			}
		}

		public double OverallEncodeProgressPercent
		{
			get
			{
				return this.overallEncodeProgressFraction * 100;
			}
		}

		public TaskbarItemProgressState EncodeProgressState
		{
			get
			{
				return this.encodeProgressState;
			}

			set
			{
				this.encodeProgressState = value;
				this.NotifyPropertyChanged("EncodeProgressState");
			}
		}

		public System.Windows.Media.Brush ProgressBarColor
		{
			get
			{
				if (this.Paused)
				{
					return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 230, 0));
				}
				else
				{
					return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 200, 0));
				}
			}
		}

		public bool EncodingWindowOpen
		{
			get
			{
				return this.encodingWindowOpen;
			}

			set
			{
				this.encodingWindowOpen = value;
				this.NotifyPropertyChanged("EncodingWindowOpen");
			}
		}

		public bool PreviewWindowOpen
		{
			get
			{
				return this.previewWindowOpen;
			}

			set
			{
				this.previewWindowOpen = value;
				this.NotifyPropertyChanged("PreviewWindowOpen");
			}
		}

		public bool LogWindowOpen
		{
			get
			{
				return this.logWindowOpen;
			}

			set
			{
				this.logWindowOpen = value;
				this.NotifyPropertyChanged("LogWindowOpen");
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

		public ICommand OpenEncodingWindowCommand
		{
			get
			{
				if (this.openEncodingWindowCommand == null)
				{
					this.openEncodingWindowCommand = new RelayCommand(param =>
					{
						this.OpenEncodingWindow();
					});
				}

				return this.openEncodingWindowCommand;
			}
		}

		public ICommand OpenPreviewWindowCommand
		{
			get
			{
				if (this.openPreviewWindowCommand == null)
				{
					this.openPreviewWindowCommand = new RelayCommand(param =>
					{
						this.OpenPreviewWindow();
					});
				}

				return this.openPreviewWindowCommand;
			}
		}

		public ICommand OpenLogWindowCommand
		{
			get
			{
				if (this.openLogWindowCommand == null)
				{
					this.openLogWindowCommand = new RelayCommand(param =>
					{
						this.OpenLogWindow();
					});
				}

				return this.openLogWindowCommand;
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

		public ICommand PickDefaultOutputFolderCommand
		{
			get
			{
				if (this.pickDefaultOutputFolderCommand == null)
				{
					this.pickDefaultOutputFolderCommand = new RelayCommand(param =>
					{
						string newOutputFolder = FileService.Instance.GetFolderName(null, "Choose the output directory for encoded video files.");

						if (newOutputFolder != null)
						{
							Properties.Settings.Default.AutoNameOutputFolder = newOutputFolder;
							Properties.Settings.Default.Save();
							this.NotifyPropertyChanged("OutputFolderChosen");
							this.NotifyPropertyChanged("CanEnqueueMultipleTitles");
							this.NotifyPropertyChanged("EnqueueToolTip");
							this.NotifyPropertyChanged("EncodeToolTip");

							this.GenerateOutputFileName();
						}
					});
				}

				return this.pickDefaultOutputFolderCommand;
			}
		}

		public ICommand PickOutputPathCommand
		{
			get
			{
				if (this.pickOutputPathCommand == null)
				{
					this.pickOutputPathCommand = new RelayCommand(param =>
					{
						string newOutputPath = FileService.Instance.GetFileNameSave(Settings.Default.LastOutputFolder);
						this.SetManualOutputPath(newOutputPath, this.OutputPath);
					},
					param =>
					{
						return this.OutputFolderChosen;
					});
				}

				return this.pickOutputPathCommand;
			}
		}

		public ICommand EncodeCommand
		{
			get
			{
				if (this.encodeCommand == null)
				{
					this.encodeCommand = new RelayCommand(param =>
					{
						if (this.Encoding)
						{
							this.ResumeEncoding();
							this.autoPause.ReportResume();
						}
						else
						{
							if (this.EncodeQueue.Count == 0)
							{
								this.EncodeQueue.Add(new EncodeJobViewModel(this.EncodeJob));
								this.EncodeQueue[0].HandBrakeInstance = this.scanInstance;
							}

							this.SelectedTabIndex = QueuedTabIndex;

							this.StartEncodeQueue();
						}
					},
					param =>
					{
						return this.CanEncode;
					});
				}

				return this.encodeCommand;
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
					});
				}

				return this.openFolderCommand;
			}
		}

		public ICommand AddToQueueCommand
		{
			get
			{
				if (this.addToQueueCommand == null)
				{
					this.addToQueueCommand = new RelayCommand(param =>
					{
						// Check for previous items in queue with this name.
						bool foundExisting = false;
						foreach (EncodeJobViewModel job in this.EncodeQueue)
						{
							if (job.Job.OutputPath.ToLowerInvariant() == this.OutputPath.ToLowerInvariant())
							{
								foundExisting = true;
								break;
							}
						}

						if (foundExisting)
						{
							MessageBoxResult result = Utilities.MessageBox.Show(this, "There is already a queue item for this destination path." + Environment.NewLine + Environment.NewLine +
								"If you continue, the encode will be overwritten. Do you wish to continue?", "Warning", MessageBoxButton.YesNo);

							if (result == MessageBoxResult.No)
							{
								return;
							}
						}

						var newEncodeJobVM = new EncodeJobViewModel(this.EncodeJob);
						newEncodeJobVM.HandBrakeInstance = this.scanInstance;

						this.Queue(newEncodeJobVM);

					},
					param =>
					{
						return this.CanEnqueue;
					});
				}

				return this.addToQueueCommand;
			}
		}

		public ICommand QueueFilesCommand
		{
			get
			{
				if (this.queueFilesCommand == null)
				{
					this.queueFilesCommand = new RelayCommand(param =>
					{
						IList<string> fileNames = FileService.Instance.GetFileNames(Settings.Default.LastInputFileFolder);
						if (fileNames != null && fileNames.Count > 0)
						{
							Settings.Default.LastInputFileFolder = Path.GetDirectoryName(fileNames[0]);
							Settings.Default.Save();

							this.QueueMultiple(fileNames);
						}
					},
					param =>
					{
						return !string.IsNullOrEmpty(Settings.Default.AutoNameOutputFolder);
					});
				}

				return this.queueFilesCommand;
			}
		}

		public ICommand QueueTitlesCommand
		{
			get
			{
				if (this.queueTitlesCommand == null)
				{
					this.queueTitlesCommand = new RelayCommand(param =>
					{
						var queueTitlesDialog = new QueueTitlesDialogViewModel(this.SourceData.Titles);
						WindowManager.OpenDialog(queueTitlesDialog, this);

						if (queueTitlesDialog.DialogResult)
						{
							int currentTitleNumber = queueTitlesDialog.TitleStartOverride;

							// Queue the selected titles
							List<Title> titlesToQueue = queueTitlesDialog.CheckedTitles;
							foreach (Title title in titlesToQueue)
							{
								// Use current subtitle and audio track choices for each queued title.
								Subtitles subtitles = new Subtitles();
								subtitles.SrtSubtitles = new List<SrtSubtitle>();
								subtitles.SourceSubtitles = new List<SourceSubtitle>();

								foreach (SourceSubtitle sourceSubtitle in this.CurrentSubtitles.SourceSubtitles)
								{
									if (sourceSubtitle.TrackNumber == 0)
									{
										subtitles.SourceSubtitles.Add(sourceSubtitle.Clone());
									}
									else if (
										title.Subtitles.Count > sourceSubtitle.TrackNumber - 1 &&
										this.SelectedTitle.Subtitles[sourceSubtitle.TrackNumber - 1].LanguageCode == title.Subtitles[sourceSubtitle.TrackNumber - 1].LanguageCode)
									{
										subtitles.SourceSubtitles.Add(sourceSubtitle.Clone());
									}
								}

								List<int> currentAudioChoices = new List<int>();
								foreach (AudioChoiceViewModel audioVM in this.AudioChoices)
								{
									int audioIndex = audioVM.SelectedIndex;

									if (title.AudioTracks.Count > audioIndex && this.SelectedTitle.AudioTracks[audioIndex].LanguageCode == title.AudioTracks[audioIndex].LanguageCode)
									{
										currentAudioChoices.Add(audioIndex + 1);
									}
								}

								// If we didn't manage to match any existing audio tracks, use the first audio track.
								if (this.AudioChoices.Count > 0 && currentAudioChoices.Count == 0)
								{
									currentAudioChoices.Add(1);
								}

								EncodingProfile profile = this.SelectedPreset.Preset.EncodingProfile;
								string queueSourceName = this.sourceName;
								if (this.SelectedSource.Type == SourceType.Dvd)
								{
									queueSourceName = this.TranslateDvdSourceName(queueSourceName);
								}

								int titleNumber = title.TitleNumber;
								if (queueTitlesDialog.TitleStartOverrideEnabled)
								{
									titleNumber = currentTitleNumber;
									currentTitleNumber++;
								}

								string queueOutputFileName = this.BuildOutputFileName(
									this.sourcePath,
									queueSourceName,
									titleNumber,
									title.Chapters.Count);

								string extension = this.GetOutputExtension(subtitles, title);
								string queueOutputPath = this.BuildOutputPath(queueOutputFileName, extension, sourcePath: null);

								var job = new EncodeJob
								{
									SourcePath = this.sourcePath,
									OutputPath = queueOutputPath,
									EncodingProfile = profile.Clone(),
									Title = title.TitleNumber,
									ChapterStart = 1,
									ChapterEnd = title.Chapters.Count,
									ChosenAudioTracks = currentAudioChoices,
									Subtitles = subtitles,
									UseDefaultChapterNames = true,
									Length = title.Duration
								};

								var jobVM = new EncodeJobViewModel(job);
								jobVM.HandBrakeInstance = this.ScanInstance;

								this.Queue(jobVM);
							}
						}
					},
					param =>
					{
						return this.CanEnqueueMultipleTitles;
					});
				}

				return this.queueTitlesCommand;
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

		public ICommand ClearCompletedCommand
		{
			get
			{
				if (this.clearCompletedCommand == null)
				{
					this.clearCompletedCommand = new RelayCommand(param =>
					{
						this.CompletedJobs.Clear();
						this.NotifyPropertyChanged("CompletedItemsCount");
						this.NotifyPropertyChanged("CompletedTabHeader");
					});
				}

				return this.clearCompletedCommand;
			}
		}

		public ICommand PauseCommand
		{
			get
			{
				if (this.pauseCommand == null)
				{
					this.pauseCommand = new RelayCommand(param =>
					{
						this.PauseEncoding();
						this.autoPause.ReportPause();
					});
				}

				return this.pauseCommand;
			}
		}

		public ICommand StopEncodeCommand
		{
			get
			{
				if (this.stopEncodeCommand == null)
				{
					this.stopEncodeCommand = new RelayCommand(param =>
					{
						// Signify that we stopped the encode manually rather than it completing.
						this.encodeStopped = true;
						this.CurrentJob.HandBrakeInstance.StopEncode();

					});
				}

				return this.stopEncodeCommand;
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
						string presetFileName = FileService.Instance.GetFileNameLoad(null, "xml", "XML Files|*.xml");
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
						Unity.Container.Resolve<IPresetImportExport>().ExportPreset(this.SelectedPreset.Preset);
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
							this.GenerateOutputFileName();
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
				return new EncodeJob
				{
					SourcePath = this.sourcePath,
					OutputPath = this.OutputPath,
					EncodingProfile = this.SelectedPreset.Preset.EncodingProfile.Clone(),
					Title = this.SelectedTitle.TitleNumber,
					Angle = this.Angle,
					RangeType = this.VideoRangeType,
					ChapterStart = this.SelectedStartChapter.ChapterNumber,
					ChapterEnd = this.SelectedEndChapter.ChapterNumber,
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

		public void StartEncodeQueue()
		{
			this.EncodeProgressState = TaskbarItemProgressState.Normal;
			this.logger.Log("Starting queue");

			this.totalTasks = this.EncodeQueue.Count;
			this.taskNumber = 0;

			this.completedQueueWork = 0.0;
			this.totalQueueCost = 0.0;
			foreach (EncodeJobViewModel jobVM in this.EncodeQueue)
			{
				this.totalQueueCost += jobVM.Cost;
			}

			this.OverallEncodeProgressFraction = 0;
			this.pollCount = 0;
			this.Encoding = true;
			this.Paused = false;
			this.encodeStopped = false;
			this.autoPause.ReportStart();

			this.EncodeNextJob();
		}

		private void EncodeNextJob()
		{
			this.taskNumber++;
			this.NotifyPropertyChanged("TaskDetails");

			if (this.CurrentJob.HandBrakeInstance == null)
			{
				HandBrakeInstance onDemandInstance = new HandBrakeInstance();
				onDemandInstance.Initialize(Settings.Default.LogVerbosity);
				onDemandInstance.ScanCompleted += (o, e) =>
				{
					this.CurrentJob.HandBrakeInstance = onDemandInstance;
					Title encodeTitle = onDemandInstance.Titles.FirstOrDefault(title => title.TitleNumber == this.CurrentJob.Job.Title);

					if (encodeTitle != null)
					{
						this.StartEncode();
					}
					else
					{
						this.OnEncodeCompleted(this, new EncodeCompletedEventArgs { Error = true });
					}
				};

				onDemandInstance.StartScan(this.CurrentJob.Job.SourcePath, Settings.Default.PreviewCount, this.CurrentJob.Job.Title);
			}
			else
			{
				this.StartEncode();
			}
		}

		private void StartEncode()
		{
			EncodeJob job = this.CurrentJob.Job;

			this.logger.Log("Starting job " + this.taskNumber + "/" + this.totalTasks);
			this.logger.Log("  Path: " + job.SourcePath);
			this.logger.Log("  Title: " + job.Title);
			this.logger.Log("  Chapters: " + job.ChapterStart + "-" + job.ChapterEnd);
			this.CurrentJob.HandBrakeInstance.EncodeProgress += this.OnEncodeProgress;
			this.CurrentJob.HandBrakeInstance.EncodeCompleted += this.OnEncodeCompleted;

			string destinationDirectory = Path.GetDirectoryName(this.CurrentJob.Job.OutputPath);
			if (!Directory.Exists(destinationDirectory))
			{
				try
				{
					Directory.CreateDirectory(destinationDirectory);
				}
				catch (IOException exception)
				{
					Utilities.MessageBox.Show(
						"Could not create output directory. Error details: " + Environment.NewLine + Environment.NewLine + exception.ToString(),
						"Error creating directory",
						MessageBoxButton.OK,
						MessageBoxImage.Error);
				}
			}

			this.EncodeQueue[0].ReportEncodeStart(this.totalTasks == 1);
			this.CurrentJob.HandBrakeInstance.StartEncode(this.CurrentJob.Job);
		}

		private void OnEncodeProgress(object sender, EncodeProgressEventArgs e)
		{
			if (this.EncodeQueue.Count == 0)
			{
				return;
			}

			EncodeJob currentJob = this.EncodeQueue[0].Job;
			double passCost = currentJob.Length.TotalSeconds;
			double scanPassCost = passCost / EncodeJobViewModel.SubtitleScanCostFactor;

			double currentJobCompletedWork = 0.0;

			if (this.EncodeQueue[0].SubtitleScan)
			{
				switch (e.Pass)
				{
					case -1:
						currentJobCompletedWork += scanPassCost * e.FractionComplete;
						break;
					case 1:
						currentJobCompletedWork += scanPassCost;
						currentJobCompletedWork += passCost * e.FractionComplete;
						break;
					case 2:
						currentJobCompletedWork += scanPassCost;
						currentJobCompletedWork += passCost;
						currentJobCompletedWork += passCost * e.FractionComplete;
						break;
					default:
						break;
				}
			}
			else
			{
				switch (e.Pass)
				{
					case 1:
						currentJobCompletedWork += passCost * e.FractionComplete;
						break;
					case 2:
						currentJobCompletedWork += passCost;
						currentJobCompletedWork += passCost * e.FractionComplete;
						break;
					default:
						break;
				}
			}

			double totalCompletedWork = this.completedQueueWork + currentJobCompletedWork;

			this.OverallEncodeProgressFraction = totalCompletedWork / this.totalQueueCost;
			double overallWorkCompletionRate = totalCompletedWork / this.elapsedQueueEncodeTime.Elapsed.TotalSeconds;

			// Only update encode time every 5th update.
			if (Interlocked.Increment(ref this.pollCount) % 5 == 1)
			{
				if (this.elapsedQueueEncodeTime != null && this.elapsedQueueEncodeTime.Elapsed.TotalSeconds > 0.5 && this.OverallEncodeProgressFraction != 0.0)
				{
					if (this.OverallEncodeProgressFraction == 1.0)
					{
						this.EstimatedTimeRemaining = Utilities.FormatTimeSpan(TimeSpan.Zero);
					}
					else
					{
						TimeSpan eta = TimeSpan.FromSeconds((long)(((1.0 - this.OverallEncodeProgressFraction) * this.elapsedQueueEncodeTime.Elapsed.TotalSeconds) / this.OverallEncodeProgressFraction));
						this.EstimatedTimeRemaining = Utilities.FormatTimeSpan(eta);
					}

					double currentJobRemainingWork = this.EncodeQueue[0].Cost - currentJobCompletedWork;

					TimeSpan currentJobEta =
						TimeSpan.FromSeconds(currentJobRemainingWork / overallWorkCompletionRate);
					this.EncodeQueue[0].Eta = currentJobEta;
				}
			}

			this.EncodeQueue[0].PercentComplete = (int)((currentJobCompletedWork / this.EncodeQueue[0].Cost) * 100.0);

			if (e.EstimatedTimeLeft >= TimeSpan.Zero)
			{
				this.CurrentFps = Math.Round(e.CurrentFrameRate, 1);
				this.AverageFps = Math.Round(e.AverageFrameRate, 1);
				this.EncodeSpeedDetailsAvailable = true;
			}
		}

		private void OnEncodeCompleted(object sender, EncodeCompletedEventArgs e)
		{
			DispatchService.Invoke(() =>
			{
				// Unregister from events. This instance may be used again.
				this.EncodeQueue[0].HandBrakeInstance.EncodeProgress -= this.OnEncodeProgress;
				this.EncodeQueue[0].HandBrakeInstance.EncodeCompleted -= this.OnEncodeCompleted;

				if (this.encodeStopped)
				{
					// If the encode was stopped manually
					this.StopEncodingAndReport();
					this.EncodeQueue[0].ReportEncodeEnd();

					if (this.totalTasks == 1)
					{
						this.EncodeQueue.Clear();
					}

					this.logger.Log("Encoding stopped");
				}
				else
				{
					// If the encode completed successfully
					this.completedQueueWork += this.EncodeQueue[0].Cost;

					var outputFileInfo = new FileInfo(this.CurrentJob.Job.OutputPath);
					bool succeeded = true;
					if (e.Error)
					{
						succeeded = false;
						this.logger.Log("Encode failed. HandBrake reported an error.");
					}
					else if (!outputFileInfo.Exists)
					{
						succeeded = false;
						this.logger.Log("Encode failed. HandBrake reported no error but the expected output file was not found.");
					}
					else if (outputFileInfo.Length == 0)
					{
						succeeded = false;
						this.logger.Log("Encode failed. HandBrake reported no error but the output file was empty.");
					}

					this.CompletedJobs.Add(new EncodeResultViewModel(
						new EncodeResult
						{
							Destination = this.CurrentJob.Job.OutputPath,
							Succeeded = succeeded,
							EncodeTime = this.CurrentJob.EncodeTime
						}));
					this.NotifyPropertyChanged("CompletedItemsCount");
					this.NotifyPropertyChanged("CompletedTabHeader");

					HandBrakeInstance finishedInstance = this.EncodeQueue[0].HandBrakeInstance;
					this.EncodeQueue.RemoveAt(0);
					this.NotifyPropertyChanged("QueuedTabHeader");

					this.CleanupHandBrakeInstance(finishedInstance);

					this.logger.Log("Job completed");

					if (this.EncodeQueue.Count == 0)
					{
						this.SelectedTabIndex = CompletedTabIndex;
						this.StopEncodingAndReport();

						this.logger.Log("Queue completed");
						this.logger.Log("");
					}
					else
					{
						this.EncodeNextJob();
					}
				}
			});
		}

		private void PauseEncoding()
		{
			this.CurrentJob.HandBrakeInstance.PauseEncode();
			this.EncodeProgressState = TaskbarItemProgressState.Paused;
			this.CurrentJob.ReportEncodePause();

			this.Paused = true;
		}

		private void ResumeEncoding()
		{
			this.CurrentJob.HandBrakeInstance.ResumeEncode();
			this.EncodeProgressState = TaskbarItemProgressState.Normal;
			this.CurrentJob.ReportEncodeResume();

			this.Paused = false;
		}

		private void StopEncodingAndReport()
		{
			this.EncodeProgressState = TaskbarItemProgressState.None;
			this.Encoding = false;
			this.autoPause.ReportStop();
		}

		private void AutoPauseEncoding(object sender, EventArgs e)
		{
			DispatchService.Invoke(() =>
			{
				if (this.Encoding && !this.Paused)
				{
					this.PauseEncoding();
				}
			});
		}

		private void AutoResumeEncoding(object sender, EventArgs e)
		{
			DispatchService.Invoke(() =>
			{
				if (this.Encoding && this.Paused)
				{
					this.ResumeEncoding();
				}
			});
		}

		/// <summary>
		/// Cleans up the given HandBrake instance if it's not being used anymore.
		/// </summary>
		/// <param name="instance">The instance to clean up.</param>
		private void CleanupHandBrakeInstance(HandBrakeInstance instance)
		{
			foreach (EncodeJobViewModel encodeJobVM in this.EncodeQueue)
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

		private void OnEncodeQueueChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
		{
			this.SaveEncodeQueue();
		}

		public void QueueMultiple(IEnumerable<string> filesToQueue)
		{
			// Don't queue anything if we don't know where the output files will go.
			if (string.IsNullOrEmpty(Settings.Default.AutoNameOutputFolder))
			{
				return;
			}

			var itemsToQueue = new List<EncodeJobViewModel>();
			foreach (string fileToQueue in filesToQueue)
			{
				// Exclude all current queued files and the source file from the possible destinations.
				HashSet<string> excludedPaths = this.GetQueuedFiles();
				excludedPaths.Add(fileToQueue);
				string queueOutputPath = Utilities.CreateUniqueFileName(Path.GetFileNameWithoutExtension(fileToQueue) + this.GetOutputExtensionForCurrentEncodingProfile(), Settings.Default.AutoNameOutputFolder, excludedPaths);

				// When ChapterStart is 0, this means the whole title is encoded.
				var job = new EncodeJob
				{
					SourcePath = fileToQueue,
					OutputPath = queueOutputPath,
					EncodingProfile = this.SelectedPreset.Preset.EncodingProfile.Clone(),
					Title = 1,
					RangeType = VideoRangeType.Chapters,
					ChapterStart = 0,
					ChapterEnd = 0,
					ChosenAudioTracks = new List<int> { 1 },
					Subtitles = new Subtitles(),
					UseDefaultChapterNames = true
				};

				var jobVM = new EncodeJobViewModel(job);
				itemsToQueue.Add(jobVM);
			}

			// This dialog will scan the items in the list, calculating length.
			var scanMultipleDialog = new ScanMultipleDialogViewModel(itemsToQueue);
			WindowManager.OpenDialog(scanMultipleDialog, this);

			var failedFiles = new List<string>();
			foreach (EncodeJobViewModel jobVM in itemsToQueue)
			{
				// Only queue items with a successful scan
				if (jobVM.HandBrakeInstance.Titles.Count > 0)
				{
					this.Queue(jobVM);
				}
				else
				{
					failedFiles.Add(jobVM.Job.SourcePath);
				}
			}

			if (failedFiles.Count > 0)
			{
				Utilities.MessageBox.Show(
					"The following file(s) could not be recognized and were not added to the queue:" + Environment.NewLine + Environment.NewLine + string.Join(Environment.NewLine, failedFiles),
					"Error scanning video file(s)",
					MessageBoxButton.OK,
					MessageBoxImage.Warning);
			}
		}

		private HashSet<string> GetQueuedFiles()
		{
			HashSet<string> queuedFiles = new HashSet<string>();
			foreach (EncodeJobViewModel encodeJobVM in this.EncodeQueue)
			{
				queuedFiles.Add(encodeJobVM.Job.OutputPath.ToLowerInvariant());
			}

			return queuedFiles;
		}

		/// <summary>
		/// Queues the given Job. Assumed that the job has an associated HandBrake instance and populated Length.
		/// </summary>
		/// <param name="encodeJobVM">The job to add.</param>
		public void Queue(EncodeJobViewModel encodeJobVM)
		{
			if (this.Encoding)
			{
				if (this.totalTasks == 1)
				{
					this.EncodeQueue[0].IsOnlyItem = false;
				}

				this.totalTasks++;
				this.totalQueueCost += encodeJobVM.Cost;
				this.NotifyPropertyChanged("TaskDetails");
			}

			this.EncodeQueue.Add(encodeJobVM);

			this.NotifyPropertyChanged("QueuedTabHeader");

			// Select the Queued tab.
			if (this.SelectedTabIndex != QueuedTabIndex)
			{
				this.SelectedTabIndex = QueuedTabIndex;
			}
		}

		public void RemoveAudioChoice(AudioChoiceViewModel choice)
		{
			this.AudioChoices.Remove(choice);
		}

		public void RemoveQueueJob(EncodeJobViewModel job)
		{
			this.EncodeQueue.Remove(job);

			if (this.Encoding)
			{
				this.totalTasks--;
				this.totalQueueCost -= job.Cost;

				this.NotifyPropertyChanged("TaskDetails");

				if (this.totalTasks == 1)
				{
					this.EncodeQueue[0].IsOnlyItem = true;
				}
			}

			this.NotifyPropertyChanged("QueuedTabHeader");
		}

		public void SavePreset()
		{
			if (this.SelectedPreset.IsModified)
			{
				this.SelectedPreset.OriginalProfile = null;
				this.SelectedPreset.Preset.IsModified = false;
			}

			// Refresh view and save in case of rename.
			this.SelectedPreset.RefreshView();
			this.SaveUserPresets();

			this.StartAnimation("PresetGlowHighlight");
		}

		public void SavePresetAs(string newPresetName)
		{
			var newPreset = new Preset
			{
				IsBuiltIn = false,
				IsModified = false,
				Name = newPresetName,
				EncodingProfile = this.SelectedPreset.Preset.EncodingProfile.Clone()
			};

			var newPresetVM = new PresetViewModel(newPreset);

			this.InsertNewPreset(newPresetVM);

			if (this.SelectedPreset.IsModified)
			{
				this.RevertPreset(userInitiated: false);
				this.SelectedPreset.RefreshView();
			}

			this.selectedPreset = null;
			this.SelectedPreset = newPresetVM;

			this.StartAnimation("PresetGlowHighlight");
			this.SaveUserPresets();
		}

		public void AddPreset(Preset newPreset)
		{
			var newPresetVM = new PresetViewModel(newPreset);

			this.InsertNewPreset(newPresetVM);

			// Switch to the new preset if we can do it cleanly.
			if (!this.SelectedPreset.IsModified)
			{
				this.selectedPreset = null;
				this.SelectedPreset = newPresetVM;

				this.StartAnimation("PresetGlowHighlight");
			}

			this.SaveUserPresets();
		}

		/// <summary>
		/// Reverts changes to the current preset back to last saved.
		/// </summary>
		public void RevertPreset(bool userInitiated)
		{
			Debug.Assert(this.SelectedPreset.OriginalProfile != null, "Error reverting preset: Original profile cannot be null.");
			Debug.Assert(this.SelectedPreset.OriginalProfile != this.SelectedPreset.Preset.EncodingProfile, "Error reverting preset: Original profile must be different from current profile.");

			this.SelectedPreset.Preset.EncodingProfile = this.SelectedPreset.OriginalProfile;
			this.SelectedPreset.OriginalProfile = null;
			this.SelectedPreset.Preset.IsModified = false;
			this.SelectedPreset.RefreshView();

			if (userInitiated)
			{
				this.StartAnimation("PresetGlowHighlight");
			}

			this.SaveUserPresets();

			// Refresh file name.
			this.GenerateOutputFileName();

			this.RefreshChapterMarkerUI();
			PreviewViewModel.FindAndRefreshPreviews();
		}

		/// <summary>
		/// Deletes the current preset.
		/// </summary>
		public void DeletePreset()
		{
			this.AllPresets.Remove(this.SelectedPreset);
			this.selectedPreset = null;
			this.SelectedPreset = this.AllPresets[0];

			this.StartAnimation("PresetGlowHighlight");
			this.SaveUserPresets();

			this.RefreshChapterMarkerUI();
			PreviewViewModel.FindAndRefreshPreviews();
		}

		/// <summary>
		/// Modify the current preset. Called when first making a change to a preset.
		/// </summary>
		/// <param name="newProfile">The new encoding profile to use.</param>
		public void ModifyPreset(EncodingProfile newProfile)
		{
			Debug.Assert(this.SelectedPreset.IsModified == false, "Cannot start modification on already modified preset.");
			Debug.Assert(this.SelectedPreset.OriginalProfile == null, "Preset already has OriginalProfile.");

			this.SelectedPreset.OriginalProfile = this.SelectedPreset.Preset.EncodingProfile;
			this.SelectedPreset.Preset.EncodingProfile = newProfile;
			this.SelectedPreset.Preset.IsModified = true;
			this.SelectedPreset.RefreshView();
		}

		public void RefreshChapterMarkerUI()
		{
			this.NotifyPropertyChanged("ShowChapterMarkerUI");
		}

		public void RefreshDestination()
		{
			this.GenerateOutputFileName();
		}

		/// <summary>
		/// Processes and sets a user-provided output path.
		/// </summary>
		/// <param name="newOutputPath">The user provided output path.</param>
		public void SetManualOutputPath(string newOutputPath, string oldOutputPath)
		{
			if (newOutputPath == oldOutputPath)
			{
				return;
			}

			if (Utilities.IsValidFullPath(newOutputPath))
			{
				string outputDirectory = Path.GetDirectoryName(newOutputPath);
				Properties.Settings.Default.LastOutputFolder = outputDirectory;
				Properties.Settings.Default.Save();

				string fileName = Path.GetFileNameWithoutExtension(newOutputPath);
				string extension = this.GetOutputExtension();

				this.manualOutputPath = true;
				this.OutputPath = Path.Combine(outputDirectory, fileName + extension);
			}
			else
			{
				// Revert the change if it's not a valid path.
				if (this.OutputPath != oldOutputPath)
				{
					this.OutputPath = oldOutputPath;
				}
			}
		}

		public void SaveUserPresets()
		{
			List<Preset> userPresets = new List<Preset>();

			foreach (PresetViewModel presetVM in this.AllPresets)
			{
				if (!presetVM.Preset.IsBuiltIn || presetVM.Preset.IsModified)
				{
					userPresets.Add(presetVM.Preset);
				}

				// Add the original version of the preset, if we're working with a more recent version.
				if (!presetVM.Preset.IsBuiltIn && presetVM.Preset.IsModified)
				{
					Debug.Assert(presetVM.OriginalProfile != null, "Error saving user presets: Preset marked as modified but no OriginalProfile could be found.");

					var originalPreset = new Preset
					{
						Name = presetVM.Preset.Name,
						IsBuiltIn = presetVM.Preset.IsBuiltIn,
						IsModified = false,
						EncodingProfile = presetVM.OriginalProfile
					};

					userPresets.Add(originalPreset);
				}
			}

			Presets.UserPresets = userPresets;
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

		private void StartScan(string path)
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

		private void SaveEncodeQueue()
		{
			List<EncodeJob> jobs = new List<EncodeJob>();
			foreach (EncodeJobViewModel jobVM in this.EncodeQueue)
			{
				jobs.Add(jobVM.Job);
			}

			EncodeJobsPersist.EncodeJobs = jobs;
		}

		private void OpenEncodingWindow()
		{
			var encodingWindow = WindowManager.FindWindow(typeof(EncodingViewModel)) as EncodingViewModel;
			this.EncodingWindowOpen = true;

			if (encodingWindow == null)
			{
				encodingWindow = new EncodingViewModel(this.SelectedPreset.Preset);
				encodingWindow.Closing = () =>
				{
					this.EncodingWindowOpen = false;

					// Focus the main window after closing. If they used a keyboard shortcut to close it might otherwise give up
					// focus on the app altogether.
					WindowManager.FocusWindow(this);
				};
				WindowManager.OpenWindow(encodingWindow, this);
			}
			else
			{
				WindowManager.FocusWindow(encodingWindow);
			}
		}

		private void OpenPreviewWindow()
		{
			var previewWindow = WindowManager.FindWindow(typeof(PreviewViewModel)) as PreviewViewModel;
			this.PreviewWindowOpen = true;

			if (previewWindow == null)
			{
				previewWindow = new PreviewViewModel();
				previewWindow.Closing = () =>
				{
					this.PreviewWindowOpen = false;
				};
				WindowManager.OpenWindow(previewWindow, this);
			}
			else
			{
				WindowManager.FocusWindow(previewWindow);
			}
		}

		private void OpenLogWindow()
		{
			var logWindow = WindowManager.FindWindow(typeof(LogViewModel)) as LogViewModel;
			this.LogWindowOpen = true;

			if (logWindow == null)
			{
				logWindow = new LogViewModel();
				logWindow.Closing = () =>
				{
					this.LogWindowOpen = false;
				};
				WindowManager.OpenWindow(logWindow, this);
			}
			else
			{
				WindowManager.FocusWindow(logWindow);
			}
		}

		private void GenerateOutputFileName()
		{
			string fileName;

			if (this.manualOutputPath)
			{
				// When a manual path has been specified, keep the directory and base file name.
				fileName = Path.GetFileNameWithoutExtension(this.OutputPath);
				this.OutputPath = Path.Combine(Path.GetDirectoryName(this.OutputPath), fileName + this.GetOutputExtension());
				return;
			}

			if (!this.HasVideoSource)
			{
				string outputFolder = Settings.Default.AutoNameOutputFolder;
				if (outputFolder != null)
				{
					this.OutputPath = outputFolder + (outputFolder.EndsWith(@"\") ? string.Empty : @"\");
				}

				return;
			}

			if (this.sourceName == null || this.SelectedStartChapter == null || this.SelectedEndChapter == null)
			{
				return;
			}

			if (!Settings.Default.AutoNameOutputFiles || string.IsNullOrEmpty(Settings.Default.AutoNameOutputFolder))
			{
				return;
			}

			// Change casing on DVD titles to be a little more friendly
			string translatedSourceName = this.sourceName;
			if ((this.SelectedSource.Type == SourceType.Dvd || this.SelectedSource.Type == SourceType.VideoFolder) && !string.IsNullOrWhiteSpace(this.sourceName))
			{
				translatedSourceName = this.TranslateDvdSourceName(this.sourceName);
			}

			fileName = this.BuildOutputFileName(
				this.sourcePath,
				translatedSourceName,
				this.SelectedTitle.TitleNumber,
				this.VideoRangeType,
				this.SelectedStartChapter.ChapterNumber,
				this.SelectedEndChapter.ChapterNumber,
				this.SelectedTitle.Chapters.Count,
				this.SecondsRangeStart,
				this.SecondsRangeEnd,
				this.FramesRangeStart,
				this.FramesRangeEnd);

			string extension = this.GetOutputExtension();

			this.OutputPath = this.BuildOutputPath(fileName, extension, sourcePath: this.sourcePath);
		}

		private void ReportLengthChanged()
		{
			var encodingWindow = WindowManager.FindWindow(typeof(EncodingViewModel)) as EncodingViewModel;
			if (encodingWindow != null)
			{
				encodingWindow.NotifyLengthChanged();
			}
		}

		/// <summary>
		/// Gets the output extension for the given subtitles and title, and with the
		/// current encoding settings.
		/// </summary>
		/// <param name="givenSubtitles">The subtitles to determine the extension for.</param>
		/// <param name="givenTitle">The title to determine the extension for.</param>
		/// <returns>The output extension (with dot) for the given subtitles and title,
		/// and with the current encoding settings.</returns>
		private string GetOutputExtension(Subtitles givenSubtitles, Title givenTitle)
		{
			string extension;

			// If we have a text source subtitle, force .m4v extension.
			bool allowMp4Extension = true;
			if (givenSubtitles != null && givenSubtitles.SourceSubtitles != null)
			{
				foreach (SourceSubtitle sourceSubtitle in givenSubtitles.SourceSubtitles)
				{
					if (sourceSubtitle.TrackNumber > 0)
					{
						if (givenTitle.Subtitles[sourceSubtitle.TrackNumber - 1].SubtitleType == SubtitleType.Text)
						{
							allowMp4Extension = false;
						}
					}
				}
			}

			EncodingProfile profile = this.SelectedPreset.Preset.EncodingProfile;
			if (profile.OutputFormat == OutputFormat.Mkv)
			{
				extension = ".mkv";
			}
			else if (profile.PreferredExtension == OutputExtension.Mp4 && allowMp4Extension)
			{
				extension = ".mp4";
			}
			else
			{
				extension = ".m4v";
			}

			return extension;
		}

		/// <summary>
		/// Gets the extension that should be used for the current encoding settings, subtitles
		/// and title.
		/// </summary>
		/// <returns>The extension (with dot) that should be used for current encoding settings, subtitles
		/// and title. If no video source is present, this is determined from the encoding settings.</returns>
		private string GetOutputExtension()
		{
			if (this.HasVideoSource)
			{
				return this.GetOutputExtension(this.CurrentSubtitles, this.SelectedTitle);
			}

			return this.GetOutputExtensionForCurrentEncodingProfile();
		}

		private string GetOutputExtensionForCurrentEncodingProfile()
		{
			EncodingProfile profile = this.SelectedPreset.Preset.EncodingProfile;
			if (profile.OutputFormat == OutputFormat.Mkv)
			{
				return ".mkv";
			}
			else if (profile.PreferredExtension == OutputExtension.Mp4)
			{
				return ".mp4";
			}
			else
			{
				return ".m4v";
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
						if (drive.Empty)
						{
							currentOption.VolumeLabel = "(Empty)";
						}
						else
						{
							currentOption.VolumeLabel = drive.VolumeLabel;
						}
					}
				}
			});
		}

		/// <summary>
		/// Changes casing on DVD titles to be a little more friendly.
		/// </summary>
		/// <param name="dvdSourceName">The input DVD source name.</param>
		/// <returns>Cleaned up version of the source name.</returns>
		private string TranslateDvdSourceName(string dvdSourceName)
		{
			string[] titleWords = this.sourceName.Split('_');
			var translatedTitleWords = new List<string>();
			bool reachedModifiers = false;

			foreach (string titleWord in titleWords)
			{
				// After the disc designator, stop changing capitalization.
				if (!reachedModifiers && titleWord.Length == 2 && titleWord[0] == 'D' && char.IsDigit(titleWord[1]))
				{
					reachedModifiers = true;
				}

				if (reachedModifiers)
				{
					translatedTitleWords.Add(titleWord);
				}
				else
				{
					if (titleWord.Length > 0)
					{
						translatedTitleWords.Add(titleWord[0] + titleWord.Substring(1).ToLower());
					}
				}
			}

			return string.Join(" ", translatedTitleWords);
		}

		private string BuildOutputPath(string fileName, string extension, string sourcePath)
		{
			string outputFolder = Settings.Default.AutoNameOutputFolder;
			if (!string.IsNullOrEmpty(outputFolder))
			{
				string result = Path.Combine(outputFolder, fileName + extension);
				if (result == sourcePath)
				{
					result = Path.Combine(outputFolder, fileName + " (Encoded)" + extension);
				}

				return result;
			}

			return null;
		}

		private string BuildOutputFileName(string sourcePath, string sourceName, int title, int totalChapters)
		{
			return this.BuildOutputFileName(
				sourcePath,
				sourceName,
				title,
				VideoRangeType.Chapters,
				1,
				totalChapters,
				totalChapters,
				0,
				0,
				0,
				0);
		}

		private string BuildOutputFileName(string sourcePath, string sourceName, int title, VideoRangeType rangeType, int startChapter, int endChapter, int totalChapters, double startSecond, double endSecond, int startFrame, int endFrame)
		{
			string fileName;
			if (Settings.Default.AutoNameCustomFormat)
			{
				string rangeString = string.Empty;
				switch (rangeType)
				{
					case VideoRangeType.Chapters:
						if (startChapter == endChapter)
						{
							rangeString = startChapter.ToString();
						}
						else
						{
							rangeString = startChapter + "-" + endChapter;
						}

						break;
					case VideoRangeType.Seconds:
						rangeString = startSecond + "-" + endSecond;
						break;
					case VideoRangeType.Frames:
						rangeString = startFrame + "-" + endFrame;
						break;
				}

				fileName = Settings.Default.AutoNameCustomFormatString;

				fileName = fileName.Replace("{source}", sourceName);
				fileName = ReplaceTitles(fileName, title);
				fileName = fileName.Replace("{range}", rangeString);

				// {chapters} is deprecated in favor of {range} but we replace here for backwards compatibility.
				fileName = fileName.Replace("{chapters}", rangeString);

				fileName = fileName.Replace("{preset}", this.SelectedPreset.Preset.Name);
				fileName = ReplaceParents(fileName, sourcePath);

				DateTime now = DateTime.Now;
				if (fileName.Contains("{date}"))
				{
					fileName = fileName.Replace("{date}", now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
				}

				if (fileName.Contains("{time}"))
				{
					fileName = fileName.Replace("{time}", now.Hour + "." + now.Minute + "." + now.Second);
				}

				if (fileName.Contains("{quality}"))
				{
					EncodingProfile profile = this.SelectedPreset.Preset.EncodingProfile;
					double quality = 0;
					switch (profile.VideoEncodeRateType)
					{
						case VideoEncodeRateType.ConstantQuality:
							quality = profile.Quality;
							break;
						case VideoEncodeRateType.AverageBitrate:
							quality = profile.VideoBitrate;
							break;
						case VideoEncodeRateType.TargetSize:
							quality = profile.TargetSize;
							break;
						default:
							break;
					}

					fileName = fileName.Replace("{quality}", quality.ToString());
				}
			}
			else
			{
				string titleSection = string.Empty;
				if (this.SelectedSource.Type != SourceType.File)
				{
					titleSection = " - Title " + title;
				}

				string rangeSection = string.Empty;
				switch (rangeType)
				{
					case VideoRangeType.Chapters:
						if (startChapter > 1 || endChapter < totalChapters)
						{
							if (startChapter == endChapter)
							{
								rangeSection = " - Chapter " + startChapter;
							}
							else
							{
								rangeSection = " - Chapters " + startChapter + "-" + endChapter;
							}
						}

						break;
					case VideoRangeType.Seconds:
						rangeSection = " - Seconds " + startSecond + "-" + endSecond;
						break;
					case VideoRangeType.Frames:
						rangeSection = " - Frames " + startFrame + "-" + endFrame;
						break;
				}

				fileName = sourceName + titleSection + rangeSection;
			}

			return Utilities.CleanFileName(fileName);
		}

		private static string ReplaceTitles(string inputString, int title)
		{
			inputString = inputString.Replace("{title}", title.ToString());

			Regex regex = new Regex("{title:(?<number>[0-9]+)}");
			Match match;
			while ((match = regex.Match(inputString)).Success)
			{
				Capture capture = match.Groups["number"].Captures[0];
				int replaceIndex = capture.Index - 7;
				int replaceLength = capture.Length + 8;

				int digits = int.Parse(capture.Value);

				if (digits > 0 && digits <= 10)
				{
					inputString = inputString.Substring(0, replaceIndex) + string.Format("{0:D" + digits + "}", title) + inputString.Substring(replaceIndex + replaceLength);
				}
			}

			return inputString;
		}

		/// <summary>
		/// Takes a string and replaces instances of {parent} or {parent:x} with the appropriate parent.
		/// </summary>
		/// <param name="inputString">The input string to perform replacements in.</param>
		/// <param name="path">The path to take the parents from.</param>
		/// <returns>The string with instances replaced.</returns>
		private static string ReplaceParents(string inputString, string path)
		{
			string directParentName = Path.GetDirectoryName(path);
			if (directParentName == null)
			{
				return inputString;
			}

			DirectoryInfo directParent = new DirectoryInfo(directParentName);

			if (directParent.Root.FullName == directParent.FullName)
			{
				return inputString;
			}

			inputString = inputString.Replace("{parent}", directParent.Name);

			Regex regex = new Regex("{parent:(?<number>[0-9]+)}");
			Match match;
			while ((match = regex.Match(inputString)).Success)
			{
				Capture capture = match.Groups["number"].Captures[0];
				int replaceIndex = capture.Index - 8;
				int replaceLength = capture.Length + 9;

				inputString = inputString.Substring(0, replaceIndex) + FindParent(path, int.Parse(capture.Value)) + inputString.Substring(replaceIndex + replaceLength);
			}

			return inputString;
		}

		private static string FindParent(string path, int parentNumber)
		{
			string directParentName = Path.GetDirectoryName(path);
			if (directParentName == null)
			{
				return string.Empty;
			}

			DirectoryInfo directParent = new DirectoryInfo(directParentName);
			string rootName = directParent.Root.FullName;

			DirectoryInfo currentDirectory = directParent;
			for (int i = 1; i < parentNumber; i++)
			{
				currentDirectory = currentDirectory.Parent;

				if (currentDirectory.FullName == rootName)
				{
					return string.Empty;
				}
			}

			if (currentDirectory.FullName == rootName)
			{
				return string.Empty;
			}

			return currentDirectory.Name;
		}

		private void InsertNewPreset(PresetViewModel presetVM)
		{
			for (int i = 0; i < this.AllPresets.Count; i++)
			{
				if (this.AllPresets[i].IsBuiltIn)
				{
					this.AllPresets.Insert(i, presetVM);
					return;
				}

				if (string.CompareOrdinal(presetVM.PresetName, this.AllPresets[i].PresetName) < 0)
				{
					this.AllPresets.Insert(i, presetVM);
					return;
				}
			}

			Debug.Assert(true, "Did not find place to insert new preset.");
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

		private void CleanOldLogs()
		{
			string logsFolder = Path.Combine(Utilities.AppFolder, "Logs");
			string[] logFiles = Directory.GetFiles(logsFolder);

			// Files are named by date so are in chronological order. Keep the most recent 10 files.
			for (int i = 0; i < logFiles.Length - 10; i++)
			{
				try
				{
					File.Delete(logFiles[i]);
				}
				catch (IOException)
				{
					// Just ignore failed deletes. They'll get cleaned up some other time.
				}
			}
		}

		private void CleanPreviewFileCache()
		{
			try
			{
				DirectoryInfo cacheFolder = new DirectoryInfo(Utilities.ImageCacheFolder);
				DirectoryInfo[] processDirectories = cacheFolder.GetDirectories();

				int ourPid = Process.GetCurrentProcess().Id;

				Process[] processes = Process.GetProcesses();
				var pidList = processes.Select(p => p.Id);
				HashSet<int> pidSet = new HashSet<int>(pidList);

				foreach (DirectoryInfo processDirectory in processDirectories)
				{
					int directoryPid;
					if (int.TryParse(processDirectory.Name, out directoryPid))
					{
						if (directoryPid == ourPid || !pidSet.Contains(directoryPid))
						{
							try
							{
								Utilities.DeleteDirectory(processDirectory.FullName);
							}
							catch (IOException)
							{
								// Ignore failed cleanup. Move on to the next folder.
							}
						}
					}

				}
			}
			catch (IOException)
			{
				// Ignore failed cleanup. Will get done some other time.
			}
		}
	}
}
