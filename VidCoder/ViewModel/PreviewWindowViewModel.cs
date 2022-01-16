using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Threading;
using HandBrake.Interop.Interop;
using Microsoft.AnyContainer;
using ReactiveUI;
using VidCoder.Extensions;
using VidCoder.Model;
using VidCoder.Resources;
using VidCoder.Services;
using VidCoder.Services.Windows;
using VidCoderCommon;
using VidCoderCommon.Model;
using Geometry = HandBrake.Interop.Interop.Json.Shared.Geometry;

namespace VidCoder.ViewModel
{
	public class PreviewWindowViewModel : ReactiveObject, IClosableWindow
	{
		private const double SubtitleScanCost = 1 / EncodeJobViewModel.SubtitleScanCostFactor;

		private VCJob job;
		private IEncodeProxy encodeProxy;
		private IAppLogger logger = StaticResolver.Resolve<IAppLogger>();
		private string previewFilePath;
		private bool cancelPending;
		private bool encodeCanceled;
		private int previewSeconds;


		private HandBrakeInstance originalScanInstance;

		private DispatcherTimer seekBarUpdateTimer;
		private bool positionSetByNonUser;

		private readonly OutputSizeService outputSizeService = StaticResolver.Resolve<OutputSizeService>();
		private MainViewModel mainViewModel = StaticResolver.Resolve<MainViewModel>();
		private PreviewUpdateService previewUpdateService = StaticResolver.Resolve<PreviewUpdateService>();

