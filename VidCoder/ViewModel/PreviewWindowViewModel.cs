using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using HandBrake.ApplicationServices.Interop;
using ReactiveUI;
using VidCoder.Extensions;
using VidCoder.Model;
using VidCoder.Resources;
using VidCoder.Services;
using VidCoder.Services.Windows;
using VidCoderCommon.Model;
using Geometry = HandBrake.ApplicationServices.Interop.Json.Shared.Geometry;

namespace VidCoder.ViewModel
{
	public class PreviewWindowViewModel : ReactiveObject, IClosableWindow
	{
		private const int PreviewImageCacheDistance = 1;
		private const double SubtitleScanCost = 1 / EncodeJobViewModel.SubtitleScanCostFactor;

		private static readonly TimeSpan MinPreviewImageRefreshInterval = TimeSpan.FromSeconds(0.5);
		private static int updateVersion;

		private VCJob job;
		private HandBrakeInstance originalScanInstance;
		private IEncodeProxy encodeProxy;
		private IAppLogger logger = Ioc.Get<IAppLogger>();
		private int selectedPreview;
		private string previewFilePath;
		private bool cancelPending;
		private bool encodeCancelled;
		private int previewSeconds;
		private int previewCount;

		private IDisposable presetsSubscription;

		private DateTime lastImageRefreshTime;
		private System.Timers.Timer previewImageRefreshTimer;
		private bool waitingOnRefresh;
		private BitmapSource[] previewImageCache;
		private Queue<PreviewImageJob> previewImageWorkQueue = new Queue<PreviewImageJob>();
		private bool previewImageQueueProcessing;
		private object imageSync = new object();
		private List<object> imageFileSync;
		private string imageFileCacheFolder;
		private BitmapSource previewBitmapSource;
		private DispatcherTimer seekBarUpdateTimer;
		private bool positionSetByNonUser;

		private MainViewModel mainViewModel = Ioc.Get<MainViewModel>();
		private OutputSizeService outputSizeService = Ioc.Get<OutputSizeService>();
		private PreviewUpdateService previewUpdateService = Ioc.Get<PreviewUpdateService>();

