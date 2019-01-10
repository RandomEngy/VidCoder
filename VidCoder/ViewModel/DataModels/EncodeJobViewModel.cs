using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Windows.Input;
using HandBrake.Interop.Interop;
using Microsoft.AnyContainer;
using ReactiveUI;
using VidCoder.DragDropUtils;
using VidCoder.Model;
using VidCoder.Resources;
using VidCoder.Services;
using VidCoderCommon.Model;

namespace VidCoder.ViewModel
{
	public class EncodeJobViewModel : ReactiveObject, IDragItem
	{
		/// <summary>
		/// Divide a normal pass cost by this factor to get the cost to do a subtitle scan.
		/// </summary>
		public const double SubtitleScanCostFactor = 5.0;

		private MainViewModel main = StaticResolver.Resolve<MainViewModel>();
		private ProcessingService processingService;

		private VCJob job;
		private Stopwatch encodeTimeStopwatch;

		public EncodeJobViewModel(VCJob job)
		{
			this.job = job;

			// ShowProgressBar
			Observable.CombineLatest(
				this.WhenAnyValue(x => x.Encoding),
				this.ProcessingService.QueueCountObservable,
				(encoding, queueCount) =>
				{
					return encoding && queueCount != 1;
				}).ToProperty(this, x => x.ShowProgressBar, out this.showProgressBar);

			// ProgressToolTip
			this.WhenAnyValue(x => x.Eta, eta =>
			{
				if (eta == TimeSpan.Zero)
				{
					return null;
				}

				return "Job ETA: " + Utilities.FormatTimeSpan(eta);
			}).ToProperty(this, x => x.ProgressToolTip, out this.progressToolTip);

			// ShowQueueEditButtons
			this.WhenAnyValue(x => x.Encoding, encoding => !encoding)
				.ToProperty(this, x => x.ShowQueueEditButtons, out this.showQueueEditButtons);

			// PercentComplete
			this.WhenAnyValue(x => x.FractionComplete).Select(fractionComplete =>
			{
				return this.fractionComplete * 100.0;
			}).ToProperty(this, x => x.PercentComplete, out this.percentComplete);

			// PassProgressPercent
			this.WhenAnyValue(x => x.PassProgressFraction).Select(passProgressFraction =>
			{
				return this.passProgressFraction * 100.0;
			}).ToProperty(this, x => x.PassProgressPercent, out this.passProgressPercent);

			// FileSizeDisplay
			this.WhenAnyValue(x => x.FileSizeBytes).Select(fileSizeBytes =>
			{
				return Utilities.FormatFileSize(fileSizeBytes);
			}).ToProperty(this, x => x.FileSizeDisplay, out this.fileSizeDisplay);

			// EncodeTimeDisplay
			this.WhenAnyValue(x => x.EncodeTime).Select(encodeTime =>
			{
				return Utilities.FormatTimeSpan(encodeTime);
			}).ToProperty(this, x => x.EncodeTimeDisplay, out this.encodeTimeDisplay);

			// EtaDisplay
			this.WhenAnyValue(x => x.Eta).Select(eta =>
			{
				return Utilities.FormatTimeSpan(eta);
			}).ToProperty(this, x => x.EtaDisplay, out this.etaDisplay);

			// PassProgressDisplay
			this.WhenAnyValue(x => x.CurrentPassId).Select(passId =>
			{
				switch (passId)
				{
					case -1:
						return EncodeDetailsRes.ScanPassLabel;
					case 0:
						return EncodeDetailsRes.EncodePassLabel;
					case 1:
						return EncodeDetailsRes.FirstPassLabel;
					case 2:
						return EncodeDetailsRes.SecondPassLabel;
					default:
						return null;
				}
			}).ToProperty(this, x => x.PassProgressDisplay, out this.passProgressDisplay);
		}

		private bool initializedForEncoding = false;
		private void InitializeForEncoding()
		{
			if (!this.initializedForEncoding)
			{
				// ShowFps
				this.ProcessingService.EncodingJobsCountObservable.Select(encodingJobCount =>
				{
					return encodingJobCount > 1;
				}).ToProperty(this, x => x.ShowFps, out this.showFps);

				this.initializedForEncoding = true;
			}
		}

