using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
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
		private const double SubtitleScanCost = 1 / EncodeJobViewModel.SubtitleScanCostFactor;

		private VCJob job;
		private IEncodeProxy encodeProxy;
		private IAppLogger logger = Ioc.Get<IAppLogger>();
		private string previewFilePath;
		private bool cancelPending;
		private bool encodeCancelled;
		private int previewSeconds;


		private HandBrakeInstance originalScanInstance;

		private DispatcherTimer seekBarUpdateTimer;
		private bool positionSetByNonUser;

		private readonly OutputSizeService outputSizeService = Ioc.Get<OutputSizeService>();
		private MainViewModel mainViewModel = Ioc.Get<MainViewModel>();
		private PreviewUpdateService previewUpdateService = Ioc.Get<PreviewUpdateService>();

		public PreviewWindowViewModel()
		{
			this.previewUpdateService.PreviewInputChanged += this.OnPreviewInputChanged;

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

						return isDvd && !Utilities.IsRunningAsAppx && playerInstalled;
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
						if (Utilities.IsRunningAsAppx)
						{
							return PreviewRes.PlaySourceDisabledAppxToolTip;
						}

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


			this.PlaySource = ReactiveCommand.Create(this.WhenAnyValue(x => x.PlayAvailable));
			this.PlaySource.Subscribe(_ => this.PlaySourceImpl());

			this.ReopenPreview = ReactiveCommand.Create();
			this.ReopenPreview.Subscribe(_ => this.ReopenPreviewImpl());

			this.GeneratePreview = ReactiveCommand.Create(this.PreviewImageService.WhenAnyValue(x => x.HasPreview));
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
				this.StopAndWait();
			}

			return true;
		}

		public MainViewModel MainViewModel
		{
			get { return this.mainViewModel; }
		}

		public PreviewImageService PreviewImageService { get; } = Ioc.Get<PreviewImageService>();

		public PreviewImageServiceClient PreviewImageServiceClient { get; } = new PreviewImageServiceClient();

		public ProcessingService ProcessingService { get; } = Ioc.Get<ProcessingService>();


		public OutputPathService OutputPathService { get; } = Ioc.Get<OutputPathService>();

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
			get { return this.displayType; }

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


		public ReactiveCommand<object> GeneratePreview { get; }
		private void GeneratePreviewImpl()
		{
			this.job = this.mainViewModel.EncodeJob;
			this.originalScanInstance = this.ScanInstance;

			this.PreviewPercentComplete = 0;
			this.EncodeState = PreviewEncodeState.EncodeStarting;
			this.cancelPending = false;
			this.encodeCancelled = false;

			this.SetPreviewFilePath();

			this.job.OutputPath = this.previewFilePath;

			if (this.job.Subtitles?.SourceSubtitles != null)
			{
				SourceSubtitle scanTrack = this.job.Subtitles.SourceSubtitles.FirstOrDefault(s => s.TrackNumber == 0);
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
			this.logger.Log("  Preview #: " + this.PreviewImageServiceClient.PreviewIndex);

			this.encodeProxy.StartEncode(this.job, this.logger, true, this.PreviewImageServiceClient.PreviewIndex, this.PreviewSeconds, this.job.Length.TotalSeconds);
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

		private void StopAndWait()
		{
			this.encodeCancelled = true;
			this.encodeProxy.StopAndWait();
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
					Ioc.Get<IAppLogger>().LogError("HandBrake returned a negative pixel aspect ratio. Cannot show preview.");
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
			MessageBoxResult result = Ioc.Get<IMessageBoxService>().Show(this, PreviewRes.VideoErrorMessage, PreviewRes.VideoErrorTitle, MessageBoxButton.YesNo);
			if (result == MessageBoxResult.Yes)
			{
				FileService.Instance.PlayVideo(this.previewFilePath);
			}
		}
	}
}
