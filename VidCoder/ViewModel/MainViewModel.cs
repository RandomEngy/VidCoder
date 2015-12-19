using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Windows;
using System.Xml.Serialization;
using HandBrake.ApplicationServices.Interop;
using HandBrake.ApplicationServices.Interop.Json.Scan;
using Microsoft.Practices.Unity;
using Newtonsoft.Json;
using ReactiveUI;
using VidCoder.Automation;
using VidCoder.Extensions;
using VidCoder.Model;
using VidCoder.Resources;
using VidCoder.Services;
using VidCoder.Services.Windows;
using VidCoderCommon.Extensions;
using VidCoderCommon.Model;

namespace VidCoder.ViewModel
{
	public class MainViewModel : ReactiveObject
	{
		private HandBrakeInstance scanInstance;

		private IUpdater updater = Ioc.Get<IUpdater>();
		private ILogger logger = Ioc.Get<ILogger>();
		private OutputPathService outputPathService;
		private OutputSizeService outputSizeService;
		private PresetsService presetsService;
		private PickersService pickersService;
		private ProcessingService processingService;
		private IWindowManager windowManager;
		private TaskBarProgressTracker taskBarProgressTracker;

		private ObservableCollection<SourceOptionViewModel> sourceOptions;
		private ObservableCollection<SourceOptionViewModel> recentSourceOptions;

		private SourceTitle selectedTitle;
		private SourceTitle oldTitle;
		private List<int> angles;

		private VideoRangeType rangeType;
		private List<ComboChoice<VideoRangeType>> rangeTypeChoices;
		private TimeSpan timeBaseline;
		private int framesBaseline;

		private ReactiveList<AudioChoiceViewModel> audioChoices;

		private IDriveService driveService;
		private IList<DriveInformation> driveCollection;
		private string pendingScan;
		private bool scanCancelledFlag;

		public event EventHandler<EventArgs<string>> AnimationStarted;
		public event EventHandler ScanCancelled;
		public event EventHandler AudioChoiceChanged;