		public PreviewWindowViewModel()
		{
			this.previewUpdateService.PreviewInputChanged += this.OnPreviewInputChanged;

			this.presetsSubscription = this.PresetsService.WhenAnyValue(x => x.SelectedPreset.Preset.EncodingProfile).Skip(1).Subscribe(x =>
			{
				this.RequestRefreshPreviews();
			});

			this.mainViewModel.WhenAnyValue(x => x.SourceData)
				.Skip(1)
				.Subscribe(_ =>
				{
					if (this.PlayingPreview)
					{
						this.PlayingPreview = false;
					}
				});

			// PlayAvailable
			this.WhenAnyValue(x => x.MainViewModel.SourcePath, x => x.HasPreview, (sourcePath, hasPreview) =>
			{
				if (!hasPreview || sourcePath == null)
				{
					return false;
				}

				try
				{
					if (FileUtilities.IsDirectory(sourcePath))
					{
						// Path is a directory. Can only preview when it's a DVD and we have a supported player installed.
						bool isDvd = Utilities.IsDvdFolder(sourcePath);
						bool playerInstalled = Players.Installed.Count > 0;

						return isDvd && playerInstalled;
					}
					else
					{
						// Path is a file
						return true;
					}
				}
				catch (IOException)
				{
					this.RaisePropertyChanged(nameof(this.PlaySourceToolTip));
					return false;
				}
			}).ToProperty(this, x => x.PlayAvailable, out this.playAvailable);

			// SeekBarEnabled
			this.WhenAnyValue(x => x.HasPreview, x => x.GeneratingPreview, (hasPreview, generatingPreview) =>
			{
				return hasPreview && !generatingPreview;
			}).ToProperty(this, x => x.SeekBarEnabled, out this.seekBarEnabled);

			// PlaySourceToolTip
			this.WhenAnyValue(x => x.HasPreview, x => x.MainViewModel.SourcePath, (hasPreview, sourcePath) =>
			{
				if (!hasPreview || sourcePath == null)
				{
					return null;
				}

				try
				{
					if (FileUtilities.IsDirectory(sourcePath))
					{
						// Path is a directory. Can only preview when it's a DVD and we have a supported player installed.
						bool isDvd = Utilities.IsDvdFolder(sourcePath);
						if (!isDvd)
						{
							return PreviewRes.PlaySourceDisabledBluRayToolTip;
						}

						bool playerInstalled = Players.Installed.Count > 0;
						if (!playerInstalled)
						{
							return PreviewRes.PlaySourceDisabledNoPlayerToolTip;
						}
					}
				}
				catch (FileNotFoundException)
				{
					return PreviewRes.PlaySourceDisabledNotFoundToolTip;
				}
				catch (IOException)
				{
				}

				return PreviewRes.PlaySourceToolTip;
			}).ToProperty(this, x => x.PlaySourceToolTip, out this.playSourceToolTip);

			// PreviewAvailable
			this.WhenAnyValue(x => x.PreviewFilePath)
				.Select(path => path != null)
				.ToProperty(this, x => x.PreviewAvailable, out this.previewAvailable);

			// MainDisplay
			this.MainDisplayObservable = this.WhenAnyValue(x => x.HasPreview, x => x.DisplayType, x => x.PlayingPreview, (hasPreview, displayType, playingPreview) =>
			{
				if (!hasPreview)
				{
					return PreviewMainDisplay.None;
				}

				switch (displayType)
				{
					case PreviewDisplay.Default:
						return PreviewMainDisplay.Default;
					case PreviewDisplay.FitToWindow:
						return PreviewMainDisplay.FitToWindow;
					case PreviewDisplay.OneToOne:
						return PreviewMainDisplay.OneToOne;
					case PreviewDisplay.Corners:
						return playingPreview ? PreviewMainDisplay.Default : PreviewMainDisplay.StillCorners;
					default:
						throw new ArgumentOutOfRangeException(nameof(displayType), displayType, null);
				}
			});

			this.MainDisplayObservable.ToProperty(this, x => x.MainDisplay, out this.mainDisplay);

			// Controls
			this.ControlsObservable = this.WhenAnyValue(x => x.GeneratingPreview, x => x.PlayingPreview, (generatingPreview, playingPreview) =>
			{
				if (playingPreview)
				{
					return PreviewControls.Playback;
				}

				if (generatingPreview)
				{
					return PreviewControls.Generation;
				}

				return PreviewControls.Creation;
			});

			this.ControlsObservable.ToProperty(this, x => x.Controls, out this.controls);

			// InCornerDisplayMode
			this.WhenAnyValue(x => x.DisplayType)
				.Select(displayType => displayType == PreviewDisplay.Corners)
				.ToProperty(this, x => x.InCornerDisplayMode, out this.inCornerDisplayMode);

			// GeneratingPreview
			this.WhenAnyValue(x => x.EncodeState)
				.Select(encodeState => encodeState == PreviewEncodeState.EncodeStarting || encodeState == PreviewEncodeState.Encoding)
				.ToProperty(this, x => x.GeneratingPreview, out this.generatingPreview);

			this.PlaySource = ReactiveCommand.Create(this.WhenAnyValue(x => x.PlayAvailable));
			this.PlaySource.Subscribe(_ => this.PlaySourceImpl());

			this.ReopenPreview = ReactiveCommand.Create();
			this.ReopenPreview.Subscribe(_ => this.ReopenPreviewImpl());

			this.GeneratePreview = ReactiveCommand.Create(this.WhenAnyValue(x => x.HasPreview));
			this.GeneratePreview.Subscribe(_ => this.GeneratePreviewImpl());

			this.CancelPreview = ReactiveCommand.Create(
				this.WhenAnyValue(x => x.EncodeState).Select(encodeState => encodeState == PreviewEncodeState.Encoding));
			this.CancelPreview.Subscribe(_ => this.CancelPreviewImpl());

			this.Pause = ReactiveCommand.Create();
			this.Pause.Subscribe(_ => this.PauseImpl());

			this.Play = ReactiveCommand.Create();
			this.Play.Subscribe(_ => this.PlayImpl());

			this.OpenInSystemPlayer = ReactiveCommand.Create();
			this.OpenInSystemPlayer.Subscribe(_ => this.OpenInSystemPlayerImpl());

			this.CloseVideo = ReactiveCommand.Create();
			this.CloseVideo.Subscribe(_ => this.CloseVideoImpl());

			this.previewSeconds = Config.PreviewSeconds;
			this.displayType = CustomConfig.PreviewDisplay;
			this.selectedPreview = 1;
			this.Title = PreviewRes.NoVideoSourceTitle;

			this.RequestRefreshPreviews();
		}

		private void OnPreviewInputChanged(object sender, EventArgs eventArgs)
		{
			this.RequestRefreshPreviews();
		}

		public IPreviewView View { get; set; }

		public void OnClosing()
		{
			this.previewUpdateService.PreviewInputChanged -= this.OnPreviewInputChanged;
			this.presetsSubscription.Dispose();

			if (this.GeneratingPreview)
			{
				this.StopAndWait();
			}
		}

		public MainViewModel MainViewModel
		{
			get { return this.mainViewModel; }
		}