		public PreviewWindowViewModel()
		{
			this.previewUpdateService.PreviewInputChanged += this.OnPreviewInputChanged;

			this.displayType = CustomConfig.PreviewDisplay;
			this.previewSeconds = Config.PreviewSeconds;

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
			this.WhenAnyValue(x => x.MainViewModel.SourcePath, x => x.PreviewImageService.HasPreview, (sourcePath, hasPreview) =>
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

			// GeneratingPreview
			this.WhenAnyValue(x => x.EncodeState)
				.Select(encodeState => encodeState == PreviewEncodeState.EncodeStarting || encodeState == PreviewEncodeState.Encoding)
				.ToProperty(this, x => x.GeneratingPreview, out this.generatingPreview);

			// SeekBarEnabled
			this.WhenAnyValue(x => x.PreviewImageService.HasPreview, x => x.GeneratingPreview, (hasPreview, generatingPreview) =>
			{
				return hasPreview && !generatingPreview;
			}).ToProperty(this, x => x.SeekBarEnabled, out this.seekBarEnabled);

			// PlaySourceToolTip
			this.WhenAnyValue(x => x.PreviewImageService.HasPreview, x => x.MainViewModel.SourcePath, (hasPreview, sourcePath) =>
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
			this.MainDisplayObservable = this.WhenAnyValue(x => x.PreviewImageService.HasPreview, x => x.DisplayType, x => x.PlayingPreview, (hasPreview, displayType, playingPreview) =>
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

			this.MainDisplayObservable.ToProperty(this, x => x.MainDisplay, out this.mainDisplay, scheduler: Scheduler.Immediate);

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

			// Title
			this.outputSizeService.WhenAnyValue(x => x.Size)
				.Select(outputSize =>
				{
					if (outputSize == null)
					{
						return PreviewRes.NoVideoSourceTitle;
					}

					return this.CreateTitle(outputSize);
				}).ToProperty(this, x => x.Title, out this.title);

			this.MainViewModel.WhenAnyValue(x => x.HasVideoSource).Subscribe(hasVideoSource =>
			{
				if (!hasVideoSource)
				{
					this.TryCancelPreviewEncode();
				}
			});

			this.outputSizeService.WhenAnyValue(x => x.Size).Subscribe(size =>
			{
				if (size == null)
				{
					this.TryCancelPreviewEncode();
				}
			});

			this.PreviewImageService.RegisterClient(this.PreviewImageServiceClient);

			this.View?.RefreshImageSize();
		}

		private void OnPreviewInputChanged(object sender, EventArgs eventArgs)
		{
			if (!this.mainViewModel.HasVideoSource || this.outputSizeService.Size == null)
			{
				this.TryCancelPreviewEncode();
				return;
			}

			if (this.originalScanInstance != this.ScanInstance || (this.job != null && this.job.Title != this.mainViewModel.EncodeJob.Title))
			{
				this.TryCancelPreviewEncode();
			}

			this.View?.RefreshImageSize();
		}

		public HandBrakeInstance ScanInstance
		{
			get { return this.mainViewModel.ScanInstance; }
		}

		public IPreviewView View { get; set; }

		public bool OnClosing()
		{
			this.previewUpdateService.PreviewInputChanged -= this.OnPreviewInputChanged;
			this.PreviewImageService.RemoveClient(this.PreviewImageServiceClient);

			if (this.GeneratingPreview)
			{
				this.encodeCanceled = true;
				this.encodeProxy.StopEncodeAsync();
			}

			return true;
		}

		public MainViewModel MainViewModel
		{
			get { return this.mainViewModel; }
		}

		public PreviewImageService PreviewImageService { get; } = StaticResolver.Resolve<PreviewImageService>();

		public PreviewImageServiceClient PreviewImageServiceClient { get; } = new PreviewImageServiceClient();

		public ProcessingService ProcessingService { get; } = StaticResolver.Resolve<ProcessingService>();


		public OutputPathService OutputPathService { get; } = StaticResolver.Resolve<OutputPathService>();

		private ObservableAsPropertyHelper<string> title;
		public string Title => this.title.Value;

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
			get => this.previewVideoDuration;
			set => this.RaiseAndSetIfChanged(ref this.previewVideoDuration, value);
		}

		private double volume = Config.PreviewVolume;
		public double Volume
		{
			get => this.volume;
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
			get => this.previewPercentComplete;
			set => this.RaiseAndSetIfChanged(ref this.previewPercentComplete, value);
		}

		public int PreviewSeconds
		{
			get => this.previewSeconds;

			set
			{
				this.previewSeconds = value;
				this.RaisePropertyChanged();

				Config.PreviewSeconds = value;
			}
		}

		public int SliderMax
		{
			get
			{
				if (this.PreviewImageService.PreviewCount > 0)
				{
					return this.PreviewImageService.PreviewCount - 1;
				}

				return Config.PreviewCount - 1;
			}
		}

		private ObservableAsPropertyHelper<string> playSourceToolTip;
		public string PlaySourceToolTip => this.playSourceToolTip.Value;

		private ObservableAsPropertyHelper<bool> playAvailable;
		public bool PlayAvailable => this.playAvailable.Value;

		private PreviewDisplay displayType;

		public PreviewDisplay DisplayType
		{
			get => this.displayType;

			set
			{
				if (this.displayType != value)
				{
					this.displayType = value;
					this.RaisePropertyChanged();

					CustomConfig.PreviewDisplay = value;

					DispatchUtilities.BeginInvoke(
						() =>
						{
							this.View?.RefreshImageSize();
						},
						DispatcherPriority.Background);
				}
			}
		}

		private ReactiveCommand<Unit, Unit> generatePreview;
		public ICommand GeneratePreview
		{
			get
			{
				return this.generatePreview ?? (this.generatePreview = ReactiveCommand.Create(
					() =>
					{
						this.job = this.mainViewModel.EncodeJob;
						this.originalScanInstance = this.ScanInstance;

						this.PreviewPercentComplete = 0;
						this.EncodeState = PreviewEncodeState.EncodeStarting;
						this.cancelPending = false;
						this.encodeCanceled = false;

						this.SetPreviewFilePath();

						this.job.FinalOutputPath = this.previewFilePath;

						if (this.job.Subtitles?.SourceSubtitles != null)
						{
							ChosenSourceSubtitle scanTrack = this.job.Subtitles.SourceSubtitles.FirstOrDefault(s => s.TrackNumber == 0);
							if (scanTrack != null)
							{
								this.job.Subtitles.SourceSubtitles.Remove(scanTrack);
							}
						}

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

								if (this.encodeCanceled)
								{
									this.logger.Log("Canceled preview clip generation");
								}
								else
								{
									if (e.Result != VCEncodeResultCode.Succeeded)
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
						this.logger.Log("  Path: " + this.job.FinalOutputPath);
						this.logger.Log("  Title: " + this.job.Title);
						this.logger.Log("  Preview #: " + this.PreviewImageServiceClient.PreviewIndex);

						this.encodeProxy.StartEncodeAsync(this.job, this.logger, true, this.PreviewImageServiceClient.PreviewIndex, this.PreviewSeconds, this.job.Length.TotalSeconds);
					},
					this.PreviewImageService.WhenAnyValue(x => x.HasPreview)));
			}
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

				this.seekBarUpdateTimer = new DispatcherTimer
				{
					Interval = TimeSpan.FromMilliseconds(250)
				};

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
				previewDirectory = CommonUtilities.LocalAppFolder;
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
				previewDirectory = CommonUtilities.LocalAppFolder;
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

		private ReactiveCommand<Unit, Unit> playSource;
		public ICommand PlaySource
		{
			get
			{
				return this.playSource ?? (this.playSource = ReactiveCommand.Create(
					() =>
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
					},
					this.WhenAnyValue(x => x.PlayAvailable)));
			}
		}

		private ReactiveCommand<Unit, Unit> reopenPreview;
		public ICommand ReopenPreview
		{
			get
			{
				return this.reopenPreview ?? (this.reopenPreview = ReactiveCommand.Create(() =>
				{
					this.PlayPreview();
				}));
			}
		}

		private ObservableAsPropertyHelper<bool> previewAvailable;
		public bool PreviewAvailable => this.previewAvailable.Value;

		private ReactiveCommand<Unit, Unit> cancelPreview;
		public ICommand CancelPreview
		{
			get
			{
				return this.cancelPreview ?? (this.cancelPreview = ReactiveCommand.Create(
					() => { this.CancelPreviewImpl(); },
					this.WhenAnyValue(x => x.EncodeState).Select(encodeState => encodeState == PreviewEncodeState.Encoding)));
			}
		}

		private void CancelPreviewImpl()
		{
			this.encodeCanceled = true;
			this.encodeProxy.StopEncodeAsync();
		}

		private ReactiveCommand<Unit, Unit> pause;
		public ICommand Pause
		{
			get
			{
				return this.pause ?? (this.pause = ReactiveCommand.Create(() =>
				{
					this.PreviewPaused = true;
				}));
			}
		}

		private ReactiveCommand<Unit, Unit> play;
		public ICommand Play
		{
			get
			{
				return this.play ?? (this.play = ReactiveCommand.Create(() =>
				{
					this.PreviewPaused = false;
				}));
			}
		}

		private ReactiveCommand<Unit, Unit> openInSystemPlayer;
		public ICommand OpenInSystemPlayer
		{
			get
			{
				return this.openInSystemPlayer ?? (this.openInSystemPlayer = ReactiveCommand.Create(() =>
				{
					this.PlayingPreview = false;
					FileService.Instance.PlayVideo(this.previewFilePath);
				}));
			}
		}

		private ReactiveCommand<Unit, Unit> closeVideo;
		public ICommand CloseVideo
		{
			get
			{
				return this.closeVideo ?? (this.closeVideo = ReactiveCommand.Create(() =>
				{
					this.PlayingPreview = false;
				}));
			}
		}

		private string CreateTitle(OutputSizeInfo size)
		{
			if (size.Par.Num == size.Par.Den)
			{
				return string.Format(PreviewRes.PreviewWindowTitleSimple, size.OutputWidth, size.OutputHeight);
			}
			else
			{
				int width = size.OutputWidth;
				int height = size.OutputHeight;
				int parWidth = size.Par.Num;
				int parHeight = size.Par.Den;

				if (parWidth <= 0 || parHeight <= 0)
				{
					StaticResolver.Resolve<IAppLogger>().LogError("HandBrake returned a negative pixel aspect ratio. Cannot show preview.");
					return PreviewRes.NoVideoSourceTitle;
				}

				return string.Format(
					PreviewRes.PreviewWindowTitleComplex,
					Math.Round(width * ((double)parWidth / parHeight)),
					height,
					width,
					height);
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

		public void OnVideoCompleted()
		{
			this.PlayingPreview = false;
		}

		public void OnVideoFailed()
		{
			this.PlayingPreview = false;
			MessageBoxResult result = StaticResolver.Resolve<IMessageBoxService>().Show(this, PreviewRes.VideoErrorMessage, PreviewRes.VideoErrorTitle, MessageBoxButton.YesNo);
			if (result == MessageBoxResult.Yes)
			{
				FileService.Instance.PlayVideo(this.previewFilePath);
			}
		}
	}
}