		public MainViewModel()
		{
			Ioc.Container.RegisterInstance(typeof(MainViewModel), this, new ContainerControlledLifetimeManager());

			// HasVideoSource
			this.WhenAnyValue(
				x => x.VideoSourceState,
				(videoSourceState) =>
				{
					return videoSourceState == VideoSourceState.ScannedSource;
				}).ToProperty(this, x => x.HasVideoSource, out this.hasVideoSource);

			// Titles
			this
				.WhenAnyValue(x => x.SourceData)
				.Select(sourceData =>
				{
					return sourceData?.Titles;
				}).ToProperty(this, x => x.Titles, out this.titles);

			// TitleVisible
			this.WhenAnyValue(x => x.HasVideoSource, x => x.SourceData, (hasVideoSource, sourceData) =>
			{
				return hasVideoSource && sourceData != null && sourceData.Titles.Count > 1;
			}).ToProperty(this, x => x.TitleVisible, out this.titleVisible);

			// SubtitlesSummary
			this.WhenAnyValue(x => x.VideoSourceState, x => x.CurrentSubtitles, x => x.SelectedTitle, (videoSourceState, currentSubtitles, selectedTitle) =>
			{
				if (videoSourceState != VideoSourceState.ScannedSource ||
					currentSubtitles == null || 
					selectedTitle == null ||
					((currentSubtitles.SourceSubtitles == null || currentSubtitles.SourceSubtitles.Count == 0) && 
						(currentSubtitles.SrtSubtitles == null || currentSubtitles.SrtSubtitles.Count == 0)))
				{
					return MainRes.NoneParen;
				}

				List<string> trackSummaries = new List<string>();

				if (currentSubtitles.SourceSubtitles != null)
				{
					foreach (SourceSubtitle sourceSubtitle in currentSubtitles.SourceSubtitles)
					{
						if (sourceSubtitle.TrackNumber == 0)
						{
							trackSummaries.Add(MainRes.ForeignAudioSearch);
						}
						else if (selectedTitle.SubtitleList != null && sourceSubtitle.TrackNumber <= selectedTitle.SubtitleList.Count)
						{
							trackSummaries.Add(selectedTitle.SubtitleList[sourceSubtitle.TrackNumber - 1].Language);
						}
					}
				}

				if (currentSubtitles.SrtSubtitles != null)
				{
					foreach (SrtSubtitle srtSubtitle in currentSubtitles.SrtSubtitles)
					{
						trackSummaries.Add(Languages.Get(srtSubtitle.LanguageCode).EnglishName);
					}
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
			}).ToProperty(this, x => x.SubtitlesSummary, out this.subtitlesSummary);

			// UsingChaptersRange
			this.WhenAnyValue(x => x.RangeType, rangeType =>
			{
				return rangeType == VideoRangeType.Chapters;
			}).ToProperty(this, x => x.UsingChaptersRange, out this.usingChaptersRange);

			// RangeBarVisible
			this.WhenAnyValue(x => x.RangeType, rangeType =>
			{
				return rangeType == VideoRangeType.Chapters || rangeType == VideoRangeType.Seconds;
			}).ToProperty(this, x => x.RangeBarVisible, out this.rangeBarVisible);

			// ChapterMarkersSummary
			this.WhenAnyValue(x => x.UseDefaultChapterNames, useDefaultChapterNames =>
			{
				if (useDefaultChapterNames)
				{
					return CommonRes.Default;
				}

				return CommonRes.Custom;
			}).ToProperty(this, x => x.ChapterMarkersSummary, out this.chapterMarkersSummary);

			// SourceIcon
			this.WhenAnyValue(x => x.SelectedSource, selectedSource =>
			{
				if (selectedSource == null)
				{
					return null;
				}

				switch (selectedSource.Type)
				{
					case SourceType.File:
						return "/Icons/video-file.png";
					case SourceType.VideoFolder:
						return "/Icons/folder.png";
					case SourceType.Dvd:
						if (selectedSource.DriveInfo.DiscType == DiscType.Dvd)
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
			}).ToProperty(this, x => x.SourceIcon, out this.sourceIcon);

			this.WhenAnyValue(x => x.SelectedSource.DriveInfo.Empty).Subscribe(observable =>
			{
				Debug.WriteLine(observable);
			});

			// SourceText
			this.WhenAnyValue(x => x.SelectedSource, x => x.SourcePath)
				.Skip(1)
				.Subscribe(_ =>
				{
					this.RaisePropertyChanged(nameof(this.SourceText));
				});

			this.WhenAnyValue(x => x.SelectedSource.DriveInfo.Empty)
				.Subscribe(_ =>
				{
					this.RaisePropertyChanged(nameof(this.SourceText));
				});

			// AngleVisible
			this.WhenAnyValue(x => x.SelectedTitle)
				.Select(selectedTitle =>
				{
					return selectedTitle != null && this.SelectedTitle.AngleCount > 1;
				}).ToProperty(this, x => x.AngleVisible, out this.angleVisible);

			// TotalChaptersText
			this.WhenAnyValue(x => x.SelectedTitle, x => x.RangeType, (selectedTitle, rangeType) =>
			{
				if (selectedTitle != null && rangeType == VideoRangeType.Chapters)
				{
					return string.Format(MainRes.ChaptersSummary, selectedTitle.ChapterList.Count);
				}

				return string.Empty;
			}).ToProperty(this, x => x.TotalChaptersText, out this.totalChaptersText);

			// CanCloseVideoSource
			IObservable<bool> canCloseVideoSourceObservable = this.WhenAnyValue(x => x.SelectedSource, x => x.VideoSourceState, (selectedSource, videoSourceState) =>
			{
				if (selectedSource == null)
				{
					return false;
				}

				if (videoSourceState == VideoSourceState.Scanning)
				{
					return false;
				}

				return true;
			});

			canCloseVideoSourceObservable.ToProperty(this, x => x.CanCloseVideoSource, out this.canCloseVideoSource);

			this.CloseVideoSource = ReactiveCommand.Create(canCloseVideoSourceObservable);
			this.CloseVideoSource.Subscribe(_ => this.CloseVideoSourceImpl());

			this.AddTrack = ReactiveCommand.Create(this
				.WhenAnyValue(x => x.SelectedTitle)
				.Select(selectedTitle =>
				{
					if (selectedTitle == null)
					{
						return true;
					}

					return selectedTitle.AudioList.Count > 0;
				}));
			this.AddTrack.Subscribe(_ => this.AddTrackImpl());

			this.OpenEncodingWindow = ReactiveCommand.Create();
			this.OpenEncodingWindow.Subscribe(_ => this.OpenEncodingWindowImpl());

			this.OpenOptions = ReactiveCommand.Create();
			this.OpenOptions.Subscribe(_ => this.OpenOptionsImpl());

			this.OpenUpdates = ReactiveCommand.Create();
			this.OpenUpdates.Subscribe(_ => this.OpenUpdatesImpl());

			this.ToggleSourceMenu = ReactiveCommand.Create(this
				.WhenAnyValue(x => x.VideoSourceState)
				.Select(videoSourceState => videoSourceState != VideoSourceState.Scanning));
			this.ToggleSourceMenu.Subscribe(_ => this.ToggleSourceMenuImpl());

			this.OpenSubtitlesDialog = ReactiveCommand.Create();
			this.OpenSubtitlesDialog.Subscribe(_ => this.OpenSubtitlesDialogImpl());

			this.OpenChaptersDialog = ReactiveCommand.Create();
			this.OpenChaptersDialog.Subscribe(_ => this.OpenChaptersDialogImpl());

			this.OpenAboutDialog = ReactiveCommand.Create();
			this.OpenAboutDialog.Subscribe(_ => this.OpenAboutDialogImpl());

			this.CancelScan = ReactiveCommand.Create();
			this.CancelScan.Subscribe(_ => this.CancelScanImpl());

			var notScanningObservable = this
				.WhenAnyValue(x => x.VideoSourceState)
				.Select(videoSourceState => videoSourceState != VideoSourceState.Scanning);
			this.OpenFile = ReactiveCommand.Create(notScanningObservable);
			this.OpenFile.Subscribe(_ => this.OpenFileImpl());

			this.OpenFolder = ReactiveCommand.Create(notScanningObservable);
			this.OpenFolder.Subscribe(_ => this.OpenFolderImpl());

			this.CustomizeQueueColumns = ReactiveCommand.Create();
			this.CustomizeQueueColumns.Subscribe(_ => this.CustomizeQueueColumnsImpl());

			this.ImportPreset = ReactiveCommand.Create();
			this.ImportPreset.Subscribe(_ => this.ImportPresetImpl());

			this.ExportPreset = ReactiveCommand.Create();
			this.ExportPreset.Subscribe(_ => this.ExportPresetImpl());

			this.OpenHomepage = ReactiveCommand.Create();
			this.OpenHomepage.Subscribe(_ => this.OpenHomepageImpl());

			this.ReportBug = ReactiveCommand.Create();
			this.ReportBug.Subscribe(_ => this.ReportBugImpl());

			this.Exit = ReactiveCommand.Create();
			this.Exit.Subscribe(_ => this.ExitImpl());
			
			this.outputPathService = Ioc.Get<OutputPathService>();
			this.outputSizeService = Ioc.Get<OutputSizeService>();
			this.processingService = Ioc.Get<ProcessingService>();
			this.presetsService = Ioc.Get<PresetsService>();
			this.pickersService = Ioc.Get<PickersService>();
			this.windowManager = Ioc.Get<IWindowManager>();
			this.taskBarProgressTracker = new TaskBarProgressTracker();

			// WindowTitle
			this.processingService.WhenAnyValue(x => x.Encoding, x => x.OverallEncodeProgressFraction, (encoding, progressFraction) =>
			{
				if (!encoding)
				{
					return "VidCoder";
				}

				return $"VidCoder - {progressFraction:P1}";
			}).ToProperty(this, x => x.WindowTitle, out this.windowTitle);

			// ShowChapterMarkerUI
			this.presetsService
				.WhenAnyValue(x => x.SelectedPreset.Preset.EncodingProfile.IncludeChapterMarkers)
				.ToProperty(this, x => x.ShowChapterMarkerUI, out this.showChapterMarkerUI);

			this.updater.CheckUpdates();

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

			this.driveService = Ioc.Get<IDriveService>();

			this.DriveCollection = this.driveService.GetDiscInformation();

			this.audioChoices = new ReactiveList<AudioChoiceViewModel>();
			this.audioChoices.ChangeTrackingEnabled = true;

			this.audioChoices.Changed
				.Subscribe(_ =>
				{
					this.AudioChoiceChanged?.Invoke(this, new EventArgs());
				});

			this.audioChoices.ItemChanged
				.Subscribe(_ =>
				{
					this.AudioChoiceChanged?.Invoke(this, new EventArgs());
				});

			this.WindowMenuItems = new List<WindowMenuItemViewModel>();
			foreach (var definition in WindowManager.Definitions.Where(d => d.InMenu))
			{
				this.WindowMenuItems.Add(new WindowMenuItemViewModel(definition));
			}
		}

		public IMainView View
		{
			get { return this.view; }
			set
			{
				this.view = value;
				this.view.RangeControlGotFocus += this.OnRangeControlGotFocus;
			}
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
				}
				else if (!this.SelectedSource.DriveInfo.Empty && !currentDrivePresent)
				{
					// If we had an item and the drive is now gone, update and remove the video source.
					DriveInformation emptyDrive = this.SelectedSource.DriveInfo;
					emptyDrive.Empty = true;

					this.VideoSourceState = VideoSourceState.EmptyDrive;
					this.SourceData = null;
					this.SelectedTitle = null;
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
			this.windowManager.Focus(this);

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
			this.windowManager.OpenTrackedWindows();
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
			}

			this.windowManager.CloseTrackedWindows();

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

		private ObservableAsPropertyHelper<string> windowTitle;
		public string WindowTitle => this.windowTitle.Value;

		private string sourcePath;
		public string SourcePath
		{
			get { return this.sourcePath; }
			private set { this.RaiseAndSetIfChanged(ref this.sourcePath, value); }
		}

		public string SourceName { get; private set; }

		public OutputPathService OutputPathService
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

		public IList<WindowMenuItemViewModel> WindowMenuItems { get; private set; }

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

		private VideoSourceState videoSourceState;
		public VideoSourceState VideoSourceState
		{
			get { return this.videoSourceState; }
			set { this.RaiseAndSetIfChanged(ref this.videoSourceState, value); }
		}

		private bool sourceSelectionExpanded;
		public bool SourceSelectionExpanded
		{
			get { return this.sourceSelectionExpanded; }
			set { this.RaiseAndSetIfChanged(ref this.sourceSelectionExpanded, value); }
		}

		private ObservableAsPropertyHelper<string> sourceIcon;
		public string SourceIcon => this.sourceIcon.Value;

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

		private SourceOption selectedSource;
		public SourceOption SelectedSource
		{
			get { return this.selectedSource; }
			set { this.RaiseAndSetIfChanged(ref this.selectedSource, value); }
		}

		private double scanProgressFraction;

		/// <summary>
		/// Gets or sets the scan progress fraction.
		/// </summary>
		public double ScanProgressFraction
		{
			get { return this.scanProgressFraction; }
			set { this.RaiseAndSetIfChanged(ref this.scanProgressFraction, value); }
		}

		private ObservableAsPropertyHelper<bool> hasVideoSource;
		public bool HasVideoSource => this.hasVideoSource.Value;

		private ObservableAsPropertyHelper<bool> canCloseVideoSource;
		public bool CanCloseVideoSource => this.canCloseVideoSource.Value;

		/// <summary>
		/// If true, EncodeJob objects can be safely obtained from this class.
		/// </summary>
		public bool JobCreationAvailable { get; set; }

		private ObservableAsPropertyHelper<List<SourceTitle>> titles;
		public List<SourceTitle> Titles => this.titles.Value;

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
						this.OutputPathService.ManualOutputPath = false;
						this.OutputPathService.NameFormatOverride = null;
						this.OutputPathService.SourceParentFolder = null;
					}

					// Save old subtitles
					VCSubtitles oldSubtitles = this.CurrentSubtitles;
					this.CurrentSubtitles = new VCSubtitles { SourceSubtitles = new List<SourceSubtitle>(), SrtSubtitles = new List<SrtSubtitle>() };

					Picker picker = this.pickersService.SelectedPicker.Picker;

					// Audio selection
					using (this.AudioChoices.SuppressChangeNotifications())
					{
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
					this.RaisePropertyChanged(nameof(this.SelectedStartChapter));
					this.RaisePropertyChanged(nameof(this.SelectedEndChapter));

					this.SetRangeTimeStart(TimeSpan.Zero);
					this.SetRangeTimeEnd(this.selectedTitle.Duration.ToSpan());
					this.RaisePropertyChanged(nameof(this.TimeRangeStart));
					this.RaisePropertyChanged(nameof(this.TimeRangeStartBar));
					this.RaisePropertyChanged(nameof(this.TimeRangeEnd));
					this.RaisePropertyChanged(nameof(this.TimeRangeEndBar));

					this.framesRangeStart = 0;
					this.framesRangeEnd = this.selectedTitle.GetEstimatedFrames();
					this.RaisePropertyChanged(nameof(this.FramesRangeStart));
					this.RaisePropertyChanged(nameof(this.FramesRangeEnd));

					this.PopulateAnglesList();

					this.angle = 1;

					this.RaisePropertyChanged(nameof(this.Angles));
					this.RaisePropertyChanged(nameof(this.Angle));


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

				// Custom chapter names are thrown out when switching titles.
				this.CustomChapterNames = null;

				this.OutputPathService.GenerateOutputFileName();

				//this.RaisePropertyChanged(nameof(this.StartChapters));
				//this.RaisePropertyChanged(nameof(this.EndChapters));
				this.RaisePropertyChanged(nameof(this.SelectedTitle));

				this.outputSizeService.Refresh();

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

		private ObservableAsPropertyHelper<bool> angleVisible;
		public bool AngleVisible => this.angleVisible.Value;

		private int angle;
		public int Angle
		{
			get { return this.angle; }
			set { this.RaiseAndSetIfChanged(ref this.angle, value); }
		}

		private VCSubtitles currentSubtitles;
		public VCSubtitles CurrentSubtitles
		{
			get { return this.currentSubtitles; }
			set { this.RaiseAndSetIfChanged(ref this.currentSubtitles, value); }
		}

		private ObservableAsPropertyHelper<string> subtitlesSummary;
		public string SubtitlesSummary => this.subtitlesSummary.Value;

		private bool useDefaultChapterNames;
		public bool UseDefaultChapterNames
		{
			get { return this.useDefaultChapterNames; }
			set { this.RaiseAndSetIfChanged(ref this.useDefaultChapterNames, value); }
		}

		private List<string> customChapterNames;
		public List<string> CustomChapterNames
		{
			get { return this.customChapterNames; }
			set { this.RaiseAndSetIfChanged(ref this.customChapterNames, value); }
		}

		private ObservableAsPropertyHelper<string> chapterMarkersSummary;
		public string ChapterMarkersSummary => this.chapterMarkersSummary.Value;

		private ObservableAsPropertyHelper<bool> showChapterMarkerUI;
		public bool ShowChapterMarkerUI => this.showChapterMarkerUI.Value;

		private ObservableAsPropertyHelper<bool> titleVisible;
		public bool TitleVisible
		{
			get { return this.titleVisible.Value; }
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

					this.OutputPathService.GenerateOutputFileName();
					this.RaisePropertyChanged();
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

		private ObservableAsPropertyHelper<bool> usingChaptersRange;
		public bool UsingChaptersRange => this.usingChaptersRange.Value;

		private ObservableAsPropertyHelper<bool> rangeBarVisible;
		public bool RangeBarVisible => this.rangeBarVisible.Value;

		private ReactiveList<ChapterViewModel> startChapters;
		public ReactiveList<ChapterViewModel> StartChapters
		{
			get { return this.startChapters; }
			set { this.RaiseAndSetIfChanged(ref this.startChapters, value); }
		}

		private ReactiveList<ChapterViewModel> endChapters;
		public ReactiveList<ChapterViewModel> EndChapters
		{
			get { return this.endChapters; }
			set { this.RaiseAndSetIfChanged(ref this.endChapters, value); }
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

				this.OutputPathService.GenerateOutputFileName();

				this.RaisePropertyChanged();
				this.RaisePropertyChanged(nameof(this.TimeRangeStart));
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

				this.OutputPathService.GenerateOutputFileName();

				this.RaisePropertyChanged();
				this.RaisePropertyChanged(nameof(this.TimeRangeEnd));
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
					this.RaisePropertyChanged(nameof(this.TimeRangeEnd));
					this.RaisePropertyChanged(nameof(this.TimeRangeEndBar));
				}

				this.OutputPathService.GenerateOutputFileName();

				this.RaisePropertyChanged();
				this.RaisePropertyChanged(nameof(this.TimeRangeStartBar));
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
					this.RaisePropertyChanged(nameof(this.TimeRangeStart));
					this.RaisePropertyChanged(nameof(this.TimeRangeStartBar));
				}

				this.OutputPathService.GenerateOutputFileName();

				this.RaisePropertyChanged(nameof(this.TimeRangeEnd));
				this.RaisePropertyChanged(nameof(this.TimeRangeEndBar));
				this.RefreshRangePreview();
				this.ReportLengthChanged();
			}
		}