		public ProcessingService ProcessingService { get; } = Ioc.Get<ProcessingService>();

		public PresetsService PresetsService { get; } = Ioc.Get<PresetsService>();

		public OutputPathService OutputPathService { get; } = Ioc.Get<OutputPathService>();

		private OutputSizeService OutputSizeService
		{
			get { return this.outputSizeService; }
		}

		private string title;

		public string Title
		{
			get { return this.title; }
			set { this.RaiseAndSetIfChanged(ref this.title, value); }
		}

		private BitmapSource previewImage;

		public BitmapSource PreviewImage
		{
			get
			{
				lock (this.imageSync)
				{
					return this.previewImage;
				}
			}
			set { this.RaiseAndSetIfChanged(ref this.previewImage, value); }
		}

		public string PreviewFilePath => this.previewFilePath;

		private PreviewEncodeState encodeState;

		public PreviewEncodeState EncodeState
		{
			get { return this.encodeState; }
			set { this.RaiseAndSetIfChanged(ref this.encodeState, value); }
		}

		private bool playingPreview;
		public bool PlayingPreview
		{
			get { return this.playingPreview; }
			set
			{
				this.RaiseAndSetIfChanged(ref this.playingPreview, value);
				if (!value && this.seekBarUpdateTimer != null && this.seekBarUpdateTimer.IsEnabled)
				{
					this.seekBarUpdateTimer.Stop();
					this.seekBarUpdateTimer = null;
				}
			}
		}

		private bool previewPaused;
		public bool PreviewPaused
		{
			get { return this.previewPaused; }
			set { this.RaiseAndSetIfChanged(ref this.previewPaused, value); }
		}

		private TimeSpan previewVideoPosition;
		public TimeSpan PreviewVideoPosition
		{
			get { return this.previewVideoPosition; }
			set
			{
				this.RaiseAndSetIfChanged(ref this.previewVideoPosition, value);
				if (!this.positionSetByNonUser)
				{
					this.View.SeekToViewModelPosition(value);
				}
			}
		}

		// When the video playback progresses naturally or is reset by system
		public void SetVideoPositionFromNonUser(TimeSpan position)
		{
			this.positionSetByNonUser = true;
			this.PreviewVideoPosition = position;
			this.positionSetByNonUser = false;
		}

		private TimeSpan previewVideoDuration;
		public TimeSpan PreviewVideoDuration
		{
			get { return this.previewVideoDuration; }
			set { this.RaiseAndSetIfChanged(ref this.previewVideoDuration, value); }
		}

		private double volume = Config.PreviewVolume;
		public double Volume
		{
			get { return this.volume; }
			set
			{
				this.RaiseAndSetIfChanged(ref this.volume, value);
				Config.PreviewVolume = value;
			}
		}

		public IObservable<PreviewMainDisplay> MainDisplayObservable { get; private set; }

		private ObservableAsPropertyHelper<PreviewMainDisplay> mainDisplay;
		public PreviewMainDisplay MainDisplay => this.mainDisplay.Value;

		public IObservable<PreviewControls> ControlsObservable { get; private set; } 

		private ObservableAsPropertyHelper<PreviewControls> controls;
		public PreviewControls Controls => this.controls.Value;

		private ObservableAsPropertyHelper<bool> generatingPreview;
		public bool GeneratingPreview => this.generatingPreview.Value;

		private ObservableAsPropertyHelper<bool> inCornerDisplayMode;
		public bool InCornerDisplayMode => this.inCornerDisplayMode.Value;

		private ObservableAsPropertyHelper<bool> seekBarEnabled;
		public bool SeekBarEnabled => this.seekBarEnabled.Value;

		private double previewPercentComplete;

		public double PreviewPercentComplete
		{
			get { return this.previewPercentComplete; }
			set { this.RaiseAndSetIfChanged(ref this.previewPercentComplete, value); }
		}

		public int PreviewSeconds
		{
			get { return this.previewSeconds; }

			set
			{
				this.previewSeconds = value;
				this.RaisePropertyChanged();

				Config.PreviewSeconds = value;
			}
		}

		private bool hasPreview;

		public bool HasPreview
		{
			get { return this.hasPreview; }
			set { this.RaiseAndSetIfChanged(ref this.hasPreview, value); }
		}

		public int SelectedPreview
		{
			get { return this.selectedPreview; }

			set
			{
				this.selectedPreview = value;
				this.RaisePropertyChanged();

				if (this.DisplayType == PreviewDisplay.Corners)
				{
					this.RequestRefreshPreviews();
				}
				else
				{
					lock (this.imageSync)
					{
						this.previewBitmapSource = this.previewImageCache[value];
						this.RefreshFromBitmapImage();
						this.ClearOutOfRangeItems();
						this.BeginBackgroundImageLoad();
					}
				}
			}
		}

