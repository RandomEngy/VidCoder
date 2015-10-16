using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;
using HandBrake.ApplicationServices.Interop;
using ReactiveUI;
using VidCoder.DragDropUtils;
using VidCoder.Messages;
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

		private MainViewModel main = Ioc.Get<MainViewModel>();
		private ProcessingService processingService;

		private VCJob job;
		private Stopwatch encodeTimeStopwatch;

		public EncodeJobViewModel(VCJob job)
		{
			this.job = job;

			// ShowProgressBar
			this.WhenAnyValue(x => x.Encoding, x => x.IsOnlyItem, (encoding, isOnlyItem) =>
			{
				return encoding && !isOnlyItem;
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

			// ProgressBarColor
			this.WhenAnyValue(x => x.IsPaused, isPaused =>
			{
				if (isPaused)
				{
					return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 230, 0));
				}
				else
				{
					return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 200, 0));
				}
			}).ToProperty(this, x => x.ProgressBarColor, out this.progressBarColor);

			this.EditQueueJob = ReactiveCommand.Create(this.WhenAnyValue(
				x => x.Encoding, 
				x => x.MainViewModel.ScanningSource, 
				(isEncoding, scanningSource) =>
				{
					return !isEncoding && !scanningSource;
				}));
			this.EditQueueJob.Subscribe(_ => this.EditQueueJobImpl());

			this.RemoveQueueJob = ReactiveCommand.Create(this.WhenAnyValue(x => x.Encoding, encoding =>
			{
				return !encoding;
			}));
			this.RemoveQueueJob.Subscribe(_ => this.RemoveQueueJobImpl());
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
					this.processingService = Ioc.Get<ProcessingService>();
				}

				return this.processingService;
			}
		}

		public ILogger Logger { get; set; }

		public VideoSource VideoSource { get; set; }

		public VideoSourceMetadata VideoSourceMetadata { get; set; }

		// The parent folder for the item (if it was inside a folder of files added in a batch)
		public string SourceParentFolder { get; set; }

		// True if the output path was picked manually rather than auto-generated
		public bool ManualOutputPath { get; set; }

		public string NameFormatOverride { get; set; }

		public string PresetName { get; set; }

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

		private ObservableAsPropertyHelper<System.Windows.Media.Brush> progressBarColor;
		public System.Windows.Media.Brush ProgressBarColor
		{
			get { return this.progressBarColor.Value; }
		}

		private bool isOnlyItem;
		public bool IsOnlyItem
		{
			get { return this.isOnlyItem; }
			set { this.RaiseAndSetIfChanged(ref this.isOnlyItem, value); }
		}

		/// <summary>
		/// Returns true if a subtitle scan will be performed on this job.
		/// </summary>
		public bool SubtitleScan
		{
			get
			{
				if (this.Job.Subtitles == null)
				{
					return false;
				}

				if (this.Job.Subtitles.SourceSubtitles == null)
				{
					return false;
				}

				return this.Job.Subtitles.SourceSubtitles.Count(item => item.TrackNumber == 0) > 0;
			}
		}

		/// <summary>
		/// Gets the job cost. Cost is roughly proportional to the amount of time it takes to encode 1 second
		/// of video.
		/// </summary>
		public double Cost
		{
			get
			{
				double cost = this.Job.Length.TotalSeconds;

                if (this.Job.EncodingProfile.VideoEncodeRateType != VCVideoEncodeRateType.ConstantQuality && this.Job.EncodingProfile.TwoPass)
				{
					cost += this.Job.Length.TotalSeconds;
				}

				if (this.SubtitleScan)
				{
					cost += this.Job.Length.TotalSeconds / SubtitleScanCostFactor;
				}

				return cost;
			}
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

		private ObservableAsPropertyHelper<bool> showQueueEditButtons;
		public bool ShowQueueEditButtons
		{
			get { return this.showQueueEditButtons.Value; }
		}

		private ObservableAsPropertyHelper<bool> showProgressBar;
		public bool ShowProgressBar
		{
			get { return this.showProgressBar.Value; }
		}

		private TimeSpan eta;
		public TimeSpan Eta
		{
			get { return this.eta; }
			set { this.RaiseAndSetIfChanged(ref this.eta, value); }
		}

		private ObservableAsPropertyHelper<string> progressToolTip;
		public string ProgressToolTip
		{
			get { return this.progressToolTip.Value; }
		}

		private int percentComplete;
		public int PercentComplete
		{
			get { return this.percentComplete; }
			set { this.RaiseAndSetIfChanged(ref this.percentComplete, value); }
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

		public ReactiveCommand<object> EditQueueJob { get; }
		private void EditQueueJobImpl()
		{
			this.main.EditJob(this);
		}

		public ReactiveCommand<object> RemoveQueueJob { get; }
		private void RemoveQueueJobImpl()
		{
			this.ProcessingService.RemoveQueueJob(this);
		}

		public bool CanDrag
		{
			get
			{
				return !this.Encoding;
			}
		}

		public void ReportEncodeStart(bool newIsOnlyItem)
		{
			this.Encoding = true;
			this.IsOnlyItem = newIsOnlyItem;
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
			this.encodeTimeStopwatch.Stop();
		}
	}
}
