using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Xml.Serialization;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;
using HandBrake.ApplicationServices.Interop;
using HandBrake.ApplicationServices.Interop.Json.Scan;
using Newtonsoft.Json;
using VidCoder.Automation;
using VidCoder.Extensions;
using VidCoder.Messages;
using VidCoder.Model;
using VidCoder.Resources;
using VidCoder.Services;
using VidCoderCommon.Extensions;
using VidCoderCommon.Model;

namespace VidCoder.ViewModel
{
	public class MainViewModel : ViewModelBase
	{
		private HandBrakeInstance scanInstance;

		private IUpdater updater = Ioc.Container.GetInstance<IUpdater>();
		private ILogger logger = Ioc.Container.GetInstance<ILogger>();
		private OutputPathService outputPathService;
		private PresetsService presetsService;
		private PickersService pickersService;
		private ProcessingService processingService;
		private WindowManagerService windowManagerService;
		private TaskBarProgressTracker taskBarProgressTracker;

		private ObservableCollection<SourceOptionViewModel> sourceOptions;
		private ObservableCollection<SourceOptionViewModel> recentSourceOptions; 
		private bool sourceSelectionExpanded;
		private SourceOption selectedSource;

		private SourceTitle selectedTitle;
		private SourceTitle oldTitle;
		private List<int> angles;
		private int angle;

		private VideoRangeType rangeType;
		private List<ComboChoice<VideoRangeType>> rangeTypeChoices; 
		private TimeSpan timeBaseline;
		private int framesRangeStart;
		private int framesRangeEnd;
		private int framesBaseline;
		private List<ChapterViewModel> startChapters;
		private List<ChapterViewModel> endChapters;
		private ChapterViewModel selectedStartChapter;
		private ChapterViewModel selectedEndChapter;

		private ObservableCollection<AudioChoiceViewModel> audioChoices;

		private VCSubtitles currentSubtitles;

		private bool useDefaultChapterNames;
		private List<string> customChapterNames;

		// sourceData is populated when a scan has finished and cleared out when a new scan starts.
		private VideoSource sourceData;

		private IDriveService driveService;
		private IList<DriveInformation> driveCollection;
		private bool scanningSource;
		private bool scanError;
		private double scanProgress;
		private string pendingScan;
		private bool scanCancelledFlag;

		private bool showTrayIcon;

		public event EventHandler<EventArgs<string>> AnimationStarted;
		public event EventHandler ScanCancelled;