		/// <summary>
		/// Gets or sets the display width of the preview image in pixels.
		/// </summary>
		public double PreviewDisplayWidth { get; set; }

		/// <summary>
		/// Gets or sets the display height of the preview image in pixels.
		/// </summary>
		public double PreviewDisplayHeight { get; set; }

		public int SliderMax
		{
			get
			{
				if (this.previewCount > 0)
				{
					return this.previewCount - 1;
				}

				return Config.PreviewCount - 1;
			}
		}

		public int PreviewCount
		{
			get
			{
				if (this.previewCount > 0)
				{
					return this.previewCount;
				}

				return Config.PreviewCount;
			}
		}

		private ObservableAsPropertyHelper<string> playSourceToolTip;
		public string PlaySourceToolTip => this.playSourceToolTip.Value;

		private ObservableAsPropertyHelper<bool> playAvailable;
		public bool PlayAvailable => this.playAvailable.Value;

		private PreviewDisplay displayType;

		public PreviewDisplay DisplayType
		{
			get { return this.displayType; }

			set
			{
				if (this.displayType != value)
				{
					this.displayType = value;
					this.RaisePropertyChanged();

					CustomConfig.PreviewDisplay = value;

					this.RequestRefreshPreviews();
				}
			}
		}

		public HandBrakeInstance ScanInstance
		{
			get { return this.mainViewModel.ScanInstance; }
		}

		public ReactiveCommand<object> GeneratePreview { get; }
		private void GeneratePreviewImpl()
		{
			this.job = this.mainViewModel.EncodeJob;

			this.PreviewPercentComplete = 0;
			this.EncodeState = PreviewEncodeState.EncodeStarting;
			this.cancelPending = false;
			this.encodeCancelled = false;

			this.SetPreviewFilePath();

			this.job.OutputPath = this.previewFilePath;

			this.encodeProxy = Utilities.CreateEncodeProxy();
			this.encodeProxy.EncodeStarted += (o, e) =>
			{
				DispatchUtilities.BeginInvoke(() =>
				{
					this.EncodeState = PreviewEncodeState.Encoding;
					if (this.cancelPending)
					{
						this.CancelPreviewImpl();
					}
				});
			};
			this.encodeProxy.EncodeProgress += (o, e) =>
			{
				double totalWeight;
				double completeWeight;
				if (e.PassCount == 1)
				{
					// Single pass, no subtitle scan
					totalWeight = 1;
					completeWeight = e.FractionComplete;
				}
				else if (e.PassCount == 2 && e.PassId <= 0)
				{
					// Single pass with subtitle scan
					totalWeight = 1 + SubtitleScanCost;
					if (e.PassId == -1)
					{
						// In subtitle scan
						completeWeight = e.FractionComplete * SubtitleScanCost;
					}
					else
					{
						// In normal pass
						completeWeight = SubtitleScanCost + e.FractionComplete;
					}
				}
				else if (e.PassCount == 2 && e.PassId >= 1)
				{
					// Two normal passes
					totalWeight = 2;

					if (e.PassId == 1)
					{
						// First pass
						completeWeight = e.FractionComplete;
					}
					else
					{
						// Second pass
						completeWeight = 1 + e.FractionComplete;
					}
				}
				else
				{
					// Two normal passes with subtitle scan
					totalWeight = 2 + SubtitleScanCost;

					if (e.PassId == -1)
					{
						// In subtitle scan
						completeWeight = e.FractionComplete * SubtitleScanCost;
					}
					else if (e.PassId == 1)
					{
						// First normal pass
						completeWeight = SubtitleScanCost + e.FractionComplete;
					}
					else
					{
						// Second normal pass
						completeWeight = SubtitleScanCost + 1 + e.FractionComplete;
					}
				}


				double fractionComplete = completeWeight / totalWeight;
				this.PreviewPercentComplete = fractionComplete * 100;
			};
			this.encodeProxy.EncodeCompleted += (o, e) =>
			{
				DispatchUtilities.BeginInvoke(() =>
				{
					this.EncodeState = PreviewEncodeState.NotEncoding;

					if (this.encodeCancelled)
					{
						this.logger.Log("Cancelled preview clip generation");
					}
					else
					{
						if (e.Error)
						{
							this.logger.Log(PreviewRes.PreviewClipGenerationFailedTitle);
							Utilities.MessageBox.Show(PreviewRes.PreviewClipGenerationFailedMessage);
						}
						else
						{
							var previewFileInfo = new FileInfo(this.previewFilePath);
							this.logger.Log("Finished preview clip generation. Size: " + Utilities.FormatFileSize(previewFileInfo.Length));

							this.PlayPreview();
						}
					}
				});
			};

			this.logger.Log("Generating preview clip");
			this.logger.Log("  Path: " + this.job.OutputPath);
			this.logger.Log("  Title: " + this.job.Title);
			this.logger.Log("  Preview #: " + this.SelectedPreview);

			this.encodeProxy.StartEncode(this.job, this.logger, true, this.SelectedPreview, this.PreviewSeconds, this.job.Length.TotalSeconds);
		}