		private int framesRangeStart;
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
					this.RaisePropertyChanged(nameof(this.FramesRangeEnd));
				}

				this.OutputPathService.GenerateOutputFileName();

				this.RaisePropertyChanged();
				this.RefreshRangePreview();
				this.ReportLengthChanged();
			}
		}

		private int framesRangeEnd;
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
					this.RaisePropertyChanged(nameof(this.FramesRangeStart));
				}

				this.OutputPathService.GenerateOutputFileName();

				this.RaisePropertyChanged();
				this.RefreshRangePreview();
				this.ReportLengthChanged();
			}
		}

		private ChapterViewModel selectedStartChapter;
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

				this.OutputPathService.GenerateOutputFileName();

				this.RaisePropertyChanged();
				this.RefreshRangePreview();
				this.ReportLengthChanged();
			}
		}

		private ChapterViewModel selectedEndChapter;
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

				this.OutputPathService.GenerateOutputFileName();

				this.RaisePropertyChanged();
				this.RefreshRangePreview();
				this.ReportLengthChanged();
			}
		}

		private ObservableAsPropertyHelper<string> totalChaptersText;
		public string TotalChaptersText => this.totalChaptersText.Value;

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

		public ReactiveList<AudioChoiceViewModel> AudioChoices
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

		// Populated when a scan finishes and cleared out when a new scan starts
		private VideoSource sourceData;
		public VideoSource SourceData
		{
			get { return this.sourceData; }
			set { this.RaiseAndSetIfChanged(ref this.sourceData, value); }
		}

		private bool showTrayIcon;
		private IMainView view;

		public bool ShowTrayIcon
		{
			get { return this.showTrayIcon; }
			set { this.RaiseAndSetIfChanged(ref this.showTrayIcon, value); }
		}

		public string TrayIconToolTip
		{
			get
			{
				return "VidCoder " + Utilities.CurrentVersion;
			}
		}

		public ReactiveCommand<object> AddTrack { get; }
		private void AddTrackImpl()
		{
			var newAudioChoice = new AudioChoiceViewModel();
			newAudioChoice.SelectedIndex = this.GetFirstUnusedAudioTrack();
			this.AudioChoices.Add(newAudioChoice);
		}

		public ReactiveCommand<object> OpenEncodingWindow { get; }
		private void OpenEncodingWindowImpl()
		{
			this.windowManager.OpenOrFocusWindow(typeof(EncodingWindowViewModel));
		}

		public ReactiveCommand<object> OpenOptions { get; }
		private void OpenOptionsImpl()
		{
			this.windowManager.OpenDialog<OptionsDialogViewModel>();
		}

		public ReactiveCommand<object> OpenUpdates { get; }
		private void OpenUpdatesImpl()
		{
			Config.OptionsDialogLastTab = OptionsDialogViewModel.UpdatesTabIndex;
			this.windowManager.OpenDialog<OptionsDialogViewModel>();
		}

		public ReactiveCommand<object> ToggleSourceMenu { get; }
		private void ToggleSourceMenuImpl()
		{
			this.SourceSelectionExpanded = !this.SourceSelectionExpanded;
		}

		public ReactiveCommand<object> OpenSubtitlesDialog { get; }
		private void OpenSubtitlesDialogImpl()
		{
			var subtitleViewModel = new SubtitleDialogViewModel(this.CurrentSubtitles);
			this.windowManager.OpenDialog(subtitleViewModel);

			if (subtitleViewModel.DialogResult)
			{
				this.CurrentSubtitles = subtitleViewModel.ChosenSubtitles;
			}
		}

		public ReactiveCommand<object> OpenChaptersDialog { get; }
		private void OpenChaptersDialogImpl()
		{
			var chaptersVM = new ChapterMarkersDialogViewModel(this.SelectedTitle.ChapterList, this.CustomChapterNames, this.UseDefaultChapterNames);
			this.windowManager.OpenDialog(chaptersVM);

			if (chaptersVM.DialogResult)
			{
				this.UseDefaultChapterNames = chaptersVM.UseDefaultNames;
				if (!chaptersVM.UseDefaultNames)
				{
					this.CustomChapterNames = chaptersVM.ChapterNamesList;
				}
			}
		}

		public ReactiveCommand<object> OpenAboutDialog { get; }
		private void OpenAboutDialogImpl()
		{
			this.windowManager.OpenDialog(new AboutDialogViewModel());
		}

		public ReactiveCommand<object> CancelScan { get; }
		private void CancelScanImpl()
		{
			this.scanCancelledFlag = true;
			this.ScanInstance.StopScan();
		}

		public ReactiveCommand<object> OpenFile { get; }
		private void OpenFileImpl()
		{
			this.SetSourceFromFile();
		}

		public ReactiveCommand<object> OpenFolder { get; }
		private void OpenFolderImpl()
		{
			this.SetSourceFromFolder();
		}

		public ReactiveCommand<object> CloseVideoSource { get; }
		private void CloseVideoSourceImpl()
		{
			this.VideoSourceState = VideoSourceState.Choices;
			this.SourceData = null;
			this.SelectedSource = null;
			this.SourceSelectionExpanded = false;

			this.RefreshRecentSourceOptions();

			this.SelectedTitle = null;

			HandBrakeInstance oldInstance = this.scanInstance;
			this.scanInstance = null;
			oldInstance?.Dispose();
		}

		public ReactiveCommand<object> CustomizeQueueColumns { get; }
		private void CustomizeQueueColumnsImpl()
		{
			// Send a request that the view save the column sizes
			this.View.SaveQueueColumns();

			// Show the queue columns dialog
			var queueDialog = new QueueColumnsDialogViewModel();
			this.windowManager.OpenDialog(queueDialog);

			if (queueDialog.DialogResult)
			{
				// Apply new columns
				Config.QueueColumns = queueDialog.NewColumns;
				this.View.ApplyQueueColumns();
			}
		}

		public ReactiveCommand<object> ImportPreset { get; }
		private void ImportPresetImpl()
		{
			string presetFileName = FileService.Instance.GetFileNameLoad(
				null,
				MainRes.ImportPresetFilePickerTitle,
				CommonRes.PresetFileFilter + "|*.xml;*.vjpreset");
			if (presetFileName != null)
			{
				try
				{
					Preset preset = Ioc.Get<IPresetImportExport>().ImportPreset(presetFileName);
					Ioc.Get<IMessageBoxService>().Show(string.Format(MainRes.PresetImportSuccessMessage, preset.Name), CommonRes.Success, System.Windows.MessageBoxButton.OK);
				}
				catch (Exception)
				{
					Ioc.Get<IMessageBoxService>().Show(MainRes.PresetImportErrorMessage, MainRes.ImportErrorTitle, System.Windows.MessageBoxButton.OK);
				}
			}
		}

		public ReactiveCommand<object> ExportPreset { get; }
		private void ExportPresetImpl()
		{
			Ioc.Get<IPresetImportExport>().ExportPreset(this.presetsService.SelectedPreset.Preset);
		}

		public ReactiveCommand<object> OpenHomepage { get; }
		private void OpenHomepageImpl()
		{
			FileService.Instance.LaunchUrl("http://vidcoder.net/");
		}

		public ReactiveCommand<object> ReportBug { get; }
		private void ReportBugImpl()
		{
			FileService.Instance.LaunchUrl("https://github.com/RandomEngy/VidCoder/issues/new");
		}

		public ReactiveCommand<object> Exit { get; }
		private void ExitImpl()
		{
			this.windowManager.Close(this);
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

				string outputPath = this.OutputPathService.OutputPath;

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
					case VideoRangeType.All:
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
			newEncodeJobVM.SourceParentFolder = this.OutputPathService.SourceParentFolder;
			newEncodeJobVM.ManualOutputPath = this.OutputPathService.ManualOutputPath;
			newEncodeJobVM.NameFormatOverride = this.OutputPathService.NameFormatOverride;
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

			if (this.PresetsService.SelectedPreset.Preset.IsModified)
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
							Ioc.Get<IMessageBoxService>().Show(MainRes.DiscNotInDriveError);
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
			if ((this.VideoSourceState == VideoSourceState.Scanning || this.VideoSourceState == VideoSourceState.ScannedSource) && string.Compare(this.SourcePath, sourcePath, StringComparison.OrdinalIgnoreCase) == 0)
			{
				// We're already on this disc. Return.
				return;
			}

			if (this.VideoSourceState == VideoSourceState.ScannedSource)
			{
				var messageResult = Ioc.Get<IMessageBoxService>().Show(
					this,
					MainRes.AutoplayDiscConfirmationMessage,
					CommonRes.ConfirmDialogTitle,
					MessageBoxButton.YesNo);

				if (messageResult == MessageBoxResult.Yes)
				{
					this.SetSource(sourcePath);
				}
			}
			else if (this.VideoSourceState == VideoSourceState.Scanning)
			{
				// If we're scanning already, cancel the scan and set a pending scan for the new path.
				this.pendingScan = sourcePath;
				this.CancelScanImpl();
			}
			else
			{
				this.SetSource(sourcePath);
			}
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
			this.VideoSourceState = VideoSourceState.Scanning;
			this.ClearVideoSource();

			this.ScanProgressFraction = 0;
			HandBrakeInstance oldInstance = this.scanInstance;

			this.logger.Log("Starting scan: " + path);

			this.scanInstance = new HandBrakeInstance();
			this.scanInstance.Initialize(Config.LogVerbosity);
			this.scanInstance.ScanProgress += (o, e) =>
			{
				this.ScanProgressFraction = e.Progress;
			};
			this.scanInstance.ScanCompleted += (o, e) =>
			{
				DispatchUtilities.Invoke(() =>
				{
					if (this.scanCancelledFlag)
					{
						this.SelectedSource = null;

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
						else
						{
							this.VideoSourceState = VideoSourceState.Choices;
						}
					}
					else
					{
						this.UpdateFromNewVideoSource(new VideoSource { Titles = this.scanInstance.Titles.TitleList, FeatureTitle = this.scanInstance.FeatureTitle });

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
									this.processingService.Encode.Execute(null);
								}
							}
						}

						this.logger.Log("Scan completed");
					}

					this.logger.Log("");
				});
			};

			this.scanCancelledFlag = false;
			this.scanInstance.StartScan(path, Config.PreviewCount, TimeSpan.FromSeconds(Config.MinimumTitleLengthSeconds), 0);

			if (oldInstance != null)
			{
				oldInstance.Dispose();
			}
		}

		private void UpdateFromNewVideoSource(VideoSource videoSource)
		{
			SourceTitle selectTitle = null;

			if (videoSource != null && videoSource.Titles != null && videoSource.Titles.Count > 0)
			{
				selectTitle = Utilities.GetFeatureTitle(videoSource.Titles, videoSource.FeatureTitle);
			}

			if (selectTitle != null)
			{
				this.SourceData = videoSource;
				this.VideoSourceState = VideoSourceState.ScannedSource;

				this.SelectedTitle = selectTitle;
			}
			else
			{
				this.SourceData = null;
				this.VideoSourceState = VideoSourceState.Choices;

				// Raise error
				Ioc.Get<IMessageBoxService>().Show(this, MainRes.ScanErrorLabel);
			}
		}

		private void ClearVideoSource()
		{
			this.SourceData = null;
			this.SourceSelectionExpanded = false;
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
			this.OutputPathService.OutputPath = job.OutputPath;
			this.OutputPathService.SourceParentFolder = jobVM.SourceParentFolder;
			this.OutputPathService.ManualOutputPath = jobVM.ManualOutputPath;
			this.OutputPathService.NameFormatOverride = jobVM.NameFormatOverride;

			// Encode profile handled above this in EditJob

			this.RaisePropertyChanged(nameof(this.SelectedSource));
			this.RaisePropertyChanged(nameof(this.SelectedTitle));
			this.RaisePropertyChanged(nameof(this.SelectedStartChapter));
			this.RaisePropertyChanged(nameof(this.SelectedEndChapter));
			//this.RaisePropertyChanged(nameof(this.StartChapters));
			//this.RaisePropertyChanged(nameof(this.EndChapters));
			this.RaisePropertyChanged(nameof(this.TimeRangeStart));
			this.RaisePropertyChanged(nameof(this.TimeRangeStartBar));
			this.RaisePropertyChanged(nameof(this.TimeRangeEnd));
			this.RaisePropertyChanged(nameof(this.TimeRangeEndBar));
			this.RaisePropertyChanged(nameof(this.FramesRangeStart));
			this.RaisePropertyChanged(nameof(this.FramesRangeEnd));
			this.RaisePropertyChanged(nameof(this.RangeType));
			this.RaisePropertyChanged(nameof(this.Angle));
			this.RaisePropertyChanged(nameof(this.Angles));

			this.RefreshRangePreview();
		}

		private void PopulateChapterSelectLists()
		{
			var newStartChapterList = new ReactiveList<ChapterViewModel>();
			var newEndChapterList = new ReactiveList<ChapterViewModel>();

			for (int i = 0; i < this.selectedTitle.ChapterList.Count; i++)
			{
				SourceChapter chapter = this.selectedTitle.ChapterList[i];

				newStartChapterList.Add(new ChapterViewModel(chapter, i + 1));
				newEndChapterList.Add(new ChapterViewModel(chapter, i + 1));
			}

			newStartChapterList.ChangeTrackingEnabled = true;
			newEndChapterList.ChangeTrackingEnabled = true;

			newStartChapterList.ItemChanged
				.Where(x => x.PropertyName == nameof(ChapterViewModel.IsHighlighted))
				.Subscribe(_ =>
				{
					this.RefreshRangePreview();
				});

			newEndChapterList.ItemChanged
				.Where(x => x.PropertyName == nameof(ChapterViewModel.IsHighlighted))
				.Subscribe(_ =>
				{
					this.RefreshRangePreview();
				});

			// We can't re-use the same list because the ComboBox control flyout sizes itself once and doesn't update when items are added.
			this.StartChapters = newStartChapterList;
			this.EndChapters = newEndChapterList;
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
			var encodingWindow = this.windowManager.Find<EncodingWindowViewModel>();
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

		private void OnRangeControlGotFocus(object sender, RangeFocusEventArgs eventArgs)
		{
			if (!eventArgs.GotFocus)
			{
				return;
			}

			if (eventArgs.RangeType == VideoRangeType.Seconds)
			{
				if (eventArgs.Start)
				{
					this.timeBaseline = this.TimeRangeEnd;
				}
				else
				{
					this.timeBaseline = this.TimeRangeStart;
				}
			}
			else if (eventArgs.RangeType == VideoRangeType.Frames)
			{
				if (eventArgs.Start)
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
			this.RaisePropertyChanged(nameof(this.ChapterPreviewStart));
			this.RaisePropertyChanged(nameof(this.ChapterPreviewEnd));
			this.RaisePropertyChanged(nameof(this.RangePreviewStart));
			this.RaisePropertyChanged(nameof(this.RangePreviewEnd));
			this.RaisePropertyChanged(nameof(this.RangePreviewText));
			this.RaisePropertyChanged(nameof(this.RangePreviewLengthText));
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