		public MainViewModel()
		{
			Ioc.Container.Register(() => this);

			this.outputPathService = Ioc.Container.GetInstance<OutputPathService>();
			this.processingService = Ioc.Container.GetInstance<ProcessingService>();
			this.presetsService = Ioc.Container.GetInstance<PresetsService>();
			this.pickersService = Ioc.Container.GetInstance<PickersService>();
			this.windowManagerService = Ioc.Container.GetInstance<WindowManagerService>();
			this.taskBarProgressTracker = new TaskBarProgressTracker();

			updater.CheckUpdates();

			this.JobCreationAvailable = false;

			this.sourceOptions = new ObservableCollection<SourceOptionViewModel>
			{
				new SourceOptionViewModel(new SourceOption { Type = SourceType.File }),
				new SourceOptionViewModel(new SourceOption { Type = SourceType.VideoFolder })
			};

			this.rangeTypeChoices = new List<ComboChoice<VideoRangeType>>
				{
					new ComboChoice<VideoRangeType>(VideoRangeType.Chapters, EnumsRes.VideoRangeType_Chapters),
					new ComboChoice<VideoRangeType>(VideoRangeType.Seconds, EnumsRes.VideoRangeType_Seconds),
					new ComboChoice<VideoRangeType>(VideoRangeType.Frames, EnumsRes.VideoRangeType_Frames),
				};

			this.recentSourceOptions = new ObservableCollection<SourceOptionViewModel>();
			this.RefreshRecentSourceOptions();

			this.useDefaultChapterNames = true;

			this.driveService = Ioc.Container.GetInstance<IDriveService>();

			this.DriveCollection = this.driveService.GetDiscInformation();

			this.audioChoices = new ObservableCollection<AudioChoiceViewModel>();
			this.audioChoices.CollectionChanged += (o, e) => { Messenger.Default.Send(new AudioInputChangedMessage()); };

			Messenger.Default.Register<SelectedTitleChangedMessage>(
				this,
				message =>
					{
						DispatchUtilities.BeginInvoke(() => { this.AddTrackCommand.RaiseCanExecuteChanged(); });
					});

			Messenger.Default.Register<HighlightedChapterChangedMessage>(
				this,
				message =>
					{
						this.RefreshRangePreview();
					});

			Messenger.Default.Register<ProgressChangedMessage>(
				this,
				message =>
					{
						this.RaisePropertyChanged(() => this.WindowTitle);
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

			string videoFile = FileService.Instance.GetFileNameLoad(
				Config.RememberPreviousFiles ? Config.LastInputFileFolder : null, 
				"Load video file");

			if (videoFile != null)
			{
				this.SetSourceFromFile(videoFile);
				return true;
			}

			return false;
		}

		public void SetSourceFromFile(string videoFile)
		{
			if (Config.RememberPreviousFiles)
			{
				Config.LastInputFileFolder = Path.GetDirectoryName(videoFile);
			}

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

			string folderPath = FileService.Instance.GetFolderName(
				Config.RememberPreviousFiles ? Config.LastVideoTSFolder : null, 
				MainRes.PickDiscFolderHelpText);

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
			if (Config.RememberPreviousFiles)
			{
				Config.LastVideoTSFolder = videoFolder;
			}

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
			this.SourcePath = driveInfo.RootDirectory;

			this.SelectedSource = new SourceOption { Type = SourceType.Dvd, DriveInfo = driveInfo };
			this.StartScan(this.SourcePath);

			return true;
		}

		public void SetSource(string sourcePath)
		{
			SourceType sourceType = Utilities.GetSourceType(sourcePath);
			switch (sourceType)
			{
				case SourceType.File:
					this.SetSourceFromFile(sourcePath);
					break;
				case SourceType.VideoFolder:
					this.SetSourceFromFolder(sourcePath);
					break;
				case SourceType.Dvd:
					DriveInformation driveInfo = this.driveService.GetDriveInformationFromPath(sourcePath);
					if (driveInfo != null)
					{
						this.SetSourceFromDvd(driveInfo);
					}

					break;
			}
		}

		public void OnLoaded()
		{
			bool windowOpened = false;

			if (Config.EncodingWindowOpen)
			{
				this.WindowManagerService.OpenEncodingWindow();
				windowOpened = true;
			}

			if (Config.PreviewWindowOpen)
			{
				this.WindowManagerService.OpenPreviewWindow();
				windowOpened = true;
			}

			if (Config.LogWindowOpen)
			{
				this.WindowManagerService.OpenLogWindow();
				windowOpened = true;
			}

		    if (Config.PickerWindowOpen)
		    {
		        this.WindowManagerService.OpenPickerWindow();
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
			if (this.processingService.Encoding)
			{
				MessageBoxResult result = Utilities.MessageBox.Show(
					this,
					MainRes.EncodesInProgressExitWarning,
					MainRes.EncodesInProgressExitWarningTitle,
					MessageBoxButton.YesNo,
					MessageBoxImage.Warning);

				if (result == MessageBoxResult.No)
				{
					return false;
				}
			}

			// If we're quitting, see if the encode is still going.
			if (this.processingService.Encoding)
			{
				// If so, stop it.
				this.processingService.EncodeProxy.StopAndWait();
				//this.processingService.CurrentJob.HandBrakeInstance.StopEncode();
			}

			ViewModelBase encodingWindow = WindowManager.FindWindow<EncodingViewModel>();
			if (encodingWindow != null)
			{
				WindowManager.Close(encodingWindow);
			}

			var previewWindow = WindowManager.FindWindow<PreviewViewModel>();
			if (previewWindow != null)
			{
				if (previewWindow.GeneratingPreview)
				{
					previewWindow.StopAndWait();
				}

				WindowManager.Close(previewWindow);
			}

			ViewModelBase logWindow = WindowManager.FindWindow<LogViewModel>();
			if (logWindow != null)
			{
				WindowManager.Close(logWindow);
			}

		    ViewModelBase pickerWindow = WindowManager.FindWindow<PickerWindowViewModel>();
		    if (pickerWindow != null)
		    {
		        WindowManager.Close(pickerWindow);
		    }

			using (SQLiteTransaction transaction = Database.ThreadLocalConnection.BeginTransaction())
			{
				Config.EncodingWindowOpen = encodingWindow != null;
				Config.PreviewWindowOpen = previewWindow != null;
				Config.LogWindowOpen = logWindow != null;

				transaction.Commit();
			}

			this.driveService.Close();

			if (this.scanInstance != null)
			{
				this.scanInstance.Dispose();
				this.scanInstance = null;
			}

			HandBrakeUtils.DisposeGlobal();

			FileCleanup.CleanOldLogs();
			FileCleanup.CleanPreviewFileCache();

			if (!Utilities.IsPortable)
			{
				AutomationHost.StopListening();
			}

			this.updater.PromptToApplyUpdate();

			this.logger.Dispose();

			return true;
		}

		public string WindowTitle
		{
			get
			{
				if (!this.processingService.Encoding)
				{
					return "VidCoder";
				}

				return string.Format("VidCoder - {0:F1}%", this.processingService.OverallEncodeProgressPercent);
			}
		}

		public string SourcePath { get; private set; }

		public string SourceName { get; private set; }

		public OutputPathService OutputPathVM
		{
			get { return this.outputPathService; }
		}

		public PresetsService PresetsService
		{
			get { return this.presetsService; }
		}

		public PickersService PickersService
		{
			get { return this.pickersService; }
		}

		public ProcessingService ProcessingService
		{
			get { return this.processingService; }
		}

		public WindowManagerService WindowManagerService
		{
			get { return this.windowManagerService; }
		}

		public TaskBarProgressTracker TaskBarProgressTracker
		{
			get { return this.taskBarProgressTracker; }
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
			get { return this.sourceOptions; }
		}

		public ObservableCollection<SourceOptionViewModel> RecentSourceOptions
		{
			get { return this.recentSourceOptions; }
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
				this.taskBarProgressTracker.SetIsScanning(value);
				this.RaisePropertyChanged(() => this.SourceOptionsVisible);
				this.CloseVideoSourceCommand.RaiseCanExecuteChanged();
				this.RaisePropertyChanged(() => this.CanCloseVideoSource);
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

		private void SetScanProgress(double scanProgressFraction)
		{
			this.taskBarProgressTracker.SetScanProgress(scanProgressFraction);
			this.ScanProgress = scanProgressFraction * 100;
		}

		/// <summary>
		/// Gets or sets the scan progress percentage.
		/// </summary>
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

		public bool CanCloseVideoSource
		{
			get
			{
				if (this.SelectedSource == null)
				{
					return false;
				}

				if (this.ScanningSource)
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

		public List<SourceTitle> Titles
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

		public SourceTitle SelectedTitle
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
						this.OutputPathVM.NameFormatOverride = null;
						this.OutputPathVM.SourceParentFolder = null;
					}

					// Save old subtitles
					VCSubtitles oldSubtitles = this.CurrentSubtitles;
					this.CurrentSubtitles = new VCSubtitles { SourceSubtitles = new List<SourceSubtitle>(), SrtSubtitles = new List<SrtSubtitle>() };

					Picker picker = this.pickersService.SelectedPicker.Picker;

					// Audio selection
					switch (picker.AudioSelectionMode)
					{
						case AudioSelectionMode.Disabled:
							// If no auto-selection is done, keep audio from previous selection.
							if (this.oldTitle != null)
							{
								var keptAudioChoices = new List<AudioChoiceViewModel>();
								foreach (AudioChoiceViewModel audioChoiceVM in this.AudioChoices)
								{
									if (audioChoiceVM.SelectedIndex < this.selectedTitle.AudioList.Count &&
										this.oldTitle.AudioList[audioChoiceVM.SelectedIndex].Language == this.selectedTitle.AudioList[audioChoiceVM.SelectedIndex].Language)
									{
										keptAudioChoices.Add(audioChoiceVM);
									}
								}

								this.AudioChoices.Clear();
								foreach (AudioChoiceViewModel audioChoiceVM in keptAudioChoices)
								{
									this.AudioChoices.Add(audioChoiceVM);
								}
							}
							else
							{
								this.AudioChoices.Clear();
							}

							break;
						case AudioSelectionMode.Language:
							this.AudioChoices.Clear();
							for (int i = 0; i < this.selectedTitle.AudioList.Count; i++)
							{
								SourceAudioTrack track = this.selectedTitle.AudioList[i];

								if (track.LanguageCode == picker.AudioLanguageCode)
								{
									var newVM = new AudioChoiceViewModel { SelectedIndex = i };
									this.AudioChoices.Add(newVM);

									if (!picker.AudioLanguageAll)
									{
										break;
									}
								}
							}

							break;
						case AudioSelectionMode.All:
							this.AudioChoices.Clear();

							for (int i = 0; i < this.selectedTitle.AudioList.Count; i++)
							{
								var newVM = new AudioChoiceViewModel { SelectedIndex = i };
								this.AudioChoices.Add(newVM);
							}

							break;
						default:
							throw new ArgumentOutOfRangeException();
					}

					// If nothing got selected, add the first one.
					if (this.selectedTitle.AudioList.Count > 0 && this.AudioChoices.Count == 0)
					{
						var newVM = new AudioChoiceViewModel { SelectedIndex = 0 };
						this.AudioChoices.Add(newVM);
					}

					switch (picker.SubtitleSelectionMode)
					{
						case SubtitleSelectionMode.Disabled:
							// If no auto-selection is done, try and keep selections from previous title
							if (this.oldTitle != null && oldSubtitles != null)
							{
								if (this.selectedTitle.SubtitleList.Count > 0)
								{
									// Keep source subtitles when changing title, but not specific SRT files.
									var keptSourceSubtitles = new List<SourceSubtitle>();
									foreach (SourceSubtitle sourceSubtitle in oldSubtitles.SourceSubtitles)
									{
										if (sourceSubtitle.TrackNumber == 0)
										{
											keptSourceSubtitles.Add(sourceSubtitle);
										}
										else if (sourceSubtitle.TrackNumber - 1 < this.selectedTitle.SubtitleList.Count &&
											this.oldTitle.SubtitleList[sourceSubtitle.TrackNumber - 1].LanguageCode == this.selectedTitle.SubtitleList[sourceSubtitle.TrackNumber - 1].LanguageCode)
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
							break;
						case SubtitleSelectionMode.ForeignAudioSearch:
							this.CurrentSubtitles.SourceSubtitles.Add(
								new SourceSubtitle
									{
										TrackNumber = 0,
										BurnedIn = picker.SubtitleForeignBurnIn,
										Forced = true,
										Default = true
									});
							break;
						case SubtitleSelectionMode.Language:
							string languageCode = picker.SubtitleLanguageCode;
							bool audioSame = false;
							bool burnIn = picker.SubtitleLanguageBurnIn;
							bool def = picker.SubtitleLanguageDefault;
							if (this.AudioChoices.Count > 0)
							{
								if (this.selectedTitle.AudioList[this.AudioChoices[0].SelectedIndex].LanguageCode == languageCode)
								{
									audioSame = true;
								}
							}

							if (!picker.SubtitleLanguageOnlyIfDifferent || !audioSame)
							{
								bool multipleLanguageTracks = this.selectedTitle.SubtitleList.Count(s => s.LanguageCode == languageCode) > 1;
								for (int i = 0; i < this.selectedTitle.SubtitleList.Count; i++)
								{
									SourceSubtitleTrack subtitle = this.selectedTitle.SubtitleList[i];

									if (subtitle.LanguageCode == languageCode)
									{
										this.CurrentSubtitles.SourceSubtitles.Add(new SourceSubtitle
										{
											BurnedIn = !multipleLanguageTracks && burnIn,
											Default = !multipleLanguageTracks && def,
											Forced = false,
											TrackNumber = i + 1
										});

										if (!picker.SubtitleLanguageAll)
										{
											break;
										}
									}
								}
							}

							break;
						case SubtitleSelectionMode.All:
							for (int i = 0; i < this.selectedTitle.SubtitleList.Count; i++)
							{
								this.CurrentSubtitles.SourceSubtitles.Add(
									new SourceSubtitle
									{
										TrackNumber = i + 1,
										BurnedIn = false,
										Default = false,
										Forced = false
									});
							}

							break;
						default:
							throw new ArgumentOutOfRangeException();
					}

					this.UseDefaultChapterNames = true;
					this.PopulateChapterSelectLists();

					// Soft update so as not to trigger range checking logic.
					this.selectedStartChapter = this.StartChapters[0];
					this.selectedEndChapter = this.EndChapters[this.EndChapters.Count - 1];
					this.RaisePropertyChanged(() => this.SelectedStartChapter);
					this.RaisePropertyChanged(() => this.SelectedEndChapter);

					this.SetRangeTimeStart(TimeSpan.Zero);
					this.SetRangeTimeEnd(this.selectedTitle.Duration.ToSpan());
					this.RaisePropertyChanged(() => this.TimeRangeStart);
					this.RaisePropertyChanged(() => this.TimeRangeStartBar);
					this.RaisePropertyChanged(() => this.TimeRangeEnd);
					this.RaisePropertyChanged(() => this.TimeRangeEndBar);

					this.framesRangeStart = 0;
					this.framesRangeEnd = this.selectedTitle.GetEstimatedFrames();
					this.RaisePropertyChanged(() => this.FramesRangeStart);
					this.RaisePropertyChanged(() => this.FramesRangeEnd);

					this.PopulateAnglesList();

					this.angle = 1;

					this.RaisePropertyChanged(() => this.Angles);
					this.RaisePropertyChanged(() => this.Angle);
					this.RaisePropertyChanged(() => this.AngleVisible);
					this.RaisePropertyChanged(() => this.TotalChaptersText);


					// Change range type based on whether or not we have any chapters
					if (this.selectedTitle.ChapterList.Count > 1)
					{
						this.RangeType = VideoRangeType.Chapters;
					}
					else
					{
						this.RangeType = VideoRangeType.Seconds;
					}

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

		public VCSubtitles CurrentSubtitles
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
					return MainRes.NoneParen;
				}

				List<string> trackSummaries = new List<string>();

				foreach (SourceSubtitle sourceSubtitle in this.CurrentSubtitles.SourceSubtitles)
				{
					if (sourceSubtitle.TrackNumber == 0)
					{
						trackSummaries.Add(MainRes.ForeignAudioSearch);
					}
					else
					{
						trackSummaries.Add(this.SelectedTitle.SubtitleList[sourceSubtitle.TrackNumber - 1].Language);
					}
				}

				foreach (SrtSubtitle srtSubtitle in this.currentSubtitles.SrtSubtitles)
				{
					trackSummaries.Add(Languages.Get(srtSubtitle.LanguageCode).EnglishName);
				}

				if (trackSummaries.Count > 3)
				{
					return string.Format(MainRes.TracksSummaryMultiple, trackSummaries.Count);
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
					return CommonRes.Default;
				}

				return CommonRes.Custom;
			}
		}

		public bool ShowChapterMarkerUI
		{
			get
			{
				return this.presetsService.SelectedPreset != null && this.presetsService.SelectedPreset.Preset.EncodingProfile.IncludeChapterMarkers;
			}
		}

		public bool TitleVisible
		{
			get { return this.HasVideoSource && this.SourceData.Titles.Count > 1; }
		}

		public VideoRangeType RangeType
		{
			get
			{
				return this.rangeType;
			}

			set
			{
				if (value != this.rangeType)
				{
					this.rangeType = value;

					this.OutputPathVM.GenerateOutputFileName();
					this.RaisePropertyChanged(() => this.RangeType);
					this.RaisePropertyChanged(() => this.SecondsRangeVisible);
					this.RaisePropertyChanged(() => this.FramesRangeVisible);
					this.RaisePropertyChanged(() => this.TotalChaptersText);
					this.RaisePropertyChanged(() => this.UsingChaptersRange);
					this.RaisePropertyChanged(() => this.RangeBarVisible);
					this.RefreshRangePreview();
					this.ReportLengthChanged();
				}
			}
		}

		public List<ComboChoice<VideoRangeType>> RangeTypeChoices
		{
			get
			{
				return this.rangeTypeChoices;
			}
		}

		public bool UsingChaptersRange
		{
			get
			{
				return this.RangeType == VideoRangeType.Chapters;
			}
		}

		public bool RangeBarVisible
		{
			get
			{
				return this.RangeType == VideoRangeType.Chapters || this.RangeType == VideoRangeType.Seconds;
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

		// Properties for the range seek bar
		private TimeSpan timeRangeStartBar;
		public TimeSpan TimeRangeStartBar
		{
			get
			{
				return this.timeRangeStartBar;
			}

			set
			{
				this.SetRangeTimeStart(value);

				this.OutputPathVM.GenerateOutputFileName();

				this.RaisePropertyChanged(() => this.TimeRangeStartBar);
				this.RaisePropertyChanged(() => this.TimeRangeStart);
				this.RefreshRangePreview();
				this.ReportLengthChanged();
			}
		}

		private TimeSpan timeRangeEndBar;
		public TimeSpan TimeRangeEndBar
		{
			get
			{
				return this.timeRangeEndBar;
			}

			set
			{
				this.SetRangeTimeEnd(value);

				this.OutputPathVM.GenerateOutputFileName();

				this.RaisePropertyChanged(() => this.TimeRangeEndBar);
				this.RaisePropertyChanged(() => this.TimeRangeEnd);
				this.RefreshRangePreview();
				this.ReportLengthChanged();
			}
		}

		// Properties for the range text boxes
		private TimeSpan timeRangeStart;
		public TimeSpan TimeRangeStart
		{
			get
			{
				return this.timeRangeStart;
			}

			set
			{
				this.timeRangeStart = value;

				TimeSpan maxTime = this.SelectedTitle.Duration.ToSpan();
				if (this.timeRangeStart > maxTime - Constants.TimeRangeBuffer)
				{
					this.timeRangeStart = maxTime - Constants.TimeRangeBuffer;
				}

				this.timeRangeStartBar = this.timeRangeStart;

				// Constrain from the baseline: what the other end was when we got focus
				TimeSpan idealTimeEnd = this.timeBaseline;
				if (idealTimeEnd < this.timeRangeStart + Constants.TimeRangeBuffer)
				{
					idealTimeEnd = this.timeRangeStart + Constants.TimeRangeBuffer;
				}

				if (idealTimeEnd != this.timeRangeEnd)
				{
					this.SetRangeTimeEnd(idealTimeEnd);
					this.RaisePropertyChanged(() => this.TimeRangeEnd);
					this.RaisePropertyChanged(() => this.TimeRangeEndBar);
				}

				this.OutputPathVM.GenerateOutputFileName();

				this.RaisePropertyChanged(() => this.TimeRangeStart);
				this.RaisePropertyChanged(() => this.TimeRangeStartBar);
				this.RefreshRangePreview();
				this.ReportLengthChanged();
			}
		}

		private TimeSpan timeRangeEnd;
		public TimeSpan TimeRangeEnd
		{
			get
			{
				return this.timeRangeEnd;
			}

			set
			{
				this.timeRangeEnd = value;

				TimeSpan maxTime = this.SelectedTitle.Duration.ToSpan();

				if (this.timeRangeEnd > maxTime)
				{
					this.timeRangeEnd = maxTime;
				}

				this.timeRangeEndBar = this.timeRangeEnd;

				// Constrain from the baseline: what the other end was when we got focus
				TimeSpan idealTimeStart = this.timeBaseline;
				if (idealTimeStart > this.timeRangeEnd - Constants.TimeRangeBuffer)
				{
					idealTimeStart = this.timeRangeEnd - Constants.TimeRangeBuffer;
				}

				if (idealTimeStart != this.timeRangeStart)
				{
					this.SetRangeTimeEnd(idealTimeStart);
					this.RaisePropertyChanged(() => this.TimeRangeStart);
					this.RaisePropertyChanged(() => this.TimeRangeStartBar);
				}

				this.OutputPathVM.GenerateOutputFileName();

				this.RaisePropertyChanged(() => this.TimeRangeEnd);
				this.RaisePropertyChanged(() => this.TimeRangeEndBar);
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
				this.framesRangeStart = value;

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
				this.framesRangeEnd = value;

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
					return string.Format(MainRes.ChaptersSummary, this.SelectedTitle.ChapterList.Count);
				}

				return string.Empty;
			}
		}

		public int ChapterPreviewStart
		{
			get
			{
				if (this.SelectedTitle == null || this.SelectedStartChapter == null || this.SelectedEndChapter == null)
				{
					return 0;
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

				return startChapter;
			}
			set
			{
				this.SelectedStartChapter = this.StartChapters.FirstOrDefault(c => c.ChapterNumber == value);
			}
		}

		public int ChapterPreviewEnd
		{
			get
			{
				if (this.SelectedTitle == null || this.SelectedStartChapter == null || this.SelectedEndChapter == null)
				{
					return 0;
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

				return endChapter;
			}
			set
			{
				this.SelectedEndChapter = this.EndChapters.FirstOrDefault(c => c.ChapterNumber == value);
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
					case VideoRangeType.All:
						return TimeSpan.Zero;
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
						return this.TimeRangeStart;
					case VideoRangeType.Frames:
						return TimeSpan.FromSeconds(this.FramesRangeStart / this.SelectedTitle.FrameRate.ToDouble());
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
					case VideoRangeType.All:
						return this.SelectedTitle.Duration.ToSpan();
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
						return this.TimeRangeEnd;
					case VideoRangeType.Frames:
						return TimeSpan.FromSeconds(this.FramesRangeEnd / this.SelectedTitle.FrameRate.ToDouble());
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

				return string.Format(MainRes.LengthPreviewLabel, duration.ToString(Utilities.TimeFormat));
			}
		}

		public ObservableCollection<AudioChoiceViewModel> AudioChoices
		{
			get
			{
				return this.audioChoices;
			}
		}

		public List<AudioTrackViewModel> InputTracks
		{
			get
			{
				if (this.SourceData != null)
				{
					var result = new List<AudioTrackViewModel>();

					for (int i = 0; i < this.SelectedTitle.AudioList.Count; i++)
					{
						SourceAudioTrack track = this.SelectedTitle.AudioList[i];
						result.Add(new AudioTrackViewModel(track, (i + 1)));
					}

					return result;
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

						return this.SelectedTitle.AudioList.Count > 0;
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
						var chaptersVM = new ChapterMarkersDialogViewModel(this.SelectedTitle.ChapterList, this.CustomChapterNames, this.UseDefaultChapterNames);
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

		private RelayCommand closeVideoSourceCommand;
		public RelayCommand CloseVideoSourceCommand
		{
			get
			{
				return this.closeVideoSourceCommand ?? (this.closeVideoSourceCommand = new RelayCommand(() =>
					{
						this.sourceData = null;
						this.SelectedSource = null;
						this.SourceSelectionExpanded = false;
						this.CloseVideoSourceCommand.RaiseCanExecuteChanged();
						this.RaisePropertyChanged(() => this.CanCloseVideoSource);
						this.RaisePropertyChanged(() => this.HasVideoSource);
						this.RaisePropertyChanged(() => this.SourceOptionsVisible);
						this.RaisePropertyChanged(() => this.SourcePicked);

						this.RefreshRecentSourceOptions();

						DispatchUtilities.BeginInvoke(() => Messenger.Default.Send(new VideoSourceChangedMessage()));
						Messenger.Default.Send(new RefreshPreviewMessage());
						this.SelectedTitle = null;

						HandBrakeInstance oldInstance = this.scanInstance;
						this.scanInstance = null;
						if (oldInstance != null)
						{
							oldInstance.Dispose();
						}
					},
					() =>
					{
						return this.CanCloseVideoSource;
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
							Config.QueueColumns = queueDialog.NewColumns;
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
						string presetFileName = FileService.Instance.GetFileNameLoad(
							null, 
							MainRes.ImportPresetFilePickerTitle, 
							CommonRes.PresetFileFilter + "|*.xml;*.vjpreset");
						if (presetFileName != null)
						{
							Ioc.Container.GetInstance<IPresetImportExport>().ImportPreset(presetFileName);
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
						Ioc.Container.GetInstance<IPresetImportExport>().ExportPreset(this.presetsService.SelectedPreset.Preset);
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

		public VCJob EncodeJob
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

				VCProfile encodingProfile = this.PresetsService.SelectedPreset.Preset.EncodingProfile.Clone();

				int title = this.SelectedTitle.Index;

				var job = new VCJob
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
							chapterEnd = this.SelectedTitle.ChapterList.Count;

							this.logger.LogError("Chapter range not selected. Using all chapters.");
						}
						else
						{
							chapterStart = this.SelectedStartChapter.ChapterNumber;
							chapterEnd = this.SelectedEndChapter.ChapterNumber;
						}

						if (chapterStart == 1 && chapterEnd == this.SelectedTitle.ChapterList.Count)
						{
							job.RangeType = VideoRangeType.All;
						}
						else
						{
							job.ChapterStart = chapterStart;
							job.ChapterEnd = chapterEnd;
						}

						break;
					case VideoRangeType.Seconds:
						TimeSpan timeRangeStart = this.TimeRangeStart;
						TimeSpan timeRangeEnd = this.TimeRangeEnd;

						if (timeRangeStart == TimeSpan.Zero && timeRangeEnd == this.SelectedTitle.Duration.ToSpan())
						{
							job.RangeType = VideoRangeType.All;
						}
						else
						{
							if (timeRangeEnd == TimeSpan.Zero)
							{
								throw new ArgumentException("Job end time of 0 seconds is invalid.");
							}

							job.SecondsStart = timeRangeStart.TotalSeconds;
							job.SecondsEnd = timeRangeEnd.TotalSeconds;
						}
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
			newEncodeJobVM.VideoSource = this.SourceData;
			newEncodeJobVM.VideoSourceMetadata = this.GetVideoSourceMetadata();
			newEncodeJobVM.SourceParentFolder = this.OutputPathVM.SourceParentFolder;
			newEncodeJobVM.ManualOutputPath = this.OutputPathVM.ManualOutputPath;
			newEncodeJobVM.NameFormatOverride = this.OutputPathVM.NameFormatOverride;
			newEncodeJobVM.PresetName = this.PresetsService.SelectedPreset.DisplayName;

			return newEncodeJobVM;
		}

		public void RemoveAudioChoice(AudioChoiceViewModel choice)
		{
			this.AudioChoices.Remove(choice);
		}

		// Brings up specified job for editing, doing a scan if necessary.
		public void EditJob(EncodeJobViewModel jobVM, bool isQueueItem = true)
		{
			VCJob job = jobVM.Job;

			if (this.PresetsService.SelectedPreset.IsModified)
			{
				MessageBoxResult dialogResult = Utilities.MessageBox.Show(this, MainRes.SaveChangesPresetMessage, MainRes.SaveChangesPresetTitle, MessageBoxButton.YesNoCancel);
				if (dialogResult == MessageBoxResult.Yes)
				{
					this.PresetsService.SavePreset();
				}
				else if (dialogResult == MessageBoxResult.No)
				{
					this.PresetsService.RevertPreset(userInitiated: false);
				}
				else if (dialogResult == MessageBoxResult.Cancel)
				{
					return;
				}
			}

			if (jobVM.VideoSource != null && jobVM.VideoSource == this.SourceData)
			{
				this.ApplyEncodeJobChoices(jobVM);
				Messenger.Default.Send(new RefreshPreviewMessage());
			}
			else
			{
				string jobPath = job.SourcePath;
				string jobRoot = Path.GetPathRoot(jobPath);

				// We need to reconstruct the source metadata since we've closed the scan since queuing.
				var videoSourceMetadata = new VideoSourceMetadata();

				switch (job.SourceType)
				{
					case SourceType.Dvd:
						DriveInformation driveInfo = this.DriveCollection.FirstOrDefault(d => string.Compare(d.RootDirectory, jobRoot, StringComparison.OrdinalIgnoreCase) == 0);
						if (driveInfo == null)
						{
							Ioc.Container.GetInstance<IMessageBoxService>().Show(MainRes.DiscNotInDriveError);
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

			string presetName = isQueueItem ? MainRes.PresetNameRestoredFromQueue : MainRes.PresetNameRestoredFromCompleted;

			var queuePreset = new PresetViewModel(
				new Preset
				{
					IsBuiltIn = false,
					IsModified = false,
					IsQueue = true,
					Name = presetName,
					EncodingProfile = jobVM.Job.EncodingProfile.Clone()
				});

			this.PresetsService.InsertQueuePreset(queuePreset);

			if (isQueueItem)
			{
				// Since it's been sent back for editing, remove the queue item
				this.ProcessingService.EncodeQueue.Remove(jobVM);
			}

			this.PresetsService.SaveUserPresets();
		}

		public void ScanFromAutoplay(string sourcePath)
		{
			if ((this.HasVideoSource || this.ScanningSource) && string.Compare(this.SourcePath, sourcePath, StringComparison.OrdinalIgnoreCase) == 0)
			{
				// We're already on this disc. Return.
				return;
			}

			//DriveInformation driveInfo = this.driveService.GetDriveInformationFromPath(sourcePath);
			if (this.HasVideoSource && !this.ScanningSource)
			{
				var messageResult = Ioc.Container.GetInstance<IMessageBoxService>().Show(
					this,
					MainRes.AutoplayDiscConfirmationMessage,
					CommonRes.ConfirmDialogTitle,
					MessageBoxButton.YesNo);

				if (messageResult == MessageBoxResult.Yes)
				{
					this.SetSource(sourcePath);
				}
			}
			else if (this.ScanningSource)
			{
				// If we're scanning already, cancel the scan and set a pending scan for the new path.
				this.pendingScan = sourcePath;
				this.CancelScanCommand.Execute(null);
			}
			else
			{
				this.SetSource(sourcePath);
			}
		}

		public void RefreshChapterMarkerUI()
		{
			this.RaisePropertyChanged(() => this.ShowChapterMarkerUI);
		}

		public void RefreshTrayIcon(bool minimized)
		{
			this.ShowTrayIcon = Config.MinimizeToTray && minimized;
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

						break;
					case VideoRangeType.Seconds:
						if (this.TimeRangeEnd >= this.TimeRangeStart)
						{
							selectedTime = this.TimeRangeEnd - this.TimeRangeStart;
						}

						break;
					case VideoRangeType.Frames:
						if (this.FramesRangeEnd >= this.FramesRangeStart)
						{
							selectedTime = TimeSpan.FromSeconds(((double)(this.FramesRangeEnd - this.FramesRangeStart)) / this.SelectedTitle.FrameRate.ToDouble());
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

			this.SetScanProgress(0);
			HandBrakeInstance oldInstance = this.scanInstance;

			this.logger.Log("Starting scan: " + path);

			this.scanInstance = new HandBrakeInstance();
			this.scanInstance.Initialize(Config.LogVerbosity);
			this.scanInstance.ScanProgress += (o, e) =>
			{
				this.SetScanProgress(e.Progress);
			};
			this.scanInstance.ScanCompleted += (o, e) =>
			{
				DispatchUtilities.Invoke(() =>
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

						if (this.pendingScan != null)
						{
							this.SetSource(this.pendingScan);

							this.pendingScan = null;
						}
					}
					else
					{
						this.SourceData = new VideoSource { Titles = this.scanInstance.Titles.TitleList, FeatureTitle = this.scanInstance.FeatureTitle };

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

							Picker picker = this.pickersService.SelectedPicker.Picker;
							if (picker.AutoQueueOnScan)
							{
								if (this.processingService.TryQueue() && picker.AutoEncodeOnScan && !this.processingService.Encoding)
								{
									this.processingService.EncodeCommand.Execute(null);
								}
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
			this.scanInstance.StartScan(path, Config.PreviewCount, TimeSpan.FromSeconds(Config.MinimumTitleLengthSeconds), 0);

			if (oldInstance != null)
			{
				oldInstance.Dispose();
			}
		}

		private void UpdateFromNewVideoSource()
		{
			SourceTitle selectTitle = null;

			if (this.sourceData != null && this.sourceData.Titles != null && this.sourceData.Titles.Count > 0)
			{
				selectTitle = Utilities.GetFeatureTitle(this.sourceData.Titles, this.sourceData.FeatureTitle);
			}

			if (selectTitle != null)
			{
				this.ScanError = false;
				this.SelectedTitle = selectTitle;
				this.RaisePropertyChanged(() => this.Titles);
				this.RaisePropertyChanged(() => this.TitleVisible);
				this.CloseVideoSourceCommand.RaiseCanExecuteChanged();
				this.RaisePropertyChanged(() => this.CanCloseVideoSource);
				this.RaisePropertyChanged(() => this.HasVideoSource);
				this.RaisePropertyChanged(() => this.SourceOptionsVisible);
			}
			else
			{
				this.sourceData = null;
				this.CloseVideoSourceCommand.RaiseCanExecuteChanged();
				this.RaisePropertyChanged(() => this.CanCloseVideoSource);
				this.RaisePropertyChanged(() => this.HasVideoSource);
				this.RaisePropertyChanged(() => this.SourceOptionsVisible);
				this.RaisePropertyChanged(() => this.SourcePicked);
				this.ScanError = true;
			}

			DispatchUtilities.BeginInvoke(() => Messenger.Default.Send(new VideoSourceChangedMessage()));
		}

		private void ClearVideoSource()
		{
			this.sourceData = null;
			this.SourceSelectionExpanded = false;
			this.CloseVideoSourceCommand.RaiseCanExecuteChanged();
			this.RaisePropertyChanged(() => this.CanCloseVideoSource);
			this.RaisePropertyChanged(() => this.HasVideoSource);
			this.RaisePropertyChanged(() => this.SourceOptionsVisible);
			this.RaisePropertyChanged(() => this.SourcePicked);

			DispatchUtilities.BeginInvoke(() => Messenger.Default.Send(new VideoSourceChangedMessage()));
			Messenger.Default.Send(new RefreshPreviewMessage());
		}

		private void LoadVideoSourceMetadata(VCJob job, VideoSourceMetadata metadata)
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

			VCJob job = jobVM.Job;

			// Title
			SourceTitle newTitle = this.sourceData.Titles.FirstOrDefault(t => t.Index == job.Title);
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
			if (this.rangeType == VideoRangeType.All)
			{
				if (this.selectedTitle.ChapterList.Count > 1)
				{
					this.rangeType = VideoRangeType.Chapters;
				}
				else
				{
					this.rangeType = VideoRangeType.Seconds;
				}
			}

			switch (this.rangeType)
			{
				case VideoRangeType.Chapters:
					if (job.ChapterStart > this.selectedTitle.ChapterList.Count ||
						job.ChapterEnd > this.selectedTitle.ChapterList.Count ||
						job.ChapterStart == 0 ||
						job.ChapterEnd == 0)
					{
						this.selectedStartChapter = this.StartChapters.FirstOrDefault(c => c.Chapter == this.selectedTitle.ChapterList[0]);
						this.selectedEndChapter = this.EndChapters.FirstOrDefault(c => c.Chapter == this.selectedTitle.ChapterList[this.selectedTitle.ChapterList.Count - 1]);
					}
					else
					{
						this.selectedStartChapter = this.StartChapters.FirstOrDefault(c => c.Chapter == this.selectedTitle.ChapterList[job.ChapterStart - 1]);
						this.selectedEndChapter = this.EndChapters.FirstOrDefault(c => c.Chapter == this.selectedTitle.ChapterList[job.ChapterEnd - 1]);
					}

					break;
				case VideoRangeType.Seconds:
					TimeSpan titleDuration = this.selectedTitle.Duration.ToSpan();

					if (titleDuration == TimeSpan.Zero)
					{
						throw new InvalidOperationException("Title's duration is 0, cannot continue.");
					}

					if (job.SecondsStart < titleDuration.TotalSeconds + 1 &&
						job.SecondsEnd < titleDuration.TotalSeconds + 1)
					{
						TimeSpan startTime = TimeSpan.FromSeconds(job.SecondsStart);
						TimeSpan endTime = TimeSpan.FromSeconds(job.SecondsEnd);

						if (endTime > titleDuration)
						{
							endTime = titleDuration;
						}

						if (startTime > endTime - Constants.TimeRangeBuffer)
						{
							startTime = endTime - Constants.TimeRangeBuffer;
						}

						if (startTime < TimeSpan.Zero)
						{
							startTime = TimeSpan.Zero;
						}

						this.SetRangeTimeStart(startTime);
						this.SetRangeTimeEnd(endTime);
					}
					else
					{
						this.SetRangeTimeStart(TimeSpan.Zero);
						this.SetRangeTimeEnd(titleDuration);
					}

					// We saw a problem with a job getting seconds 0-0 after a queue edit, add some sanity checking.
					if (this.TimeRangeEnd == TimeSpan.Zero)
					{
						this.SetRangeTimeEnd(titleDuration);
					}

					break;
				case VideoRangeType.Frames:
					this.framesRangeStart = job.FramesStart;
					this.framesRangeEnd = job.FramesEnd;

					break;
				default:
					throw new ArgumentOutOfRangeException();
			}

			// Audio tracks
			this.AudioChoices.Clear();
			foreach (int chosenTrack in job.ChosenAudioTracks)
			{
				if (chosenTrack <= this.selectedTitle.AudioList.Count)
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
					if (sourceSubtitle.TrackNumber <= this.selectedTitle.SubtitleList.Count)
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
				if (this.CustomChapterNames != null && this.selectedTitle.ChapterList.Count == this.CustomChapterNames.Count)
				{
					this.CustomChapterNames = job.CustomChapterNames;
				}
			}

			// Output path
			this.OutputPathVM.OutputPath = job.OutputPath;
			this.OutputPathVM.SourceParentFolder = jobVM.SourceParentFolder;
			this.OutputPathVM.ManualOutputPath = jobVM.ManualOutputPath;
			this.OutputPathVM.NameFormatOverride = jobVM.NameFormatOverride;

			// Encode profile handled above this in EditJob

			this.RaisePropertyChanged(() => this.SourceIcon);
			this.RaisePropertyChanged(() => this.SourceText);
			this.RaisePropertyChanged(() => this.SelectedTitle);
			this.RaisePropertyChanged(() => this.SelectedStartChapter);
			this.RaisePropertyChanged(() => this.SelectedEndChapter);
			this.RaisePropertyChanged(() => this.StartChapters);
			this.RaisePropertyChanged(() => this.EndChapters);
			this.RaisePropertyChanged(() => this.TimeRangeStart);
			this.RaisePropertyChanged(() => this.TimeRangeStartBar);
			this.RaisePropertyChanged(() => this.TimeRangeEnd);
			this.RaisePropertyChanged(() => this.TimeRangeEndBar);
			this.RaisePropertyChanged(() => this.FramesRangeStart);
			this.RaisePropertyChanged(() => this.FramesRangeEnd);
			this.RaisePropertyChanged(() => this.RangeType);
			this.RaisePropertyChanged(() => this.UsingChaptersRange);
			this.RaisePropertyChanged(() => this.RangeBarVisible);
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

			for (int i = 0; i < this.selectedTitle.ChapterList.Count; i++)
			{
				SourceChapter chapter = this.selectedTitle.ChapterList[i];

				this.startChapters.Add(new ChapterViewModel(chapter, i + 1));
				this.endChapters.Add(new ChapterViewModel(chapter, i + 1));
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
			var encodingWindow = WindowManager.FindWindow<EncodingViewModel>();
			if (encodingWindow != null)
			{
				encodingWindow.NotifyLengthChanged();
			}
		}

		private void UpdateDrives()
		{
			DispatchUtilities.BeginInvoke(() =>
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

			for (int i = 0; i < this.SelectedTitle.AudioList.Count; i++)
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
					this.timeBaseline = this.TimeRangeEnd;
				}
				else
				{
					this.timeBaseline = this.TimeRangeStart;
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

		// Sets both range time start properties passively (time box and bar)
		private void SetRangeTimeStart(TimeSpan timeStart)
		{
			this.timeRangeStart = timeStart;
			this.timeRangeStartBar = timeStart;
		}

		// Sets both range time end properties passively (time box and bar)
		private void SetRangeTimeEnd(TimeSpan timeEnd)
		{
			this.timeRangeEnd = timeEnd;
			this.timeRangeEndBar = timeEnd;
		}

		private void RefreshRangePreview()
		{
			this.RaisePropertyChanged(() => this.ChapterPreviewStart);
			this.RaisePropertyChanged(() => this.ChapterPreviewEnd);
			this.RaisePropertyChanged(() => this.RangePreviewStart);
			this.RaisePropertyChanged(() => this.RangePreviewEnd);
			this.RaisePropertyChanged(() => this.RangePreviewText);
			this.RaisePropertyChanged(() => this.RangePreviewLengthText);
		}

		private void RefreshRecentSourceOptions()
		{
			this.recentSourceOptions.Clear();

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
		}

		private TimeSpan GetChapterRangeDuration(int startChapter, int endChapter)
		{
			if (startChapter > endChapter ||
				endChapter > this.SelectedTitle.ChapterList.Count ||
				startChapter < 1)
			{
				return TimeSpan.Zero;
			}

			TimeSpan rangeTime = TimeSpan.Zero;

			for (int i = startChapter; i <= endChapter; i++)
			{
				rangeTime += this.SelectedTitle.ChapterList[i - 1].Duration.ToSpan();
			}

			return rangeTime;
		}
	}
}