		private void PlayPreview()
		{
			if (!Config.UseBuiltInPlayerForPreviews || this.DisplayType == PreviewDisplay.Corners)
			{
				FileService.Instance.PlayVideo(this.previewFilePath);
			}
			else
			{
				this.PlayingPreview = true;
				this.SetVideoPositionFromNonUser(TimeSpan.Zero);
				this.PreviewPaused = false;

				this.seekBarUpdateTimer = new DispatcherTimer();
				this.seekBarUpdateTimer.Interval = TimeSpan.FromMilliseconds(250);
				this.seekBarUpdateTimer.Tick += (sender, args) => { this.View.RefreshViewModelFromMediaElement(); };

				this.seekBarUpdateTimer.Start();
			}
		}

		private void SetPreviewFilePath()
		{
			string extension = OutputPathService.GetExtensionForProfile(this.job.EncodingProfile);

			string previewDirectory;
			if (Config.UseCustomPreviewFolder)
			{
				previewDirectory = Config.PreviewOutputFolder;
			}
			else
			{
				previewDirectory = Utilities.LocalAppFolder;
			}

			try
			{
				if (!Directory.Exists(previewDirectory))
				{
					Directory.CreateDirectory(previewDirectory);
				}
			}
			catch (Exception exception)
			{
				this.logger.LogError("Could not create preview directory " + Config.PreviewOutputFolder + Environment.NewLine + exception);
				previewDirectory = Utilities.LocalAppFolder;
			}

			this.previewFilePath = Path.Combine(previewDirectory, "preview" + extension);

			if (File.Exists(this.previewFilePath))
			{
				try
				{
					File.Delete(this.previewFilePath);
				}
				catch (Exception)
				{
					this.previewFilePath = FileUtilities.CreateUniqueFileName(this.previewFilePath, new HashSet<string>());
				}
			}

			this.RaisePropertyChanged(nameof(this.PreviewFilePath));
		}

		public ReactiveCommand<object> PlaySource { get; }
		private void PlaySourceImpl()
		{
			string sourcePath = this.mainViewModel.SourcePath;

			try
			{
				if (FileUtilities.IsDirectory(sourcePath))
				{
					// Path is a directory
					IVideoPlayer player = Players.Installed.FirstOrDefault(p => p.Id == Config.PreferredPlayer);
					if (player == null)
					{
						player = Players.Installed[0];
					}

					player.PlayTitle(sourcePath, this.mainViewModel.SelectedTitle.Index);
				}
				else
				{
					// Path is a file
					FileService.Instance.PlayVideo(sourcePath);
				}
			}
			catch (IOException)
			{
				this.RaisePropertyChanged(nameof(this.PlayAvailable));
			}
		}

		public ReactiveCommand<object> ReopenPreview { get; }
		private void ReopenPreviewImpl()
		{
			this.PlayPreview();
		}

		private ObservableAsPropertyHelper<bool> previewAvailable;
		public bool PreviewAvailable => this.previewAvailable.Value;

		public ReactiveCommand<object> CancelPreview { get; }
		private void CancelPreviewImpl()
		{
			this.encodeCancelled = true;
			this.encodeProxy.StopEncode();
		}

		public ReactiveCommand<object> Pause { get; }
		private void PauseImpl()
		{
			this.PreviewPaused = true;
		}

		public ReactiveCommand<object> Play { get; }
		private void PlayImpl()
		{
			this.PreviewPaused = false;
		}

		public ReactiveCommand<object> OpenInSystemPlayer { get; }
		private void OpenInSystemPlayerImpl()
		{
			this.PlayingPreview = false;
			FileService.Instance.PlayVideo(this.previewFilePath);
		}

		public ReactiveCommand<object> CloseVideo { get; }
		private void CloseVideoImpl()
		{
			this.PlayingPreview = false;
		}