		public VCJob Job
		{
			get
			{
				return this.job;
			}
		}

		public VCProfile Profile
		{
			get
			{
				return this.Job.EncodingProfile;
			}
		}

		// Set when the job should be overridden with the given JSON.
		public string DebugEncodeJsonOverride { get; set; }

		public MainViewModel MainViewModel
		{
			get
			{
				return this.main;
			}
		}

		public ProcessingService ProcessingService
		{
			get
			{
				if (this.processingService == null)
				{
					this.processingService = StaticResolver.Resolve<ProcessingService>();
				}

				return this.processingService;
			}
		}

		public IAppLogger Logger { get; set; }

		public VideoSource VideoSource { get; set; }

		public VideoSourceMetadata VideoSourceMetadata { get; set; }

		// The parent folder for the item (if it was inside a folder of files added in a batch)
		public string SourceParentFolder { get; set; }

		// True if the output path was picked manually rather than auto-generated
		public bool ManualOutputPath { get; set; }

		public string NameFormatOverride { get; set; }

		public string PresetName { get; set; }

		public string ShortFileName => Path.GetFileName(this.Job.FinalOutputPath);

		public IEncodeProxy EncodeProxy { get; set; }

		private bool isSelected;
		public bool IsSelected
		{
			get { return this.isSelected; }
			set { this.RaiseAndSetIfChanged(ref this.isSelected, value); }
		}

		private bool encoding;
		public bool Encoding
		{
			get { return this.encoding; }
			set { this.RaiseAndSetIfChanged(ref this.encoding, value); }
		}

		private bool isPaused;
		public bool IsPaused
		{
			get { return this.isPaused; }
			private set { this.RaiseAndSetIfChanged(ref this.isPaused, value); }
		}

		/// <summary>
		/// Returns true if a subtitle scan will be performed on this job.
		/// </summary>
		public bool SubtitleScan
		{
			get
			{
				return this.Job.Subtitles?.SourceSubtitles?.Count(item => item.TrackNumber == 0) > 0;
			}
		}

		private JobWork work;
		public JobWork Work
		{
			get
			{
				if (this.work == null)
				{
					this.CalculateWork();
				}

				return this.work;
			}
		}

		public void CalculateWork()
		{
			if (this.Job == null)
			{
				throw new InvalidOperationException("Job cannot be null.");
			}

			if (this.Job.Length <= TimeSpan.Zero)
			{
				this.Logger.LogError($"Invalid length '{this.Job.Length}' on job {this.Job.FinalOutputPath}");
			}

			var profile = this.Job.EncodingProfile;
			if (profile == null)
			{
				throw new InvalidOperationException("Encoding profile cannot be null.");
			}

			this.work = new JobWork(this.Job.Length, profile.VideoEncodeRateType != VCVideoEncodeRateType.ConstantQuality && profile.TwoPass, this.SubtitleScan);
		}

		public TimeSpan EncodeTime
		{
			get
			{
				if (this.encodeTimeStopwatch == null)
				{
					return TimeSpan.Zero;
				}

				return this.encodeTimeStopwatch.Elapsed;
			}
		}

		private readonly ObservableAsPropertyHelper<string> encodeTimeDisplay;
		public string EncodeTimeDisplay => this.encodeTimeDisplay.Value;

		public void RefreshEncodeTimeDisplay()
		{
			this.RaisePropertyChanged(nameof(this.EncodeTime));
		}

		private readonly ObservableAsPropertyHelper<bool> showQueueEditButtons;
		public bool ShowQueueEditButtons => this.showQueueEditButtons.Value;

		private readonly ObservableAsPropertyHelper<bool> showProgressBar;
		public bool ShowProgressBar => this.showProgressBar.Value;

		private TimeSpan eta;
		public TimeSpan Eta
		{
			get { return this.eta; }
			set { this.RaiseAndSetIfChanged(ref this.eta, value); }
		}

