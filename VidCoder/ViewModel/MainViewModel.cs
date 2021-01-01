using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SQLite;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using DynamicData;
using DynamicData.Binding;
using HandBrake.Interop.Interop;
using HandBrake.Interop.Interop.Json.Scan;
using HandBrake.Interop.Interop.Model.Encoding;
using Microsoft.AnyContainer;
using ReactiveUI;
using VidCoder.Automation;
using VidCoder.Extensions;
using VidCoder.Model;
using VidCoder.Resources;
using VidCoder.Services;
using VidCoder.Services.Notifications;
using VidCoder.Services.Windows;
using VidCoder.ViewModel.DataModels;
using VidCoderCommon;
using VidCoderCommon.Extensions;
using VidCoderCommon.Model;

namespace VidCoder.ViewModel
{
	public class MainViewModel : ReactiveObject, IClosableWindow
	{
		private HandBrakeInstance scanInstance;
		private IAppLogger scanLogger;

		private IUpdater updater = StaticResolver.Resolve<IUpdater>();
		private IAppLogger logger = StaticResolver.Resolve<IAppLogger>();
		private PreviewUpdateService previewUpdateService;
		private IWindowManager windowManager;
		private ObservableCollection<SourceOptionViewModel> sourceOptions;
		private ObservableCollection<SourceOptionViewModel> recentSourceOptions;

		private SourceTitleViewModel oldTitle;
		private List<int> angles;

		private VideoRangeType rangeType;
		private List<ComboChoice<VideoRangeType>> rangeTypeChoices;
		private TimeSpan timeBaseline;
		private int framesBaseline;
		private IDriveService driveService;
		private IList<DriveInformation> driveCollection;
		private string pendingScan;

		private IObservable<bool> canCloseVideoSourceObservable;

		public event EventHandler<EventArgs<string>> AnimationStarted;
		public event EventHandler ScanCanceled;