		private void RequestRefreshPreviews()
		{
			if (!this.mainViewModel.HasVideoSource || this.outputSizeService.Size == null)
			{
				this.HasPreview = false;
				this.Title = PreviewRes.NoVideoSourceTitle;
				this.TryCancelPreviewEncode();
				return;
			}

			if (this.originalScanInstance != this.ScanInstance || (this.job != null && this.job.Title != this.mainViewModel.EncodeJob.Title))
			{
				this.TryCancelPreviewEncode();
			}

			if (this.waitingOnRefresh)
			{
				return;
			}

			DateTime now = DateTime.Now;
			TimeSpan timeSinceLastRefresh = now - this.lastImageRefreshTime;
			if (timeSinceLastRefresh < MinPreviewImageRefreshInterval)
			{
				this.waitingOnRefresh = true;
				TimeSpan timeUntilNextRefresh = MinPreviewImageRefreshInterval - timeSinceLastRefresh;
				this.previewImageRefreshTimer = new System.Timers.Timer(timeUntilNextRefresh.TotalMilliseconds);
				this.previewImageRefreshTimer.Elapsed += this.previewImageRefreshTimer_Elapsed;
				this.previewImageRefreshTimer.AutoReset = false;
				this.previewImageRefreshTimer.Start();

				return;
			}

			this.lastImageRefreshTime = now;

			this.RefreshPreviews();
		}

		private void StopAndWait()
		{
			this.encodeCancelled = true;
			this.encodeProxy.StopAndWait();
		}

		private void previewImageRefreshTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
		{
			this.waitingOnRefresh = false;
			this.lastImageRefreshTime = DateTime.MinValue;
			DispatchUtilities.BeginInvoke(this.RefreshPreviews);
		}

		private void RefreshPreviews()
		{
			this.originalScanInstance = this.ScanInstance;
			this.job = this.mainViewModel.EncodeJob;

			Geometry outputGeometry = this.outputSizeService.Size;

			int width = outputGeometry.Width;
			int height = outputGeometry.Height;
			int parWidth = outputGeometry.PAR.Num;
			int parHeight = outputGeometry.PAR.Den;

			if (parWidth <= 0 || parHeight <= 0)
			{
				this.HasPreview = false;
				this.Title = PreviewRes.NoVideoSourceTitle;

				Ioc.Get<IAppLogger>().LogError("HandBrake returned a negative pixel aspect ratio. Cannot show preview.");
				return;
			}

			if (width < 100 || height < 100)
			{
				this.HasPreview = false;
				this.UpdateTitle(outputGeometry);

				return;
			}

			this.PreviewDisplayHeight = height;
			this.PreviewDisplayWidth = width * ((double)parWidth / parHeight);


			// Update the number of previews.
			this.previewCount = this.ScanInstance.PreviewCount;
			if (this.selectedPreview >= this.previewCount)
			{
				this.selectedPreview = this.previewCount - 1;
				this.RaisePropertyChanged(nameof(this.SelectedPreview));
			}

			this.RaisePropertyChanged(nameof(this.PreviewCount));

			this.HasPreview = true;

			lock (this.imageSync)
			{
				this.previewImageCache = new BitmapSource[this.previewCount];
				updateVersion++;

				// Clear main work queue.
				this.previewImageWorkQueue.Clear();

				this.imageFileCacheFolder = Path.Combine(
					Utilities.ImageCacheFolder, 
					Process.GetCurrentProcess().Id.ToString(CultureInfo.InvariantCulture), 
					updateVersion.ToString(CultureInfo.InvariantCulture));
				if (!Directory.Exists(this.imageFileCacheFolder))
				{
					Directory.CreateDirectory(this.imageFileCacheFolder);
				}

				// Clear old images out of the file cache.
				this.ClearImageFileCache();

				this.imageFileSync = new List<object>(this.previewCount);
				for (int i = 0; i < this.previewCount; i++)
				{
					this.imageFileSync.Add(new object());
				}

				this.BeginBackgroundImageLoad();

				this.View?.RefreshImageSize();
			}

			this.UpdateTitle(outputGeometry);
		}

		private void UpdateTitle(Geometry size)
		{
			if (size.PAR.Num == size.PAR.Den)
			{
				this.Title = string.Format(PreviewRes.PreviewWindowTitleSimple, size.Width, size.Height);
			}
			else
			{
				this.Title = string.Format(
					PreviewRes.PreviewWindowTitleComplex,
					Math.Round(this.PreviewDisplayWidth), 
					Math.Round(this.PreviewDisplayHeight),
					size.Width, 
					size.Height);
			}
		}