		private readonly ObservableAsPropertyHelper<string> etaDisplay;
		public string EtaDisplay => this.etaDisplay.Value;

		private readonly ObservableAsPropertyHelper<string> progressToolTip;
		public string ProgressToolTip => this.progressToolTip.Value;

		private double fractionComplete;
		public double FractionComplete
		{
			get { return this.fractionComplete; }
			set { this.RaiseAndSetIfChanged(ref this.fractionComplete, value); }
		}

		private readonly ObservableAsPropertyHelper<double> percentComplete;
		public double PercentComplete => this.percentComplete.Value;

		private int currentPassId;
		public int CurrentPassId
		{
			get { return this.currentPassId; }
			set { this.RaiseAndSetIfChanged(ref this.currentPassId, value); }
		}

		private readonly ObservableAsPropertyHelper<string> passProgressDisplay;
		public string PassProgressDisplay => this.passProgressDisplay.Value;

		private double passProgressFraction;
		public double PassProgressFraction
		{
			get { return this.passProgressFraction; }
			set { this.RaiseAndSetIfChanged(ref this.passProgressFraction, value); }
		}

		private readonly ObservableAsPropertyHelper<double> passProgressPercent;
		public double PassProgressPercent => this.passProgressPercent.Value;

		public bool ShowPassProgress => this.SubtitleScan || this.Profile.VideoEncodeRateType != VCVideoEncodeRateType.ConstantQuality;

		private long fileSizeBytes;
		public long FileSizeBytes
		{
			get { return this.fileSizeBytes; }
			set { this.RaiseAndSetIfChanged(ref this.fileSizeBytes, value); }
		}

		private readonly ObservableAsPropertyHelper<string> fileSizeDisplay;
		public string FileSizeDisplay => this.fileSizeDisplay.Value;

		public bool EncodeSpeedDetailsAvailable { get; set; }

		private ObservableAsPropertyHelper<bool> showFps;
		public bool ShowFps => this.showFps.Value;

		private double currentFps;
		public double CurrentFps
		{
			get { return this.currentFps; }
			set { this.RaiseAndSetIfChanged(ref this.currentFps, value); }
		}

		private double averageFps;
		public double AverageFps
		{
			get { return this.averageFps; }
			set { this.RaiseAndSetIfChanged(ref this.averageFps, value); }
		}

		public string RangeDisplay
		{
			get
			{
				switch (this.job.RangeType)
				{
					case VideoRangeType.All:
						return MainRes.QueueRangeAll;
					case VideoRangeType.Chapters:
						int startChapter = this.job.ChapterStart;
						int endChapter = this.job.ChapterEnd;

						string chaptersString;
						if (startChapter == endChapter)
						{
							chaptersString = startChapter.ToString(CultureInfo.CurrentCulture);
						}
						else
						{
							chaptersString = startChapter + " - " + endChapter;
						}

						return string.Format(MainRes.QueueFormat_Chapters, chaptersString);
					case VideoRangeType.Seconds:
						return TimeSpan.FromSeconds(this.job.SecondsStart).ToString("g") + " - " + TimeSpan.FromSeconds(this.job.SecondsEnd).ToString("g");
					case VideoRangeType.Frames:
						return string.Format(MainRes.QueueFormat_Frames, this.job.FramesStart, this.job.FramesEnd);
					default:
						throw new ArgumentOutOfRangeException();
				}
			}
		}

		public string VideoEncoderDisplay
		{
			get
			{
				return HandBrakeEncoderHelpers.GetVideoEncoder(this.Profile.VideoEncoder).DisplayName;
			}
		}

		public string AudioEncodersDisplay
		{
			get
			{
				var encodingParts = new List<string>();
				foreach (AudioEncoding audioEncoding in this.Profile.AudioEncodings)
				{
					encodingParts.Add(HandBrakeEncoderHelpers.GetAudioEncoder(audioEncoding.Encoder).DisplayName);
				}

				return string.Join(", ", encodingParts);
			}
		}

