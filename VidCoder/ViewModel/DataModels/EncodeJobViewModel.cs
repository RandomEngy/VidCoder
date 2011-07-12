using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HandBrake.Interop.Model;
using System.Windows.Input;
using HandBrake.Interop.Model.Encoding;
using VidCoder.DragDropUtils;
using HandBrake.Interop;
using System.Diagnostics;
using Microsoft.Practices.Unity;

namespace VidCoder.ViewModel
{
	public class EncodeJobViewModel : ViewModelBase, IDragItem
	{
		public const double SubtitleScanCostFactor = 80.0;

		private MainViewModel mainViewModel = Unity.Container.Resolve<MainViewModel>();

		private bool isPaused;
		private EncodeJob job;
		private bool encoding;
		private int percentComplete;
		private bool isOnlyItem;
		private Stopwatch encodeTimeStopwatch;
		private TimeSpan eta;

		private ICommand removeQueueJobCommand;

		public EncodeJobViewModel(EncodeJob job)
		{
			this.job = job;
		}

		public EncodeJob Job
		{
			get
			{
				return this.job;
			}
		}

		public EncodingProfile Profile
		{
			get
			{
				return this.Job.EncodingProfile;
			}
		}

		public HandBrakeInstance HandBrakeInstance { get; set; }

		public bool Encoding
		{
			get
			{
				return this.encoding;
			}

			set
			{
				this.encoding = value;
				this.NotifyPropertyChanged("Encoding");
			}
		}

		public System.Windows.Media.Brush ProgressBarColor
		{
			get
			{
				if (this.isPaused)
				{
					return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 230, 0));
				}
				else
				{
					return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 200, 0));
				}
			}
		}

		public bool IsOnlyItem
		{
			get
			{
				return this.isOnlyItem;
			}

			set
			{
				this.isOnlyItem = value;
				this.NotifyPropertyChanged("ShowProgressBar");
			}
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

				if (this.Job.EncodingProfile.TwoPass)
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

		public bool ShowRemoveButton
		{
			get
			{
				return !this.encoding;
			}
		}

		public bool ShowProgressBar
		{
			get
			{
				return this.encoding && !this.IsOnlyItem;
			}
		}

		public TimeSpan Eta
		{
			get
			{
				return this.eta;
			}

			set
			{
				this.eta = value;
				this.NotifyPropertyChanged("Eta");
				this.NotifyPropertyChanged("ProgressToolTip");
			}
		}

		public string ProgressToolTip
		{
			get
			{
				if (this.Eta == TimeSpan.Zero)
				{
					return null;
				}

				return "Job ETA: " + Utilities.FormatTimeSpan(this.Eta);
			}
		}

		public int PercentComplete
		{
			get
			{
				return this.percentComplete;
			}

			set
			{
				this.percentComplete = value;
				this.NotifyPropertyChanged("PercentComplete");
			}
		}

		public string ChaptersDisplay
		{
			get
			{
				if (this.job.ChapterStart == this.job.ChapterEnd)
				{
					return this.job.ChapterStart.ToString();
				}
				else
				{
					return this.job.ChapterStart + " - " + this.job.ChapterEnd;
				}
			}
		}

		public string AudioEncodersDisplay
		{
			get
			{
				var encodingParts = new List<string>();
				foreach (AudioEncoding audioEncoding in this.Profile.AudioEncodings)
				{
					encodingParts.Add(DisplayConversions.DisplayAudioEncoder(audioEncoding.Encoder));
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
					case VideoEncodeRateType.AverageBitrate:
						return this.Profile.VideoBitrate + " kbps";
					case VideoEncodeRateType.TargetSize:
						return this.Profile.TargetSize + " MB";
					case VideoEncodeRateType.ConstantQuality:
						return "CQ " + this.Profile.Quality;
					default:
						break;
				}

				return string.Empty;
			}
		}

		public string AudioBitrateDisplay
		{
			get
			{
				var bitrateParts = new List<string>();
				foreach (AudioEncoding audioEncoding in this.Profile.AudioEncodings)
				{
					if (audioEncoding.Encoder != AudioEncoder.Ac3Passthrough && audioEncoding.Encoder != AudioEncoder.DtsPassthrough)
					{
						bitrateParts.Add(audioEncoding.Bitrate.ToString());
					}
				}

				return string.Join(", ", bitrateParts);
			}
		}

		public ICommand RemoveQueueJobCommand
		{
			get
			{
				if (this.removeQueueJobCommand == null)
				{
					this.removeQueueJobCommand = new RelayCommand(param =>
					{
						this.mainViewModel.RemoveQueueJob(this);
					},
					param =>
					{
						return !this.Encoding;
					});
				}

				return this.removeQueueJobCommand;
			}
		}

		public bool CanDrag
		{
			get
			{
				return !this.Encoding;
			}
		}

		public void ReportEncodeStart(bool isOnlyItem)
		{
			this.Encoding = true;
			this.IsOnlyItem = isOnlyItem;
			this.encodeTimeStopwatch = Stopwatch.StartNew();
			this.NotifyPropertyChanged("ShowRemoveButton");
			this.NotifyPropertyChanged("ShowProgressBar");
		}

		public void ReportEncodePause()
		{
			this.isPaused = true;
			this.encodeTimeStopwatch.Stop();
			this.NotifyPropertyChanged("ProgressBarColor");
		}

		public void ReportEncodeResume()
		{
			this.isPaused = false;
			this.encodeTimeStopwatch.Start();
			this.NotifyPropertyChanged("ProgressBarColor");
		}

		public void ReportEncodeEnd()
		{
			this.Encoding = false;
			this.encodeTimeStopwatch.Stop();
			this.NotifyPropertyChanged("ShowRemoveButton");
			this.NotifyPropertyChanged("ShowProgressBar");
		}
	}
}