		private void ClearImageFileCache()
		{
			try
			{
				string processCacheFolder = Path.Combine(Utilities.ImageCacheFolder, Process.GetCurrentProcess().Id.ToString(CultureInfo.InvariantCulture));
				if (!Directory.Exists(processCacheFolder))
				{
					return;
				}

				int lowestUpdate = -1;
				for (int i = updateVersion - 1; i >= 1; i--)
				{
					if (Directory.Exists(Path.Combine(processCacheFolder, i.ToString(CultureInfo.InvariantCulture))))
					{
						lowestUpdate = i;
					}
					else
					{
						break;
					}
				}

				if (lowestUpdate == -1)
				{
					return;
				}

				for (int i = lowestUpdate; i <= updateVersion - 1; i++)
				{
					FileUtilities.DeleteDirectory(Path.Combine(processCacheFolder, i.ToString(CultureInfo.InvariantCulture)));
				}
			}
			catch (IOException)
			{
				// Ignore. Later checks will clear the cache.
			}
		}

		private void TryCancelPreviewEncode()
		{
			switch (this.EncodeState)
			{
				case PreviewEncodeState.NotEncoding:
					break;
				case PreviewEncodeState.EncodeStarting:
					this.cancelPending = true;
					break;
				case PreviewEncodeState.Encoding:
					this.CancelPreviewImpl();
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		private void ClearOutOfRangeItems()
		{
			// Remove out of range items from work queue
			var newWorkQueue = new Queue<PreviewImageJob>();
			while (this.previewImageWorkQueue.Count > 0)
			{
				PreviewImageJob job = this.previewImageWorkQueue.Dequeue();
				if (Math.Abs(job.PreviewNumber - this.SelectedPreview) <= PreviewImageCacheDistance)
				{
					newWorkQueue.Enqueue(job);
				}
			}

			// Remove out of range cache entries
			for (int i = 0; i < this.previewCount; i++)
			{
				if (Math.Abs(i - this.SelectedPreview) > PreviewImageCacheDistance)
				{
					this.previewImageCache[i] = null;
				}
			}
		}

		private void BeginBackgroundImageLoad()
		{
			int currentPreview = this.SelectedPreview;

			if (!ImageLoadedOrLoading(currentPreview))
			{
				this.EnqueueWork(currentPreview);
			}

			for (int i = 1; i <= PreviewImageCacheDistance; i++)
			{
				if (currentPreview - i >= 0 && !ImageLoadedOrLoading(currentPreview - i))
				{
					EnqueueWork(currentPreview - i);
				}

				if (currentPreview + i < this.previewCount && !ImageLoadedOrLoading(currentPreview + i))
				{
					EnqueueWork(currentPreview + i);
				}
			}

			// Start a queue processing thread if one is not going already.
			if (!this.previewImageQueueProcessing && this.previewImageWorkQueue.Count > 0)
			{
				ThreadPool.QueueUserWorkItem(this.ProcessPreviewImageWork);
				this.previewImageQueueProcessing = true;
			}
		}

		private bool ImageLoadedOrLoading(int previewNumber)
		{
			if (this.previewImageCache[previewNumber] != null)
			{
				return true;
			}

			if (this.previewImageWorkQueue.Count(j => j.PreviewNumber == previewNumber) > 0)
			{
				return true;
			}

			return false;
		}

		private void EnqueueWork(int previewNumber)
		{
			this.previewImageWorkQueue.Enqueue(new PreviewImageJob
			{
				UpdateVersion = updateVersion,
				ScanInstance = this.ScanInstance,
				PreviewNumber = previewNumber,
				Profile = this.job.EncodingProfile,
				Title = this.MainViewModel.SelectedTitle,
				ImageFileSync = this.imageFileSync[previewNumber]
			});
		}

		private void ProcessPreviewImageWork(object state)
		{
			PreviewImageJob imageJob;
			bool workLeft = true;

			while (workLeft)
			{
				lock (this.imageSync)
				{
					if (this.previewImageWorkQueue.Count == 0)
					{
						this.previewImageQueueProcessing = false;
						return;
					}

					imageJob = this.previewImageWorkQueue.Dequeue();
					while (imageJob.UpdateVersion < updateVersion)
					{
						if (this.previewImageWorkQueue.Count == 0)
						{
							this.previewImageQueueProcessing = false;
							return;
						}

						imageJob = this.previewImageWorkQueue.Dequeue();
					}
				}

				string imagePath = Path.Combine(Utilities.ImageCacheFolder, Process.GetCurrentProcess().Id.ToString(CultureInfo.InvariantCulture), imageJob.UpdateVersion.ToString(CultureInfo.InvariantCulture), imageJob.PreviewNumber + ".bmp");
				BitmapSource imageSource = null;

				// Check the disc cache for the image
				lock (imageJob.ImageFileSync)
				{
					if (File.Exists(imagePath))
					{
						// When we read from disc cache the image is already transformed.
						var bitmapImage = new BitmapImage();
						bitmapImage.BeginInit();
						bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
						bitmapImage.UriSource = new Uri(imagePath);
						bitmapImage.EndInit();
						bitmapImage.Freeze();

						imageSource = bitmapImage;
					}
				}

				if (imageSource == null && !imageJob.ScanInstance.IsDisposed)
				{
					// Make a HandBrake call to get the image
					imageSource = imageJob.ScanInstance.GetPreview(imageJob.Profile.CreatePreviewSettings(imageJob.Title), imageJob.PreviewNumber);

					// Transform the image as per rotation and reflection settings
					VCProfile profile = imageJob.Profile;
					if (profile.FlipHorizontal || profile.FlipVertical || profile.Rotation != VCPictureRotation.None)
					{
						imageSource = CreateTransformedBitmap(imageSource, profile);
					}

					// Start saving the image file in the background and continue to process the queue.
					ThreadPool.QueueUserWorkItem(this.BackgroundFileSave, new SaveImageJob
					{
						PreviewNumber = imageJob.PreviewNumber,
						UpdateVersion = imageJob.UpdateVersion,
						FilePath = imagePath,
						Image = imageSource,
						ImageFileSync = imageJob.ImageFileSync
					});
				}

				lock (this.imageSync)
				{
					if (imageJob.UpdateVersion == updateVersion)
					{
						this.previewImageCache[imageJob.PreviewNumber] = imageSource;
						if (this.SelectedPreview == imageJob.PreviewNumber)
						{
							DispatchUtilities.BeginInvoke(() =>
							{
								this.previewBitmapSource = imageSource;
								this.RefreshFromBitmapImage();
							});
						}
					}

					if (this.previewImageWorkQueue.Count == 0)
					{
						workLeft = false;
						this.previewImageQueueProcessing = false;
					}
				}
			}
		}

		private static TransformedBitmap CreateTransformedBitmap(BitmapSource source, VCProfile profile)
		{
			var transformedBitmap = new TransformedBitmap();
			transformedBitmap.BeginInit();
			transformedBitmap.Source = source;
			var transformGroup = new TransformGroup();
			transformGroup.Children.Add(new ScaleTransform(profile.FlipHorizontal ? -1 : 1, profile.FlipVertical ? -1 : 1));
			transformGroup.Children.Add(new RotateTransform(ConvertRotationToDegrees(profile.Rotation)));
			transformedBitmap.Transform = transformGroup;
			transformedBitmap.EndInit();
			transformedBitmap.Freeze();

			return transformedBitmap;
		}

		private static double ConvertRotationToDegrees(VCPictureRotation rotation)
		{
			switch (rotation)
			{
				case VCPictureRotation.None:
					return 0;
				case VCPictureRotation.Clockwise90:
					return 90;
				case VCPictureRotation.Clockwise180:
					return 180;
				case VCPictureRotation.Clockwise270:
					return 270;
			}

			return 0;
		}

		/// <summary>
		/// Refreshes the view using this.previewBitmapSource.
		/// </summary>
		private void RefreshFromBitmapImage()
		{
			if (this.previewBitmapSource == null)
			{
				return;
			}

			this.PreviewImage = this.previewBitmapSource;
		}

		private void BackgroundFileSave(object state)
		{
			var job = state as SaveImageJob;

			lock (this.imageSync)
			{
				if (job.UpdateVersion < updateVersion)
				{
					return;
				}
			}

			lock (job.ImageFileSync)
			{
				try
				{
					using (var memoryStream = new MemoryStream())
					{
						// Write the bitmap out to a memory stream before saving so that we won't be holding
						// a write lock on the BitmapImage for very long; it's used in the UI.
						var encoder = new BmpBitmapEncoder();
						encoder.Frames.Add(BitmapFrame.Create(job.Image));
						encoder.Save(memoryStream);

						using (var fileStream = new FileStream(job.FilePath, FileMode.Create))
						{
							fileStream.Write(memoryStream.GetBuffer(), 0, (int)memoryStream.Length);
						}
					}
				}
				catch (IOException)
				{
					// Directory may have been deleted. Ignore.
				}
			}
		}

		public void OnVideoCompleted()
		{
			this.PlayingPreview = false;
		}

		public void OnVideoFailed()
		{
			this.PlayingPreview = false;
			MessageBoxResult result = Ioc.Get<IMessageBoxService>().Show(this, PreviewRes.VideoErrorMessage, PreviewRes.VideoErrorTitle, MessageBoxButton.YesNo);
			if (result == MessageBoxResult.Yes)
			{
				FileService.Instance.PlayVideo(this.previewFilePath);
			}
		}
	}
}