		public string VideoQualityDisplay
		{
			get
			{
				switch (this.Profile.VideoEncodeRateType)
				{
					case VCVideoEncodeRateType.AverageBitrate:
						return this.Profile.VideoBitrate + " kbps";
                    case VCVideoEncodeRateType.TargetSize:
						return this.Profile.TargetSize + " MB";
                    case VCVideoEncodeRateType.ConstantQuality:
						return "CQ " + this.Profile.Quality;
					default:
						break;
				}

				return string.Empty;
			}
		}

		public string DurationDisplay
		{
			get
			{
				TimeSpan length = this.Job.Length;

				return string.Format("{0}:{1:00}:{2:00}", Math.Floor(length.TotalHours), length.Minutes, length.Seconds);
			}
		}

		public string AudioQualityDisplay
		{
			get
			{
				var bitrateParts = new List<string>();
				foreach (AudioEncoding audioEncoding in this.Profile.AudioEncodings)
				{
					if (!HandBrakeEncoderHelpers.GetAudioEncoder(audioEncoding.Encoder).IsPassthrough)
					{
						if (audioEncoding.EncodeRateType == AudioEncodeRateType.Bitrate)
						{
							if (audioEncoding.Bitrate == 0)
							{
								bitrateParts.Add(MainRes.CbrAuto);
							}
							else
							{
								bitrateParts.Add(audioEncoding.Bitrate + " kbps");
							}
						}
						else
						{
							bitrateParts.Add("CQ " + audioEncoding.Quality);
						}
					}
				}

				return string.Join(", ", bitrateParts);
			}
		}

		private ReactiveCommand<Unit, Unit> editQueueJob;
		public ICommand EditQueueJob
		{
			get
			{
				return this.editQueueJob ?? (this.editQueueJob = ReactiveCommand.Create(
					() =>
					{
						this.main.EditJob(this);
					},
					this.WhenAnyValue(
						x => x.Encoding,
						x => x.MainViewModel.VideoSourceState,
						(isEncoding, videoSourceState) =>
						{
							return !isEncoding && videoSourceState != VideoSourceState.Scanning;
						})));
			}
		}

		private ReactiveCommand<Unit, Unit> removeQueueJob;
		public ICommand RemoveQueueJob
		{
			get
			{
				return this.removeQueueJob ?? (this.removeQueueJob = ReactiveCommand.Create(
					() =>
					{
						this.ProcessingService.RemoveQueueJobAndOthersIfSelected(this);
					},
					this.WhenAnyValue(x => x.Encoding, encoding =>
					{
						return !encoding;
					})));
			}
		}

		private ReactiveCommand<Unit, Unit> openSourceFolder;
		public ICommand OpenSourceFolder
		{
			get
			{
				return this.openSourceFolder ?? (this.openSourceFolder = ReactiveCommand.Create(() =>
				{
					try
					{
						if (FileUtilities.IsDirectory(this.Job.SourcePath))
						{
							Process.Start(this.Job.SourcePath);
						}
						else
						{
							FileUtilities.OpenFolderAndSelectItem(this.Job.SourcePath);
						}
					}
					catch (Exception exception)
					{
						this.Logger.LogError("Could not open folder:" + Environment.NewLine + exception);
					}
				}));
			}
		}

		public bool CanDrag
		{
			get
			{
				return !this.Encoding;
			}
		}

		public void ReportEncodeStart()
		{
			this.InitializeForEncoding();
			this.Encoding = true;
			this.encodeTimeStopwatch = Stopwatch.StartNew();
		}

		public void ReportEncodePause()
		{
			this.IsPaused = true;
			this.encodeTimeStopwatch.Stop();
		}

		public void ReportEncodeResume()
		{
			this.IsPaused = false;
			this.encodeTimeStopwatch.Start();
		}

		public void ReportEncodeEnd()
		{
			this.Encoding = false;
			this.encodeTimeStopwatch?.Stop();
		}

		public override string ToString()
		{
			return this.Job.FinalOutputPath;
		}
	}
}