		public MainViewModel()
		{
			Ioc.Container.RegisterSingleton<MainViewModel>(() => this);

			this.VideoExpanded = Config.VideoExpanded;
			this.AudioExpanded = Config.AudioExpanded;
			this.SubtitlesExpanded = Config.SubtitlesExpanded;

			this.SourceSubtitles.Connect().Bind(this.SourceSubtitlesBindable).Subscribe();
			this.FileSubtitles.Connect().Bind(this.FileSubtitlesBindable).Subscribe();

			// HasVideoSource
			this.WhenAnyValue(x => x.VideoSourceState)
				.Select(videoSourceState =>
				{
					return videoSourceState == VideoSourceState.ScannedSource;
				}).ToProperty(this, x => x.HasVideoSource, out this.hasVideoSource, scheduler: Scheduler.Immediate);

			// Titles
			this.WhenAnyValue(x => x.SourceData)
				.Select(sourceData =>
				{
					if (sourceData == null)
					{
						return null;
					}

					return sourceData.Titles.Select(title => new SourceTitleViewModel(title)).ToList();
				}).ToProperty(this, x => x.Titles, out this.titles, scheduler: Scheduler.Immediate);

			// TitleVisible
			this.WhenAnyValue(x => x.HasVideoSource, x => x.SourceData, (hasVideoSource, sourceData) =>
			{
				return hasVideoSource && sourceData != null && sourceData.Titles.Count > 1;
			}).ToProperty(this, x => x.TitleVisible, out this.titleVisible);

			IObservable<int> similarTitlesObservable = this.WhenAnyValue(x => x.Titles, x => x.SelectedTitle, (titles, selectedTitle) =>
			{
				if (titles == null || selectedTitle == null)
				{
					return 0;
				}

				int similarTitles = 0;

				TimeSpan duration = selectedTitle.Duration;
				foreach (var title in titles)
				{
					if (title == selectedTitle)
					{
						continue;
					}

					TimeSpan difference = duration - title.Duration;
					if (Math.Abs(difference.TotalMinutes) <= 5)
					{
						similarTitles++;
					}
				}

				return similarTitles;
			});

			// TitleWarning
			similarTitlesObservable.Select(similarTitles =>
			{
				return string.Format(MainRes.SimilarTitlesToolTip, similarTitles);
			}).ToProperty(this, x => x.TitleWarning, out this.titleWarning);

			// TitleWarningVisible
			similarTitlesObservable.Select(similarTitles =>
			{
				return similarTitles > 12;
			}).ToProperty(this, x => x.TitleWarningVisible, out this.titleWarningVisible);

			// UsingChaptersRange
			this.WhenAnyValue(x => x.RangeType, rangeType =>
			{
				return rangeType == VideoRangeType.Chapters;
			}).ToProperty(this, x => x.UsingChaptersRange, out this.usingChaptersRange);

			// RangeBarVisible
			this.WhenAnyValue(x => x.RangeType, x => x.SelectedTitle.Duration, (rangeType, duration) =>
			{
				return duration > TimeSpan.Zero && (rangeType == VideoRangeType.Chapters || rangeType == VideoRangeType.Seconds);
			}).ToProperty(this, x => x.RangeBarVisible, out this.rangeBarVisible);

			// ChapterMarkersSummary
			this.WhenAnyValue(x => x.UseDefaultChapterNames, useDefaultChapterNames =>
			{
				if (useDefaultChapterNames)
				{
					return CommonRes.Default;
				}

				return CommonRes.Custom;
			}).ToProperty(this, x => x.ChapterMarkersSummary, out this.chapterMarkersSummary, deferSubscription: true);

			// SourceIcon
			this.WhenAnyValue(x => x.SelectedSource, selectedSource =>
			{
				if (selectedSource == null)
				{
					return "/Icons/video-file.png";
				}

				switch (selectedSource.Type)
				{
					case SourceType.File:
						return "/Icons/video-file.png";
					case SourceType.DiscVideoFolder:
						return "/Icons/dvd_folder.png";
					case SourceType.Disc:
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

			// SourceIconVisible
			this.WhenAnyValue(x => x.SelectedSource)
				.Select(source => source != null)
				.ToProperty(this, x => x.SourceIconVisible, out this.sourceIconVisible);

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
					return selectedTitle != null && this.SelectedTitle.Title.AngleCount > 1;
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
			this.canCloseVideoSourceObservable = this.WhenAnyValue(x => x.SelectedSource, x => x.VideoSourceState, (selectedSource, videoSourceState) =>
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

			this.canCloseVideoSourceObservable.ToProperty(this, x => x.CanCloseVideoSource, out this.canCloseVideoSource);

			// VideoDetails
			this.WhenAnyValue(x => x.SelectedTitle).Select(selectedTitle =>
			{
				if (selectedTitle == null)
				{
					return new List<InfoLineViewModel>();
				}

				List<InfoLineViewModel> lines = ResolutionUtilities.GetResolutionInfoLines(selectedTitle.Title);

				string videoCodec = DisplayConversions.DisplayVideoCodecName(selectedTitle.VideoCodec);
				lines.Add(new InfoLineViewModel(EncodingRes.CodecLabel, videoCodec));

				string framerate = string.Format(EncodingRes.FpsFormat, selectedTitle.FrameRate.ToDouble());
				lines.Add(new InfoLineViewModel(EncodingRes.ShortFramerateLabel, framerate));

				return lines;
			}).ToProperty(this, x => x.VideoDetails, out this.videoDetails);

			// HasAudio
			this.AudioTracks.CountChanged.Select(count => count > 0)
				.ToProperty(this, x => x.HasAudio, out this.hasAudio, initialValue: this.AudioTracks.Count > 0);

			// HasSourceSubtitles
			this.SourceSubtitles.CountChanged.Select(count => count > 0)
				.ToProperty(this, x => x.HasSourceSubtitles, out this.hasSourceSubtitles, initialValue: this.SourceSubtitles.Count > 0);

			// HasSrtSubtitles
			this.FileSubtitles.CountChanged.Select(count => count > 0)
				.ToProperty(this, x => x.HasFileSubtitles, out this.hasFileSubtitles, initialValue: this.FileSubtitles.Count > 0);

			// ShowSourceSubtitlesLabel
			Observable.CombineLatest(
				this.SourceSubtitles.CountChanged,
				this.FileSubtitles.CountChanged).Select(
				countList =>
				{
					return countList[0] > 0 && countList[1] > 0;
				}).ToProperty(this, x => x.ShowSourceSubtitlesLabel, out this.showSourceSubtitlesLabel);

			// ScanningSourceLabel
			this.WhenAnyValue(x => x.SourceName)
				.Select(sourceName =>
				{
					return string.Format(MainRes.ScanningSourceLabelFormat, sourceName);
				}).ToProperty(this, x => x.ScanningSourceLabel, out this.scanningSourceLabel);

			this.NotScanningObservable = this
				.WhenAnyValue(x => x.VideoSourceState)
				.Select(videoSourceState => videoSourceState != VideoSourceState.Scanning);

			this.OutputPathService = StaticResolver.Resolve<OutputPathService>();
			this.OutputSizeService = StaticResolver.Resolve<OutputSizeService>();
			this.ProcessingService = StaticResolver.Resolve<ProcessingService>();
			this.PresetsService = StaticResolver.Resolve<PresetsService>();
			this.PickersService = StaticResolver.Resolve<PickersService>();
			this.windowManager = StaticResolver.Resolve<IWindowManager>();
			this.previewUpdateService = StaticResolver.Resolve<PreviewUpdateService>();
			this.TaskBarProgressTracker = new TaskBarProgressTracker();

			// EncodingPresetButtonText
			this.PresetsService
				.WhenAnyValue(x => x.SelectedPreset.DisplayNameWithStar)
				.Select(presetNameWithStar => string.Format(CultureInfo.CurrentCulture, MainRes.EncodingSettingsButton, presetNameWithStar))
				.ToProperty(this, x => x.EncodingPresetButtonText, out this.encodingPresetButtonText);

			// WindowTitle
			this.ProcessingService.WhenAnyValue(x => x.Encoding, x => x.WorkTracker.OverallEncodeProgressFraction, (encoding, progressFraction) =>
			{
				double progressPercent = progressFraction * 100;
				var titleBuilder = new StringBuilder("VidCoder");

				if (CommonUtilities.Beta)
				{
					titleBuilder.Append(" Beta");
				}

				if (encoding)
				{
					titleBuilder.Append($" - {progressPercent:F1}%");
				}

				return titleBuilder.ToString();
			}).ToProperty(this, x => x.WindowTitle, out this.windowTitle);

			// ShowChapterMarkerUI
			this.WhenAnyValue(x => x.PresetsService.SelectedPreset.Preset.EncodingProfile.IncludeChapterMarkers, x => x.SelectedTitle, (includeChapterMarkers, selectedTitle) =>
			{
				return includeChapterMarkers && selectedTitle != null && selectedTitle.ChapterList != null && selectedTitle.ChapterList.Count > 1;
			}).ToProperty(this, x => x.ShowChapterMarkerUI, out this.showChapterMarkerUI);

			this.updater.CheckUpdates();

			this.JobCreationAvailable = false;

			this.sourceOptions = new ObservableCollection<SourceOptionViewModel>
			{
				new SourceOptionViewModel(new SourceOption { Type = SourceType.File }),
				new SourceOptionViewModel(new SourceOption { Type = SourceType.DiscVideoFolder })
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

			this.driveService = StaticResolver.Resolve<IDriveService>();

			// When an item is checked, resize the grid columns
			IObservable<IChangeSet<AudioTrackViewModel>> audioTracksObservable = this.AudioTracks.Connect();
			audioTracksObservable.Bind(this.AudioTracksBindable).Subscribe();
			audioTracksObservable
				.WhenValueChanged(track => track.Selected)
				.Subscribe(selected =>
			{
				if (selected)
				{
					this.View.ResizeAudioColumns();
				}
			});

			this.DriveCollection = this.driveService.GetDiscInformation();

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

		public IObservable<bool> NotScanningObservable { get; }

		public void UpdateDriveCollection()
		{
			// Get new set of drives
			IList<DriveInformation> newDriveCollection = this.driveService.GetDiscInformation();

			// If our currently selected source is a DVD drive, see if it needs to be updated
			if (this.SelectedSource != null && this.SelectedSource.Type == SourceType.Disc)
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
			this.View.RefreshDiscMenuItems();
		}

        /// <summary>
        /// Handles a list of provided file paths. Can be preset, subtitle, input file or folder.
        /// </summary>
        /// <param name="paths">The paths to process.</param>
        /// <param name="alwaysQueue">True if the given items should always be queued, and never opened as source.</param>
	    public void HandlePaths(IList<string> paths, bool alwaysQueue = false)
	    {
            if (paths.Count > 0)
            {
				DispatchUtilities.BeginInvoke(() =>
				{
					if (paths.Count == 1)
					{
						string item = paths[0];

						string extension = Path.GetExtension(item);
						if (extension != null)
						{
							extension = extension.ToLowerInvariant();
						}

						if (extension == ".xml" || extension == ".vjpreset")
						{
							// It's a preset
							try
							{
								Preset preset = StaticResolver.Resolve<IPresetImportExport>().ImportPreset(paths[0]);
								StaticResolver.Resolve<IMessageBoxService>().Show(string.Format(MainRes.PresetImportSuccessMessage, preset.Name), CommonRes.Success, System.Windows.MessageBoxButton.OK);
							}
							catch (Exception)
							{
								StaticResolver.Resolve<IMessageBoxService>().Show(MainRes.PresetImportErrorMessage, MainRes.ImportErrorTitle, System.Windows.MessageBoxButton.OK);
							}
						}
						else if (FileUtilities.SubtitleExtensions.Contains(extension))
						{
							if (this.HasVideoSource)
							{
								FileSubtitle subtitle = StaticResolver.Resolve<SubtitlesService>().LoadSubtitleFile(item);

								if (subtitle != null)
								{
									this.FileSubtitles.Add(new FileSubtitleViewModel(this, subtitle));
									StaticResolver.Resolve<StatusService>().Show(string.Format(MainRes.AddedSubtitleFromFile, item));
									if (!this.SubtitlesExpanded)
									{
										this.SubtitlesExpanded = true;
									}

									this.View.BringExternalSubtitlesIntoView();
								}
							}
						}
						else if (Utilities.IsDiscFolder(item))
						{
							// It's a disc folder or disc
							if (alwaysQueue)
							{
								this.ProcessingService.QueueMultiple(new []{ item });
							}
							else
							{
								this.SetSource(item);
							}
						}
						else
						{
							// It is a video file or folder full of video files
							this.HandlePathsAsVideoSource(paths, alwaysQueue);
						}
					}
					else
					{
						// With multiple items, treat it as a list video files/disc folders or folders full of those items
						this.HandlePathsAsVideoSource(paths, alwaysQueue);
					}
				});
            }
        }

		/// <summary>
		/// Handles a list of given file paths as video source files/folders.
		/// </summary>
		/// <param name="itemList">The list of paths to process.</param>
		/// <param name="alwaysQueue">True if the given items should always be queued, and never opened as source.</param>
		private void HandlePathsAsVideoSource(IList<string> itemList, bool alwaysQueue)
        {
            List<SourcePath> fileList = GetPathList(itemList);
            if (fileList.Count > 0)
            {
                if (fileList.Count == 1 && !alwaysQueue)
                {
                    this.SetSourceFromFile(fileList[0].Path);
                }
                else
                {
                    StaticResolver.Resolve<ProcessingService>().QueueMultiple(fileList);
                }
            }
        }

		/// <summary>
		/// Takes a list of file/folder paths and returns a list of valid video sources in them.
		/// </summary>
		/// <param name="itemList">The list of paths to process.</param>
		/// <returns>The list of video sources in the given paths.</returns>
		private static List<SourcePath> GetPathList(IList<string> itemList)
        {
            var videoExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string extensionsString = Config.VideoFileExtensions;
            string[] rawExtensions = extensionsString.Split(',', ';');
            foreach (string rawExtension in rawExtensions)
            {
                string extension = rawExtension.Trim();
                if (extension.Length > 0)
                {
                    if (!extension.StartsWith("."))
                    {
                        extension = "." + extension;
                    }

                    videoExtensions.Add(extension);
                }
            }

			var files = new List<string>();
			var folders = new List<string>();

			// First make a pass and assign each path to the files or folders list
			foreach (string item in itemList)
			{
				try
				{
					var fileAttributes = File.GetAttributes(item);
					if ((fileAttributes & FileAttributes.Directory) == FileAttributes.Directory)
					{
						// Path is a directory
						folders.Add(item);
					}
					else
					{
						// Path is a file
						files.Add(item);
					}
				}
				catch (Exception exception)
				{
					StaticResolver.Resolve<IAppLogger>().LogError($"Could not process {item} : " + Environment.NewLine + exception);
				}
			}

			// Sort the file and folder lists.
			files.Sort();
			folders.Sort();

			var pathList = new List<SourcePath>();

			// Now add all folders
			foreach (string folder in folders)
			{
				// Path is a directory
				if (Utilities.IsDiscFolder(folder))
				{
					// If it's a disc folder, add it
					pathList.Add(new SourcePath { Path = folder, SourceType = SourceType.DiscVideoFolder });
				}
				else
				{
					string parentFolder = Path.GetDirectoryName(folder);
					pathList.AddRange(Utilities.GetFilesOrVideoFolders(folder, videoExtensions)
						.Select(p => new SourcePath
						{
							Path = p,
							ParentFolder = parentFolder,
							SourceType = SourceType.None
						}));
				}
			}

			// Now add all files
			foreach (string file in files)
			{
				// Path is a file
				pathList.Add(new SourcePath { Path = file, SourceType = SourceType.File });
			}

            return pathList;
        }

        public bool SetSourceFromFile()
		{
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
			string folderPath = FileService.Instance.GetFolderName(
				Config.RememberPreviousFiles ? Config.LastVideoTSFolder : null,
				MainRes.PickDiscFolderHelpText);

			// Make sure we get focus back after displaying the dialog.
			this.windowManager.Focus(this);

			if (folderPath != null)
			{
				// Could be a disc folder or a folder of video files
				this.HandlePaths(new List<string> { folderPath });

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
			this.SelectedSource = new SourceOption { Type = SourceType.DiscVideoFolder };
		}

		public bool SetSourceFromDvd(DriveInformation driveInfo)
		{
			if (driveInfo.Empty)
			{
				return false;
			}

			this.SourceName = driveInfo.VolumeLabel;
			this.SourcePath = driveInfo.RootDirectory;

			this.SelectedSource = new SourceOption { Type = SourceType.Disc, DriveInfo = driveInfo };
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
				case SourceType.DiscVideoFolder:
					this.SetSourceFromFolder(sourcePath);
					break;
				case SourceType.Disc:
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
			if (this.ProcessingService.Encoding)
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

			// Call to view to save column widths
			this.view.SaveQueueColumns();
			this.view.SaveCompletedColumnWidths();

			// If we're quitting, see if the encode is still going.
			if (this.ProcessingService.Encoding)
			{
				// If so, stop it.
				this.ProcessingService.StopAndWaitAsync(EncodeCompleteReason.AppExit).Wait();
			}

			this.windowManager.CloseTrackedWindows();

			this.driveService.Dispose();

			if (this.scanInstance != null)
			{
				this.scanInstance.Dispose();
				this.scanInstance = null;
			}

			HandBrakeUtils.DisposeGlobal();

			FileCleanup.CleanOldLogs();
			FileCleanup.CleanPreviewFileCache();
			FileCleanup.CleanHandBrakeTempFiles();

			if (!Utilities.IsPortable)
			{
				AutomationHost.StopListening();
			}

			StaticResolver.Resolve<IToastNotificationService>().Clear();

			if (CustomConfig.UpdatePromptTiming == UpdatePromptTiming.OnExit)
			{
				this.updater.PromptToApplyUpdate(relaunchWhenComplete: false);
			}

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

		private string sourceName;
		public string SourceName
		{
			get { return this.sourceName; }
			set { this.RaiseAndSetIfChanged(ref this.sourceName, value); }
		}

		private ObservableAsPropertyHelper<string> scanningSourceLabel;
		public string ScanningSourceLabel => this.scanningSourceLabel.Value;

		public OutputPathService OutputPathService { get; }

		public OutputSizeService OutputSizeService { get; }

		public PresetsService PresetsService { get; }

		public PickersService PickersService { get; }

		public ProcessingService ProcessingService { get; }

		public TaskBarProgressTracker TaskBarProgressTracker { get; }

		private PreviewImageService previewImageService;
		public PreviewImageService PreviewImageService
		{
			get
			{
				if (this.previewImageService == null)
				{
					this.previewImageService = StaticResolver.Resolve<PreviewImageService>();
				}

				return this.previewImageService;
			}
			
		}

		private PreviewImageServiceClient previewImageServiceClient;
		public PreviewImageServiceClient PreviewImageServiceClient
		{
			get
			{
				if (this.previewImageServiceClient == null)
				{
					this.previewImageServiceClient = new PreviewImageServiceClient();
					this.PreviewImageService.RegisterClient(this.previewImageServiceClient);
				}

				return this.previewImageServiceClient;
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

		private ObservableAsPropertyHelper<string> sourceIcon;
		public string SourceIcon => this.sourceIcon.Value;

		private ObservableAsPropertyHelper<bool> sourceIconVisible;
		public bool SourceIconVisible => this.sourceIconVisible.Value;

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
					case SourceType.DiscVideoFolder:
						return this.SourcePath;
					case SourceType.Disc:
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

		public bool ShowUpdateMenuItem => Utilities.SupportsUpdates;

		private ObservableAsPropertyHelper<List<SourceTitleViewModel>> titles;
		public List<SourceTitleViewModel> Titles => this.titles.Value;

		private SourceTitleViewModel selectedTitle;
		public SourceTitleViewModel SelectedTitle
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

					Picker picker = this.PickersService.SelectedPicker.Picker;

					// Audio list build
					this.BuildAudioViewModelList();

					// Subtitle list build
					this.BuildSubtitleViewModelList();

					// Chapter build
					this.UseDefaultChapterNames = true;
					this.PopulateChapterSelectLists();

					// Select range
					TimeSpan localTimeRangeEnd;
					switch (picker.PickerTimeRangeMode)
					{
						case PickerTimeRangeMode.Chapters:
							var chapterRange = PickerUtilities.GetChapterRange(picker, value.Title);

							this.selectedStartChapter = this.StartChapters[chapterRange.chapterStart - 1];
							this.selectedEndChapter = this.EndChapters[chapterRange.chapterEnd - 1];

							this.RaisePropertyChanged(nameof(this.SelectedStartChapter));
							this.RaisePropertyChanged(nameof(this.SelectedEndChapter));

							this.SetRangeTimeStart(TimeSpan.Zero);
							localTimeRangeEnd = this.selectedTitle.Duration;

							break;
						case PickerTimeRangeMode.Time:

							var range = this.ProcessingService.GetRangeFromPicker(value.Title, picker);
							this.SetRangeTimeStart(range.start);
							localTimeRangeEnd = range.end;

							break;
						case PickerTimeRangeMode.All:
						default:
							// Soft update so as not to trigger range checking logic.
							this.selectedStartChapter = this.StartChapters[0];
							this.selectedEndChapter = this.EndChapters[this.EndChapters.Count - 1];
							this.RaisePropertyChanged(nameof(this.SelectedStartChapter));
							this.RaisePropertyChanged(nameof(this.SelectedEndChapter));

							this.SetRangeTimeStart(TimeSpan.Zero);
							localTimeRangeEnd = this.selectedTitle.Duration;

							break;
					}

					this.timeRangeEndBar = localTimeRangeEnd;
					this.timeRangeEnd = TimeSpan.FromSeconds(Math.Floor(localTimeRangeEnd.TotalSeconds));
					this.RaisePropertyChanged(nameof(this.TimeRangeStart));
					this.RaisePropertyChanged(nameof(this.TimeRangeStartBar));
					this.RaisePropertyChanged(nameof(this.TimeRangeEnd));
					this.RaisePropertyChanged(nameof(this.TimeRangeEndBar));

					this.framesRangeStart = 0;
					this.framesRangeEnd = this.selectedTitle.EstimatedFrames;
					this.RaisePropertyChanged(nameof(this.FramesRangeStart));
					this.RaisePropertyChanged(nameof(this.FramesRangeEnd));

					this.PopulateAnglesList();

					this.angle = 1;

					this.RaisePropertyChanged(nameof(this.Angles));
					this.RaisePropertyChanged(nameof(this.Angle));


					// Change range type based on whether or not we have any chapters
					if (picker.PickerTimeRangeMode == PickerTimeRangeMode.Time)
					{
						this.RangeType = VideoRangeType.Seconds;
					}
					else if (picker.PickerTimeRangeMode == PickerTimeRangeMode.Chapters ||this.selectedTitle.ChapterList.Count > 1)
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

				this.RaisePropertyChanged(nameof(this.SelectedTitle));

				this.OutputSizeService.Refresh();
				this.previewUpdateService.RefreshPreview();

				this.RefreshRangePreview();
				this.RefreshSubtitleSummary();
				this.RefreshAudioSummary();

				DispatchUtilities.BeginInvoke(
					() =>
					{
						// Expand/collapse sections to keep everything visible
						this.SetSectionExpansion();

						if (this.AudioExpanded)
						{
							this.View.ResizeAudioColumns();
						}
					},
					DispatcherPriority.Background);
			}
		}

		private ObservableAsPropertyHelper<bool> titleWarningVisible;
		public bool TitleWarningVisible => this.titleWarningVisible.Value;

		private ObservableAsPropertyHelper<string> titleWarning;
		public string TitleWarning => this.titleWarning.Value;

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
			set
			{
				this.previewUpdateService.RefreshPreview();
				this.RaiseAndSetIfChanged(ref this.angle, value);
			}
		}

		[Flags]
		private enum SourceSection
		{
			None = 0,
			Video = 1,
			Audio = 2,
			Subtitles = 4
		}

		private static readonly List<SourceSection> SourceSectionPriority = new List<SourceSection>
		{
			SourceSection.Video,
			SourceSection.Audio,
			SourceSection.Subtitles
		};

		private void SetSectionExpansion()
		{
			if (this.SelectedTitle == null)
			{
				return;
			}

			SourceSection expansion = this.GetSectionExpansion();
			this.ApplySectionExpansion(expansion);
		}

		private void ApplySectionExpansion(SourceSection expansion)
		{
			this.videoExpanded = expansion.HasFlag(SourceSection.Video);
			this.RaisePropertyChanged(nameof(this.VideoExpanded));

			this.audioExpanded = expansion.HasFlag(SourceSection.Audio) && this.SelectedTitle.AudioList != null && this.SelectedTitle.AudioList.Count > 0;
			this.RaisePropertyChanged(nameof(this.AudioExpanded));

			this.subtitlesExpanded = expansion.HasFlag(SourceSection.Subtitles) && this.SelectedTitle.SubtitleList != null && this.SelectedTitle.SubtitleList.Count > 0;
			this.RaisePropertyChanged(nameof(this.SubtitlesExpanded));
		}

		private SourceSection GetSectionExpansion()
		{
			double availableHeight = this.View.SourceAreaHeight;
			SourceSection userCollapsedSections = GetUserCollapsedSections();

			// Start with nothing expanded
			SourceSection sections = SourceSection.None;

			// First pass try to open sections user has not collapsed.
			foreach (SourceSection newSection in SourceSectionPriority)
			{
				if (!userCollapsedSections.HasFlag(newSection))
				{
					SourceSection sectionsWithNewExpansion = sections | newSection;
					if (this.GetTotalHeight(sectionsWithNewExpansion) > availableHeight)
					{
						// Failed to open section. Bail with current list.
						return sections;
					}

					sections = sections | newSection;
				}
			}

			// Second pass see if we can open more sections.
			foreach (SourceSection newSection in SourceSectionPriority)
			{
				if (userCollapsedSections.HasFlag(newSection))
				{
					SourceSection sectionsWithNewExpansion = sections | newSection;
					if (this.GetTotalHeight(sectionsWithNewExpansion) > availableHeight)
					{
						// Failed to open section. Bail with current list.
						return sections;
					}

					sections = sections | newSection;
				}
			}

			return sections;
		}

		private static SourceSection GetUserCollapsedSections()
		{
			SourceSection sections = SourceSection.None;
			if (!Config.VideoExpanded)
			{
				sections = sections | SourceSection.Video;
			}

			if (!Config.AudioExpanded)
			{
				sections = sections | SourceSection.Audio;
			}

			if (!Config.SubtitlesExpanded)
			{
				sections = sections | SourceSection.Subtitles;
			}

			return sections;
		}

		private double GetTotalHeight(SourceSection expandedSections)
		{
			double height = 98;

			if (expandedSections.HasFlag(SourceSection.Video))
			{
				height += 110;
			}

			int audioCount = this.SelectedTitle.AudioList.Count;
			if (audioCount > 0 && expandedSections.HasFlag(SourceSection.Audio))
			{
				height += 24;
				height += audioCount * 27;
			}

			int subtitleCount = this.SelectedTitle.SubtitleList.Count;
			if (subtitleCount > 0 && expandedSections.HasFlag(SourceSection.Subtitles))
			{
				height += 24;
				height += subtitleCount * 27;
			}

			if (this.PresetsService.SelectedPreset.Preset.EncodingProfile.IncludeChapterMarkers 
				&& this.SelectedTitle.ChapterList != null 
				&& this.SelectedTitle.ChapterList.Count > 0)
			{
				height += 31;
			}

			return height;
		}

		private bool videoExpanded;
		public bool VideoExpanded
		{
			get { return this.videoExpanded; }
			set
			{
				this.RaiseAndSetIfChanged(ref this.videoExpanded, value);
				Config.VideoExpanded = value;
			}
		}

		private ObservableAsPropertyHelper<List<InfoLineViewModel>> videoDetails;
		public List<InfoLineViewModel> VideoDetails => this.videoDetails.Value;

		private bool audioExpanded;
		public bool AudioExpanded
		{
			get { return this.audioExpanded; }
			set
			{
				this.RaiseAndSetIfChanged(ref this.audioExpanded, value);
				Config.AudioExpanded = value;
			}
		}

		private ObservableAsPropertyHelper<bool> hasAudio;
		public bool HasAudio => this.hasAudio.Value;

		private void BuildAudioViewModelList()
		{
			Picker picker = this.PickersService.SelectedPicker.Picker;

			List<AudioTrackViewModel> oldTracks = this.AudioTracks.Items.Where(t => t.Selected).ToList();

			this.AudioTracks.Edit(audioTracksInnerList =>
			{
				audioTracksInnerList.Clear();

				switch (picker.AudioSelectionMode)
				{
					case AudioSelectionMode.Disabled:
						// If no auto-selection is done, keep audio from previous selection.
						if (oldTracks.Count > 0)
						{
							foreach (AudioTrackViewModel audioTrackVM in oldTracks)
							{
								if (audioTrackVM.TrackIndex < this.selectedTitle.AudioList.Count &&
								    audioTrackVM.AudioTrack.Language == this.selectedTitle.AudioList[audioTrackVM.TrackIndex].Language)
								{
									audioTracksInnerList.Add(new AudioTrackViewModel(this, this.selectedTitle.AudioList[audioTrackVM.TrackIndex], new ChosenAudioTrack { TrackNumber = audioTrackVM.TrackNumber }) { Selected = true });
								}
							}
						}

						break;
					case AudioSelectionMode.First:
					case AudioSelectionMode.ByIndex:
					case AudioSelectionMode.Language:
					case AudioSelectionMode.All:
						audioTracksInnerList.AddRange(ProcessingService
							.ChooseAudioTracks(this.selectedTitle.AudioList, picker)
							.Select(track => new AudioTrackViewModel(this, track, new ChosenAudioTrack { TrackNumber = track.TrackNumber }) { Selected = true }));

						break;
					default:
						throw new ArgumentOutOfRangeException();
				}

				// If nothing got selected and we have not explicitly left it out, add the first one.
				if (this.selectedTitle.AudioList.Count > 0 && this.AudioTracks.Count == 0 && picker.AudioSelectionMode != AudioSelectionMode.ByIndex)
				{
					audioTracksInnerList.Add(new AudioTrackViewModel(this, this.selectedTitle.AudioList[0], new ChosenAudioTrack { TrackNumber = 1 }) { Selected = true });
				}

				// Fill in rest of unselected audio tracks
				this.FillInUnselectedAudioTracks(audioTracksInnerList);
			});
		}

		private void FillInUnselectedAudioTracks(IList<AudioTrackViewModel> listToAddTo)
		{
			for (int i = 0; i < this.SelectedTitle.AudioList.Count; i++)
			{
				SourceAudioTrack track = this.SelectedTitle.AudioList[i];
				if (listToAddTo.All(t => t.AudioTrack != track))
				{
					listToAddTo.Add(new AudioTrackViewModel(this, track, new ChosenAudioTrack { TrackNumber = i + 1 }));
				}
			}
		}

		/// <summary>
		/// Determines if the given track has a duplicate.
		/// </summary>
		/// <param name="trackNumber">The 1-based track number.</param>
		/// <returns>True if the track has a duplicate.</returns>
		public bool HasMultipleAudioTracks(int trackNumber)
		{
			return this.AudioTracks.Items.Count(s => s.TrackNumber == trackNumber) > 1;
		}

		public void RemoveAudioTrack(AudioTrackViewModel audioTrackViewModel)
		{
			this.AudioTracks.Remove(audioTrackViewModel);
			this.UpdateAudioTrackButtonVisibility();
			this.RefreshAudioSummary();
		}

		public void DuplicateAudioTrack(AudioTrackViewModel audioTrackViewModel)
		{
			audioTrackViewModel.Selected = true;

			var newTrack = new AudioTrackViewModel(
				this,
				audioTrackViewModel.AudioTrack,
				new ChosenAudioTrack { TrackNumber = audioTrackViewModel.TrackNumber });
			newTrack.Selected = true;

			this.AudioTracks.Insert(
				this.AudioTracks.Items.IndexOf(audioTrackViewModel) + 1,
				newTrack);
			this.UpdateAudioTrackButtonVisibility();
			this.RefreshAudioSummary();
		}

		private void UpdateAudioTrackButtonVisibility()
		{
			foreach (AudioTrackViewModel audioTrackViewModel in this.AudioTracks.Items)
			{
				audioTrackViewModel.UpdateButtonVisiblity();
			}
		}

		private string audioSummary;
		public string AudioSummary
		{
			get { return this.audioSummary; }
			set { this.RaiseAndSetIfChanged(ref this.audioSummary, value); }
		}

		public void RefreshAudioSummary()
		{
			if (this.SelectedTitle == null)
			{
				this.AudioSummary = string.Empty;
				return;
			}

			if (this.AudioTracks.Count > 0)
			{
				List<AudioTrackViewModel> selectedTracks = this.AudioTracks.Items.Where(s => s.Selected).ToList();

				int selectedCount = selectedTracks.Count;
				string sourceDescription = string.Format(
					CultureInfo.CurrentCulture, 
					CommonRes.SelectedOverTotalAudioTracksFormat, 
					selectedCount,
					this.SelectedTitle.AudioList.Count);

				if (selectedCount > 0 && selectedCount <= 3)
				{
					List<string> trackSummaries = new List<string>();
					foreach (AudioTrackViewModel track in selectedTracks)
					{
						if (this.SelectedTitle.AudioList != null && track.TrackNumber <= this.SelectedTitle.AudioList.Count)
						{
							trackSummaries.Add(this.SelectedTitle.AudioList[track.TrackNumber - 1].Language);
						}
					}

					sourceDescription += $": {string.Join(", ", trackSummaries)}";
				}

				this.AudioSummary = sourceDescription;
			}
			else
			{
				this.AudioSummary = MainRes.NoAudioSummary;
			}
		}

		private bool subtitlesExpanded;
		public bool SubtitlesExpanded
		{
			get { return this.subtitlesExpanded; }
			set
			{
				this.RaiseAndSetIfChanged(ref this.subtitlesExpanded, value);
				Config.SubtitlesExpanded = value;
			}
		}

		public VCSubtitles CurrentSubtitles
		{
			get
			{
				var result = new VCSubtitles
				{
					SourceSubtitles = this.SourceSubtitles.Items.Where(s => s.Selected).Select(s => s.Subtitle.Clone()).ToList(),
					FileSubtitles = this.FileSubtitles.Items.Select(s => s.Subtitle.Clone()).ToList()
				};

				return result;
			}
		}

		public SourceList<SourceSubtitleViewModel> SourceSubtitles { get; } = new SourceList<SourceSubtitleViewModel>();
		public ObservableCollectionExtended<SourceSubtitleViewModel> SourceSubtitlesBindable { get; } = new ObservableCollectionExtended<SourceSubtitleViewModel>();

		public SourceList<FileSubtitleViewModel> FileSubtitles { get; } = new SourceList<FileSubtitleViewModel>();
		public ObservableCollectionExtended<FileSubtitleViewModel> FileSubtitlesBindable { get; } = new ObservableCollectionExtended<FileSubtitleViewModel>();

		private void BuildSubtitleViewModelList()
		{
			VCSubtitles oldSubtitles = this.CurrentSubtitles;

			Picker picker = this.PickersService.SelectedPicker.Picker;

			this.SourceSubtitles.Edit(sourceSubtitlesInnerList =>
			{
				sourceSubtitlesInnerList.Clear();

				// Subtitle selection
				switch (picker.SubtitleSelectionMode)
				{
					case SubtitleSelectionMode.Disabled:
						// If no auto-selection is done, try and keep selections from previous title
						var keptSourceSubtitles = new List<ChosenSourceSubtitle>();

						if (this.oldTitle != null && oldSubtitles != null)
						{
							if (this.selectedTitle.SubtitleList.Count > 0)
							{
								// Keep source subtitles when changing title, but not specific subtitle files.
								foreach (ChosenSourceSubtitle sourceSubtitle in oldSubtitles.SourceSubtitles)
								{
									if (sourceSubtitle.TrackNumber == 0)
									{
										sourceSubtitle.Name = null;
										keptSourceSubtitles.Add(sourceSubtitle);
									}
									else if (sourceSubtitle.TrackNumber - 1 < this.selectedTitle.SubtitleList.Count &&
									         this.oldTitle.SubtitleList[sourceSubtitle.TrackNumber - 1].LanguageCode == this.selectedTitle.SubtitleList[sourceSubtitle.TrackNumber - 1].LanguageCode)
									{
										sourceSubtitle.Name = null;
										keptSourceSubtitles.Add(sourceSubtitle);
									}
								}
							}
						}

						this.PopulateSourceSubtitles(keptSourceSubtitles, sourceSubtitlesInnerList);
						break;

					case SubtitleSelectionMode.None:
					case SubtitleSelectionMode.First:
					case SubtitleSelectionMode.ByIndex:
					case SubtitleSelectionMode.ForeignAudioSearch:
					case SubtitleSelectionMode.Language:
					case SubtitleSelectionMode.All:
						AudioTrackViewModel firstSelectedAudio = this.AudioTracks.Items.FirstOrDefault(t => t.Selected);

						var selectedSubtitles = ProcessingService.ChooseSubtitles(
							this.selectedTitle.Title,
							picker,
							firstSelectedAudio != null ? firstSelectedAudio.TrackNumber : -1,
							this.PresetsService.SelectedPreset.Preset.EncodingProfile.ContainerName);

						this.PopulateSourceSubtitles(selectedSubtitles, sourceSubtitlesInnerList);
						break;

					default:
						throw new ArgumentOutOfRangeException();
				}
			});

			this.UpdateSourceSubtitleBoxes();
		}

		private void PopulateSourceSubtitles(IList<ChosenSourceSubtitle> selectedSubtitles, IExtendedList<SourceSubtitleViewModel> listToPopulate)
		{
			foreach (ChosenSourceSubtitle sourceSubtitle in selectedSubtitles)
			{
				listToPopulate.Add(new SourceSubtitleViewModel(this, sourceSubtitle) { Selected = true });
			}

			this.FillInRemainingSourceSubtitles(selectedSubtitles, listToPopulate);
		}

		/// <summary>
		/// Fills in the remaining unselected source subtitles
		/// </summary>
		/// <param name="selectedSubtitles">The already selected subtitles.</param>
		/// <param name="listToPopulate">The list to fill in.</param>
		private void FillInRemainingSourceSubtitles(IList<ChosenSourceSubtitle> selectedSubtitles, IExtendedList<SourceSubtitleViewModel> listToPopulate)
		{
			for (int i = 1; i <= this.SelectedTitle.SubtitleList.Count; i++)
			{
				if (selectedSubtitles.All(s => s.TrackNumber != i))
				{
					var newSubtitle = new ChosenSourceSubtitle
					{
						TrackNumber = i,
						Default = false,
						ForcedOnly = false,
						BurnedIn = false
					};

					listToPopulate.Add(new SourceSubtitleViewModel(this, newSubtitle));
				}
			}

			if (!listToPopulate.Any(subtitleViewModel => subtitleViewModel.TrackNumber == 0) && this.SelectedTitle.SubtitleList.Count > 0)
			{
				listToPopulate.Insert(0, new SourceSubtitleViewModel(this, new ChosenSourceSubtitle
				{
					TrackNumber = 0,
					BurnedIn = false,
					Default = false,
					ForcedOnly = true
				}));
			}
		}

		// Update state of checked boxes after a change
		public void UpdateSourceSubtitleBoxes(SourceSubtitleViewModel updatedSubtitle = null)
		{
			this.DeselectDefaultSubtitlesIfAnyBurned();

			if (updatedSubtitle != null && updatedSubtitle.Selected)
			{
				if (!updatedSubtitle.CanPass)
				{
					// If we just selected a burn-in only subtitle, deselect all other subtitles.
					foreach (SourceSubtitleViewModel sourceSub in this.SourceSubtitles.Items)
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
					foreach (SourceSubtitleViewModel sourceSub in this.SourceSubtitles.Items)
					{
						if (!sourceSub.CanPass)
						{
							sourceSub.Deselect();
						}
					}
				}
			}
		}

		public void UpdateSubtitleWarningVisibility()
		{
			bool textSubtitleVisible = false;
			VCProfile profile = this.PresetsService.SelectedPreset.Preset.EncodingProfile;
			HBContainer profileContainer = HandBrakeEncoderHelpers.GetContainer(profile.ContainerName);
			if (profileContainer.DefaultExtension == "mp4" && profile.PreferredExtension == VCOutputExtension.Mp4)
			{
				foreach (SourceSubtitleViewModel sourceVM in this.SourceSubtitles.Items)
				{
					if (sourceVM.Selected && sourceVM.TrackSummary.Contains("(Text)"))
					{
						textSubtitleVisible = true;
						break;
					}
				}
			}

			this.TextSubtitleWarningVisible = textSubtitleVisible;

			bool anyBurned = false;
			int totalTracks = 0;
			foreach (SourceSubtitleViewModel sourceVM in this.SourceSubtitles.Items)
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

			foreach (FileSubtitleViewModel fileSubtitleViewModel in this.FileSubtitles.Items)
			{
				totalTracks++;
				if (fileSubtitleViewModel.BurnedIn)
				{
					anyBurned = true;
				}
			}

			this.BurnedOverlapWarningVisible = anyBurned && totalTracks > 1;
		}

		public void ReportDefaultSubtitle(object viewModel)
		{
			foreach (SourceSubtitleViewModel sourceVM in this.SourceSubtitles.Items)
			{
				if (sourceVM != viewModel)
				{
					sourceVM.Default = false;
				}
			}

			foreach (FileSubtitleViewModel fileSubtitleViewModel in this.FileSubtitles.Items)
			{
				if (fileSubtitleViewModel != viewModel)
				{
					fileSubtitleViewModel.Default = false;
				}
			}
		}

		public void ReportBurnedSubtitle(SourceSubtitleViewModel subtitleViewModel)
		{
			foreach (SourceSubtitleViewModel sourceVM in this.SourceSubtitles.Items)
			{
				if (sourceVM != subtitleViewModel)
				{
					sourceVM.BurnedIn = false;
				}
			}

			foreach (FileSubtitleViewModel fileSubtitleViewModel in this.FileSubtitles.Items)
			{
				fileSubtitleViewModel.BurnedIn = false;
			}
		}

		public void ReportBurnedSubtitle(FileSubtitleViewModel subtitleViewModel)
		{
			foreach (SourceSubtitleViewModel sourceVM in this.SourceSubtitles.Items)
			{
				sourceVM.BurnedIn = false;
			}

			foreach (FileSubtitleViewModel fileSubtitleViewModel in this.FileSubtitles.Items)
			{
				if (fileSubtitleViewModel != subtitleViewModel)
				{
					fileSubtitleViewModel.BurnedIn = false;
				}
			}
		}

		public bool HasMultipleSourceSubtitleTracks(int trackNumber)
		{
			return this.SourceSubtitles.Items.Count(s => s.TrackNumber == trackNumber) > 1;
		}

		public void RemoveSourceSubtitle(SourceSubtitleViewModel subtitleViewModel)
		{
			this.SourceSubtitles.Remove(subtitleViewModel);
			this.UpdateSourceSubtitleBoxes();
			this.UpdateSourceSubtitleButtonVisibility();
			this.UpdateSubtitleWarningVisibility();
			this.RefreshSubtitleSummary();
		}

		public void RemoveFileSubtitle(FileSubtitleViewModel subtitleViewModel)
		{
			this.FileSubtitles.Remove(subtitleViewModel);
			this.UpdateSubtitleWarningVisibility();
			this.RefreshSubtitleSummary();
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
				new ChosenSourceSubtitle
				{
					BurnedIn = false,
					Default = false,
					ForcedOnly = !sourceSubtitleViewModel.ForcedOnly,
					TrackNumber = sourceSubtitleViewModel.TrackNumber
				});
			newSubtitle.Selected = true;

			this.SourceSubtitles.Insert(
				this.SourceSubtitles.Items.IndexOf(sourceSubtitleViewModel) + 1,
				newSubtitle);
			this.UpdateSourceSubtitleBoxes();
			this.UpdateSourceSubtitleButtonVisibility();
			this.UpdateSubtitleWarningVisibility();
			this.RefreshSubtitleSummary();
		}

		private void UpdateSourceSubtitleButtonVisibility()
		{
			foreach (SourceSubtitleViewModel sourceVM in this.SourceSubtitles.Items)
			{
				sourceVM.UpdateButtonVisiblity();
			}
		}

		private void DeselectDefaultSubtitlesIfAnyBurned()
		{
			bool anyBurned = this.SourceSubtitles.Items.Any(sourceSub => sourceSub.BurnedIn) || this.FileSubtitles.Items.Any(sourceSub => sourceSub.BurnedIn);
			this.DefaultSubtitlesEnabled = !anyBurned;

			if (!this.DefaultSubtitlesEnabled)
			{
				foreach (SourceSubtitleViewModel sourceVM in this.SourceSubtitles.Items)
				{
					sourceVM.Default = false;
				}

				foreach (FileSubtitleViewModel srtVM in this.FileSubtitles.Items)
				{
					srtVM.Default = false;
				}
			}
		}

		private ReactiveCommand<Unit, Unit> addFileSubtitle;
		public ICommand AddFileSubtitle
		{
			get
			{
				return this.addFileSubtitle ?? (this.addFileSubtitle = ReactiveCommand.Create(() =>
				{
					List<string> starExtensionList = FileUtilities.SubtitleExtensions.Select(e => "*" + e).ToList();
					string descriptionExtensionList = string.Join(", ", starExtensionList);
					string extensionList = string.Join(";", starExtensionList);

					string srtFile = FileService.Instance.GetFileNameLoad(
						Config.RememberPreviousFiles ? Config.LastSubtitleFolder : null,
						SubtitleRes.SubtitleFilePickerText,
						$"{SubtitleRes.FilePickerDescription} ({descriptionExtensionList})|{extensionList}");

					if (srtFile != null)
					{
						if (Config.RememberPreviousFiles)
						{
							Config.LastSubtitleFolder = Path.GetDirectoryName(srtFile);
						}

						FileSubtitle newSubtitle = StaticResolver.Resolve<SubtitlesService>().LoadSubtitleFile(srtFile);

						if (newSubtitle != null)
						{
							this.FileSubtitles.Add(new FileSubtitleViewModel(this, newSubtitle));
						}
					}

					this.UpdateSubtitleWarningVisibility();
					this.RefreshSubtitleSummary();
				}));
			}
		}

		private bool defaultSubtitlesEnabled = true;
		public bool DefaultSubtitlesEnabled
		{
			get { return this.defaultSubtitlesEnabled; }
			set { this.RaiseAndSetIfChanged(ref this.defaultSubtitlesEnabled, value); }
		}

		private bool textSubtitleWarningVisible;
		public bool TextSubtitleWarningVisible
		{
			get { return this.textSubtitleWarningVisible; }
			set { this.RaiseAndSetIfChanged(ref this.textSubtitleWarningVisible, value); }
		}

		private bool burnedOverlapWarningVisible;
		public bool BurnedOverlapWarningVisible
		{
			get { return this.burnedOverlapWarningVisible; }
			set { this.RaiseAndSetIfChanged(ref this.burnedOverlapWarningVisible, value); }
		}

		private ObservableAsPropertyHelper<bool> hasSourceSubtitles;
		public bool HasSourceSubtitles => this.hasSourceSubtitles.Value;

		private ObservableAsPropertyHelper<bool> hasFileSubtitles;
		public bool HasFileSubtitles => this.hasFileSubtitles.Value;

		public void RefreshSubtitleSummary()
		{
			if (this.SelectedTitle == null)
			{
				this.SubtitlesSummary = string.Empty;
				return;
			}

			var summaryParts = new List<string>();

			if (this.SourceSubtitles.Count > 0)
			{
				List<SourceSubtitleViewModel> selectedSubtitles = this.SourceSubtitles.Items.Where(s => s.Selected).ToList();

				int selectedCount = selectedSubtitles.Count;
				int totalCount = this.SelectedTitle.SubtitleList.Count;
				string sourceDescription = string.Format(
					CultureInfo.CurrentCulture, 
					CommonRes.SelectedOverTotalSubtitleTracksFormat, 
					selectedCount,
					totalCount);

				if (selectedCount > 0 && selectedCount <= 3)
				{
					List<string> trackSummaries = new List<string>();
					foreach (SourceSubtitleViewModel subtitle in selectedSubtitles)
					{
						if (subtitle.TrackNumber == 0)
						{
							trackSummaries.Add(MainRes.ForeignAudioSearch);
						}
						else if (this.SelectedTitle.SubtitleList != null && subtitle.TrackNumber <= this.SelectedTitle.SubtitleList.Count)
						{
							trackSummaries.Add(this.SelectedTitle.SubtitleList[subtitle.TrackNumber - 1].Language);
						}
					}

					sourceDescription += $": {string.Join(", ", trackSummaries)}";
				}

				summaryParts.Add(sourceDescription);
			}

			if (this.FileSubtitles.Count > 0)
			{
				string externalSubtitlesDescription = string.Format(SubtitleRes.ExternalSubtitlesSummaryFormat, this.FileSubtitles.Count);

				if (this.FileSubtitles.Count <= 3)
				{
					List<string> trackSummaries = new List<string>();
					foreach (FileSubtitleViewModel subtitle in this.FileSubtitles.Items)
					{
						trackSummaries.Add(HandBrakeLanguagesHelper.Get(subtitle.LanguageCode).Display);
					}

					externalSubtitlesDescription += $": {string.Join(", ", trackSummaries)}";
				}

				summaryParts.Add(externalSubtitlesDescription);
			}

			if (summaryParts.Count == 0)
			{
				this.SubtitlesSummary = MainRes.NoSubtitlesSummary;
			}
			else
			{
				this.SubtitlesSummary = string.Join(" - ", summaryParts);
			}
		}

		private string subtitlesSummary;
		public string SubtitlesSummary
		{
			get { return this.subtitlesSummary; }
			set { this.RaiseAndSetIfChanged(ref this.subtitlesSummary, value); }
		}

		private ObservableAsPropertyHelper<bool> showSourceSubtitlesLabel;
		public bool ShowSourceSubtitlesLabel => this.showSourceSubtitlesLabel.Value;

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

		private ObservableAsPropertyHelper<string> encodingPresetButtonText;
		public string EncodingPresetButtonText => this.encodingPresetButtonText.Value;

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

					// If switching to chapters, make sure we have some start and end selected.
					if (value == VideoRangeType.Chapters)
					{
						if (this.selectedStartChapter == null)
						{
							this.selectedStartChapter = this.StartChapters[0];
							this.RaisePropertyChanged(nameof(this.SelectedStartChapter));
						}

						if (this.selectedEndChapter == null)
						{
							this.selectedEndChapter = this.EndChapters[this.EndChapters.Count - 1];
							this.RaisePropertyChanged(nameof(this.SelectedEndChapter));
						}
					}

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

		private List<ChapterViewModel> startChapters;
		public List<ChapterViewModel> StartChapters
		{
			get { return this.startChapters; }
			set { this.RaiseAndSetIfChanged(ref this.startChapters, value); }
		}

		private List<ChapterViewModel> endChapters;
		public List<ChapterViewModel> EndChapters
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

				TimeSpan maxTime = this.SelectedTitle.Duration;
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

				TimeSpan maxTime = this.SelectedTitle.Duration;

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

						return this.SelectedTitle.Title.GetChapterRangeDuration(1, startChapter - 1);
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
						return this.SelectedTitle.Duration;
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

						return this.SelectedTitle.Title.GetChapterRangeDuration(1, endChapter);
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
				return this.RangePreviewStart.FormatWithHours() + " - " + this.RangePreviewEnd.FormatWithHours();
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

				return string.Format(MainRes.LengthPreviewLabel, duration.FormatWithHours());
			}
		}

		public SourceList<AudioTrackViewModel> AudioTracks { get; } = new SourceList<AudioTrackViewModel>();
		public ObservableCollectionExtended<AudioTrackViewModel> AudioTracksBindable { get; } = new ObservableCollectionExtended<AudioTrackViewModel>();

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
				return "VidCoder " + Utilities.CurrentVersion.ToShortString();
			}
		}

		private ReactiveCommand<Unit, Unit> openEncodingWindow;
		public ICommand OpenEncodingWindow
		{
			get
			{
				return this.openEncodingWindow ?? (this.openEncodingWindow = ReactiveCommand.Create(() =>
				{
					this.windowManager.OpenOrFocusWindow<EncodingWindowViewModel>();
				}));
			}
		}

		private ReactiveCommand<Unit, Unit> openOptions;
		public ICommand OpenOptions
		{
			get
			{
				return this.openOptions ?? (this.openOptions = ReactiveCommand.Create(() =>
				{
					// Need to dispatch this one so React doesn't disable the command.
					// When this happens after closing the dialog the main window doesn't have focus and keyboard commands don't work.
					DispatchUtilities.BeginInvoke(() =>
					{
						this.windowManager.OpenDialog<OptionsDialogViewModel>();
					});
				}));
			}
		}

		private ReactiveCommand<Unit, Unit> openUpdates;
		public ICommand OpenUpdates
		{
			get
			{
				return this.openUpdates ?? (this.openUpdates = ReactiveCommand.Create(() =>
				{
					Config.OptionsDialogLastTab = OptionsDialogViewModel.UpdatesTabIndex;
					this.windowManager.OpenDialog<OptionsDialogViewModel>();
				}));
			}
		}

		private ReactiveCommand<Unit, Unit> openChaptersDialog;
		public ICommand OpenChaptersDialog
		{
			get
			{
				return this.openChaptersDialog ?? (this.openChaptersDialog = ReactiveCommand.Create(() =>
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
				}));
			}
		}

		private ReactiveCommand<Unit, Unit> openAboutDialog;
		public ICommand OpenAboutDialog
		{
			get
			{
				return this.openAboutDialog ?? (this.openAboutDialog = ReactiveCommand.Create(() =>
				{
					this.windowManager.OpenDialog(new AboutDialogViewModel());
				}));
			}
		}

		private ReactiveCommand<Unit, Unit> cancelScan;
		public ICommand CancelScan
		{
			get { return this.CancelScanImpl(); }
		}

		private ReactiveCommand<Unit, Unit> CancelScanImpl()
		{
			return this.cancelScan ?? (this.cancelScan = ReactiveCommand.Create(() =>
			{
				this.ScanInstance.StopScan();

				DispatchUtilities.Invoke(() =>
				{
					try
					{
						this.SelectedSource = null;

						this.ScanCanceled?.Invoke(this, EventArgs.Empty);

						this.scanLogger.Log("Scan canceled");

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
					finally
					{
						if (this.scanLogger is LocalScanAppLogger)
						{
							this.scanLogger.Dispose();
						}
					}
				});
			}));
		}

		private ReactiveCommand<Unit, Unit> openFile;
		public ICommand OpenFile
		{
			get
			{
				return this.openFile ?? (this.openFile = ReactiveCommand.Create(
					() =>
					{
						this.SetSourceFromFile();
					},
					this.NotScanningObservable));
			}
		}

		private ReactiveCommand<Unit, Unit> openFolder;
		public ICommand OpenFolder
		{
			get
			{
				return this.openFolder ?? (this.openFolder = ReactiveCommand.Create(
					() =>
					{
						this.SetSourceFromFolder();
					},
					this.NotScanningObservable));
			}
		}

		private ReactiveCommand<Unit, Unit> clearRecentFiles;
		public ICommand ClearRecentFiles
		{
			get
			{
				return this.clearRecentFiles ?? (this.clearRecentFiles = ReactiveCommand.Create(() =>
				{
					SourceHistory.Clear();
					this.RefreshRecentSourceOptions();
				}));
			}
		}

		private ReactiveCommand<Unit, Unit> closeVideoSource;
		public ICommand CloseVideoSource
		{
			get
			{
				return this.closeVideoSource ?? (this.closeVideoSource = ReactiveCommand.Create(
					() =>
					{
						this.VideoSourceState = VideoSourceState.Choices;
						this.SourceData = null;
						this.SelectedSource = null;

						this.RefreshRecentSourceOptions();

						this.SelectedTitle = null;

						HandBrakeInstance oldInstance = this.scanInstance;
						this.scanInstance = null;
						this.scanLogger = null;
						oldInstance?.Dispose();
					},
					this.canCloseVideoSourceObservable));
			}
		}

		private ReactiveCommand<Unit, Unit> customizeQueueColumns;
		public ICommand CustomizeQueueColumns
		{
			get
			{
				return this.customizeQueueColumns ?? (this.customizeQueueColumns = ReactiveCommand.Create(() =>
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
				}));
			}
		}

		private ReactiveCommand<Unit, Unit> importPreset;
		public ICommand ImportPreset
		{
			get
			{
				return this.importPreset ?? (this.importPreset = ReactiveCommand.Create(() =>
				{
					string presetFileName = FileService.Instance.GetFileNameLoad(
						null,
						MainRes.ImportPresetFilePickerTitle,
						CommonRes.PresetFileFilter + "|*.vjpreset");
					if (presetFileName != null)
					{
						try
						{
							Preset preset = StaticResolver.Resolve<IPresetImportExport>().ImportPreset(presetFileName);
							StaticResolver.Resolve<IMessageBoxService>().Show(string.Format(MainRes.PresetImportSuccessMessage, preset.Name), CommonRes.Success, System.Windows.MessageBoxButton.OK);
						}
						catch (Exception)
						{
							StaticResolver.Resolve<IMessageBoxService>().Show(MainRes.PresetImportErrorMessage, MainRes.ImportErrorTitle, System.Windows.MessageBoxButton.OK);
						}
					}
				}));
			}
		}

		private ReactiveCommand<Unit, Unit> exportPreset;
		public ICommand ExportPreset
		{
			get
			{
				return this.exportPreset ?? (this.exportPreset = ReactiveCommand.Create(() =>
				{
					StaticResolver.Resolve<IPresetImportExport>().ExportPreset(this.PresetsService.SelectedPreset.Preset);
				}));
			}
		}

		private ReactiveCommand<Unit, Unit> openHomepage;
		public ICommand OpenHomepage
		{
			get
			{
				return this.openHomepage ?? (this.openHomepage = ReactiveCommand.Create(() =>
				{
					FileService.Instance.LaunchUrl("http://vidcoder.net/");
				}));
			}
		}

		private ReactiveCommand<Unit, Unit> reportBug;
		public ICommand ReportBug
		{
			get
			{
				return this.reportBug ?? (this.reportBug = ReactiveCommand.Create(() =>
				{
					FileService.Instance.LaunchUrl("https://github.com/RandomEngy/VidCoder/issues/new");
				}));
			}
		}

		private ReactiveCommand<Unit, Unit> openAppData;
		public ICommand OpenAppData
		{
			get
			{
				return this.openAppData ?? (this.openAppData = ReactiveCommand.Create(() =>
				{
					FileUtilities.OpenFolderAndSelectItem(FileUtilities.GetRealFilePath(Database.DatabaseFile));
				}));
			}
		}

		private ReactiveCommand<Unit, Unit> exit;
		public ICommand Exit
		{
			get
			{
				return this.exit ?? (this.exit = ReactiveCommand.Create(() =>
				{
					this.windowManager.Close(this);
				}));
			}
		}

		private ReactiveCommand<Unit, Unit> showPreviousPreview;
		public ICommand ShowPreviousPreview
		{
			get
			{
				return this.showPreviousPreview ?? (this.showPreviousPreview = ReactiveCommand.Create(
					() =>
					{
						this.PreviewImageServiceClient.ShowPreviousPreview();
					},
					this.PreviewImageServiceClient
						.WhenAnyValue(x => x.PreviewIndex)
						.Select(previewIndex => previewIndex > 0)));
			}
		}

		private ReactiveCommand<Unit, Unit> showNextPreview;
		public ICommand ShowNextPreview
		{
			get
			{
				return this.showNextPreview ?? (this.showNextPreview = ReactiveCommand.Create(
					() =>
					{
						this.PreviewImageServiceClient.ShowNextPreview();
					},
					this.WhenAnyValue(
						x => x.PreviewImageServiceClient.PreviewIndex,
						x => x.PreviewImageService.PreviewCount,
						(previewIndex, previewCount) =>
						{
							return previewIndex < previewCount - 1;
						})));
			}
		}

		private ReactiveCommand<Unit, Unit> openVideoEncoderOptions;
		public ICommand OpenVideoEncoderOptions
		{
			get
			{
				return this.openVideoEncoderOptions ?? (this.openVideoEncoderOptions = ReactiveCommand.Create(() =>
				{
					var encodingWindowViewModel = this.windowManager.OpenOrFocusWindow<EncodingWindowViewModel>();
					encodingWindowViewModel.SelectedTabIndex = EncodingWindowViewModel.VideoEncodingTabIndex;
				}));
			}
		}

		private ReactiveCommand<Unit, Unit> openAudioEncoderOptions;
		public ICommand OpenAudioEncoderOptions
		{
			get
			{
				return this.openAudioEncoderOptions ?? (this.openAudioEncoderOptions = ReactiveCommand.Create(() =>
				{
					var encodingWindowViewModel = this.windowManager.OpenOrFocusWindow<EncodingWindowViewModel>();
					encodingWindowViewModel.SelectedTabIndex = EncodingWindowViewModel.AudioEncodingTabIndex;
				}));
			}
		}

		private ReactiveCommand<Unit, Unit> openPickerToAudio;
		public ICommand OpenPickerToAudio
		{
			get
			{
				return this.openPickerToAudio ?? (this.openPickerToAudio = ReactiveCommand.Create(() =>
				{
					var pickerWindowViewModel = this.windowManager.OpenOrFocusWindow<PickerWindowViewModel>();
					pickerWindowViewModel.View.ScrollAudioSectionIntoView();
				}));
			}
		}

		private ReactiveCommand<Unit, Unit> openPickerToSubtitles;
		public ICommand OpenPickerToSubtitles
		{
			get
			{
				return this.openPickerToSubtitles ?? (this.openPickerToSubtitles = ReactiveCommand.Create(() =>
				{
					var pickerWindowViewModel = this.windowManager.OpenOrFocusWindow<PickerWindowViewModel>();
					pickerWindowViewModel.View.ScrollSubtitlesSectionIntoView();
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
#if DEBUG
					return null;
#else
					throw new InvalidOperationException("Source must be selected.");
#endif
				}

				if (this.SelectedTitle == null)
				{
#if DEBUG
					return null;
#else
					throw new InvalidOperationException("Title must be selected.");
#endif
				}

				SourceType type = this.SelectedSource.Type;

				string outputPath = this.OutputPathService.OutputPath;

				VCProfile encodingProfile = this.PresetsService.SelectedPreset.Preset.EncodingProfile.Clone();

				int title = this.SelectedTitle.Index;

				var job = new VCJob
				{
					SourceType = type,
					SourcePath = this.SourcePath,
					FinalOutputPath = outputPath,
					EncodingProfile = encodingProfile,
					Title = title,
					Angle = this.Angle,
					RangeType = this.RangeType,
					AudioTracks = this.GetChosenAudioTracks(),
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
						TimeSpan titleDuration = this.SelectedTitle.Duration;

						// We start the range end on a whole second value so if it's within 1 second of the title duration, count it as being exactly at the end.
						bool rangeAtEnd = timeRangeEnd == titleDuration || (titleDuration - timeRangeEnd < TimeSpan.FromSeconds(1) && timeRangeEnd.Milliseconds == 0);

						if (timeRangeStart == TimeSpan.Zero && rangeAtEnd)
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
							job.SecondsEnd = rangeAtEnd ? titleDuration.TotalSeconds : timeRangeEnd.TotalSeconds;
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

			if (this.SelectedSource.Type == SourceType.Disc)
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

		public IList<EncodeJobViewModel> SelectedJobs => this.View.SelectedJobs;

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
					case SourceType.Disc:
						DriveInformation driveInfo = this.DriveCollection.FirstOrDefault(d => string.Compare(d.RootDirectory, jobRoot, StringComparison.OrdinalIgnoreCase) == 0);
						if (driveInfo == null)
						{
							StaticResolver.Resolve<IMessageBoxService>().Show(MainRes.DiscNotInDriveError);
							return;
						}

						videoSourceMetadata.Name = driveInfo.VolumeLabel;
						videoSourceMetadata.DriveInfo = driveInfo;
						break;
					case SourceType.File:
						videoSourceMetadata.Name = Utilities.GetSourceNameFile(job.SourcePath);
						break;
					case SourceType.DiscVideoFolder:
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
				this.ProcessingService.RemoveQueueJob(jobVM);
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
				var messageResult = StaticResolver.Resolve<IMessageBoxService>().Show(
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
				((System.Windows.Input.ICommand)this.CancelScan).Execute(null);
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
		public List<ChosenAudioTrack> GetChosenAudioTracks()
		{
			var tracks = new List<ChosenAudioTrack>();

			foreach (AudioTrackViewModel audioTrackVM in this.AudioTracks.Items.Where(track => track.Selected))
			{
				tracks.Add(audioTrackVM.ChosenAudioTrack);
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

						selectedTime = this.SelectedTitle.Title.GetChapterRangeDuration(this.SelectedStartChapter.ChapterNumber, this.SelectedEndChapter.ChapterNumber);

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

			// With a new source we don't want to keep around old subtitles
			this.FileSubtitles.Clear();

			this.ScanProgressFraction = 0;
			HandBrakeInstance oldInstance = this.scanInstance;

			this.scanLogger = StaticResolver.Resolve<AppLoggerFactory>().ResolveLocalScanLogger(path);
			this.scanLogger.Log("Starting scan: " + path);

			this.scanInstance = new HandBrakeInstance();
			this.scanInstance.Initialize(Config.LogVerbosity, noHardware: false);
			this.scanInstance.ScanProgress += (o, e) =>
			{
				this.ScanProgressFraction = e.Progress;
			};
			this.scanInstance.ScanCompleted += (o, e) =>
			{
				DispatchUtilities.Invoke(() =>
				{
					try
					{
						this.scanLogger.Log("Titles JSON:" + Environment.NewLine + this.scanInstance.TitlesJson);

						this.UpdateFromNewVideoSource(new VideoSource {Titles = this.scanInstance.Titles.TitleList, FeatureTitle = this.scanInstance.FeatureTitle});

						// If scan failed source data will be null.
						if (this.sourceData != null)
						{
							if (jobVM != null)
							{
								this.ApplyEncodeJobChoices(jobVM);
							}

							if (jobVM == null && this.SelectedSource != null &&
								(this.SelectedSource.Type == SourceType.File || this.SelectedSource.Type == SourceType.DiscVideoFolder))
							{
								SourceHistory.AddToHistory(this.SourcePath);
							}

							Picker picker = this.PickersService.SelectedPicker.Picker;
							if (picker.AutoQueueOnScan)
							{
								if (this.ProcessingService.TryQueue() && picker.AutoEncodeOnScan && !this.ProcessingService.Encoding)
								{
									this.ProcessingService.Encode.Execute(null);
								}
							}
						}

						this.scanLogger.Log("Scan completed");
					}
					finally
					{
						if (this.scanLogger is LocalScanAppLogger)
						{
							this.scanLogger.Dispose();
						}
					}
				});
			};

			this.scanInstance.StartScan(path, Config.PreviewCount, TimeSpan.FromSeconds(Config.MinimumTitleLengthSeconds), 0);

			oldInstance?.Dispose();
		}

		public void UpdateFromNewVideoSource(VideoSource videoSource)
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

				this.SelectedTitle = this.Titles.Single(title => title.Title == selectTitle);

				DispatchUtilities.BeginInvoke(() =>
				{
					this.View.RefreshSummaryMaxSizes();
				}, DispatcherPriority.Background);
			}
			else
			{
				this.SourceData = null;
				this.VideoSourceState = VideoSourceState.Choices;

				// Raise error
				StaticResolver.Resolve<IMessageBoxService>().Show(this, MainRes.ScanErrorLabel);
			}
		}

		private void ClearVideoSource()
		{
			this.SourceData = null;
		}

		private void LoadVideoSourceMetadata(VCJob job, VideoSourceMetadata metadata)
		{
			this.SourceName = metadata.Name;
			this.SourcePath = job.SourcePath;

			if (job.SourceType == SourceType.Disc)
			{
				this.SelectedSource = new SourceOption { Type = SourceType.Disc, DriveInfo = metadata.DriveInfo };
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
			SourceTitleViewModel newTitle = this.Titles.FirstOrDefault(t => t.Index == job.Title);
			if (newTitle == null)
			{
				newTitle = this.Titles[0];
			}

			this.selectedTitle = newTitle;

			// Angle
			this.PopulateAnglesList();

			if (job.Angle <= this.selectedTitle.Title.AngleCount)
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
					TimeSpan titleDuration = this.selectedTitle.Duration;

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
			this.AudioTracks.Edit(audioTracksInnerList =>
			{
				audioTracksInnerList.Clear();
				foreach (ChosenAudioTrack chosenTrack in job.AudioTracks)
				{
					if (chosenTrack.TrackNumber <= this.selectedTitle.AudioList.Count)
					{
						audioTracksInnerList.Add(new AudioTrackViewModel(this, this.selectedTitle.AudioList[chosenTrack.TrackNumber - 1], chosenTrack) { Selected = true });
					}
				}

				this.FillInUnselectedAudioTracks(audioTracksInnerList);
			});

			this.RefreshAudioSummary();

			// Subtitles (standard+SRT)
			this.SourceSubtitles.Edit(sourceSubtitlesInnerList =>
			{
				sourceSubtitlesInnerList.Clear();

				foreach (ChosenSourceSubtitle sourceSubtitle in job.Subtitles.SourceSubtitles)
				{
					if (sourceSubtitle.TrackNumber <= this.selectedTitle.SubtitleList.Count)
					{
						sourceSubtitlesInnerList.Add(new SourceSubtitleViewModel(this, sourceSubtitle) { Selected = true });
					}
				}

				this.FillInRemainingSourceSubtitles(job.Subtitles.SourceSubtitles, sourceSubtitlesInnerList);
			});

			this.FileSubtitles.Edit(srtSubtitlesInnerList =>
			{
				srtSubtitlesInnerList.Clear();

				foreach (FileSubtitle srtSubtitle in job.Subtitles.FileSubtitles)
				{
					srtSubtitlesInnerList.Add(new FileSubtitleViewModel(this, srtSubtitle));
				}
			});

			this.RefreshSubtitleSummary();

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
			this.OutputPathService.OutputPath = job.FinalOutputPath;
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
			var newStartChapterList = new List<ChapterViewModel>();
			var newEndChapterList = new List<ChapterViewModel>();

			for (int i = 0; i < this.selectedTitle.ChapterList.Count; i++)
			{
				SourceChapter chapter = this.selectedTitle.ChapterList[i];

				var startChapterViewModel = new ChapterViewModel(chapter, i + 1);
				var endChapterViewModel = new ChapterViewModel(chapter, i + 1);

				startChapterViewModel.PropertyChanged += this.OnChapterViewModelPropertyChanged;
				endChapterViewModel.PropertyChanged += this.OnChapterViewModelPropertyChanged;

				newStartChapterList.Add(startChapterViewModel);
				newEndChapterList.Add(endChapterViewModel);
			}

			// We can't re-use the same list because the ComboBox control flyout sizes itself once and doesn't update when items are added.
			this.StartChapters = newStartChapterList;
			this.EndChapters = newEndChapterList;
		}

		private void OnChapterViewModelPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(ChapterViewModel.IsHighlighted))
			{
				this.RefreshRangePreview();
			}
		}

		private void PopulateAnglesList()
		{
			this.angles = new List<int>();
			for (int i = 1; i <= this.selectedTitle.Title.AngleCount; i++)
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
					if (this.sourceOptions[i].SourceOption.Type == SourceType.Disc)
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
					SourceOptionViewModel currentOption = this.sourceOptions.SingleOrDefault(sourceOptionVM => sourceOptionVM.SourceOption.Type == SourceType.Disc && sourceOptionVM.SourceOption.DriveInfo.RootDirectory == drive.RootDirectory);

					if (currentOption == null)
					{
						// The device is new, add it
						var newSourceOptionVM = new SourceOptionViewModel(new SourceOption { Type = SourceType.Disc, DriveInfo = drive });

						bool added = false;
						for (int i = 0; i < this.sourceOptions.Count; i++)
						{
							if (this.sourceOptions[i].SourceOption.Type == SourceType.Disc && string.CompareOrdinal(drive.RootDirectory, this.sourceOptions[i].SourceOption.DriveInfo.RootDirectory) < 0)
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
					this.recentSourceOptions.Add(new SourceOptionViewModel(new SourceOption { Type = SourceType.DiscVideoFolder }, recentSourcePath));
				}
			}

			this.RaisePropertyChanged(nameof(this.RecentSourcesVisible));
		}
	}
}
