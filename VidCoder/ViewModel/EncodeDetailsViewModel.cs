using System.Diagnostics;

namespace VidCoder.ViewModel
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Linq;
	using System.Text;
	using GalaSoft.MvvmLight;
	using Messages;
	using Resources;

	public class EncodeDetailsViewModel : OkCancelDialogViewModel
	{
		public EncodeDetailsViewModel()
		{
			MessengerInstance.Register<ProgressChangedMessage>(this,
				m =>
				{
					if (m.Encoding)
					{
						this.OverallProgressPercent = m.OverallProgressFraction * 100;
						this.TaskNumberDisplay = m.TaskNumber + "/" + m.TotalTasks;
						this.OverallElapsedTime = Utilities.FormatTimeSpan(m.OverallElapsedTime);
						this.OverallEta = Utilities.FormatTimeSpan(m.OverallEta);
						this.FileName = m.FileName;
						this.FileProgressPercent = m.FileProgressFraction * 100;
						this.FileElapsedTime = Utilities.FormatTimeSpan(m.FileElapsedTime);
						this.FileEta = Utilities.FormatTimeSpan(m.FileEta);
						this.CurrentFps = m.CurrentFps;
						this.AverageFps = m.AverageFps;
						this.FileSize = Utilities.FormatFileSize(m.FileSizeBytes);

						this.ShowPassProgress = m.HasScanPass || m.TwoPass;
						if (this.ShowPassProgress)
						{
							this.PassProgressPercent = m.PassProgressFraction * 100;
							Debug.WriteLine("Pass id in encode details: " + m.CurrentPassId);
							switch (m.CurrentPassId)
							{
								case -1:
									this.PassProgressLabel = EncodeDetailsRes.ScanPassLabel;
									break;
								case 0:
									this.PassProgressLabel = EncodeDetailsRes.EncodePassLabel;
									break;
								case 1:
									this.PassProgressLabel = EncodeDetailsRes.FirstPassLabel;
									break;
								case 2:
									this.PassProgressLabel = EncodeDetailsRes.SecondPassLabel;
									break;
							}
						}
					}
				});
		}

		private double overallProgressPercent;
		public double OverallProgressPercent
		{
			get
			{
				return this.overallProgressPercent;
			}

			set
			{
				this.overallProgressPercent = value;
				this.RaisePropertyChanged(() => this.OverallProgressPercent);
			}
		}

		private string taskNumberDisplay;
		public string TaskNumberDisplay
		{
			get
			{
				return this.taskNumberDisplay;
			}

			set
			{
				this.taskNumberDisplay = value;
				this.RaisePropertyChanged(() => this.TaskNumberDisplay);
			}
		}

		private string overallElapsedTime;
		public string OverallElapsedTime
		{
			get
			{
				return this.overallElapsedTime;
			}

			set
			{
				this.overallElapsedTime = value;
				this.RaisePropertyChanged(() => this.OverallElapsedTime);
			}
		}

		private string overallEta;
		public string OverallEta
		{
			get
			{
				return this.overallEta;
			}

			set
			{
				this.overallEta = value;
				this.RaisePropertyChanged(() => this.OverallEta);
			}
		}

		private string fileName;
		public string FileName
		{
			get
			{
				return this.fileName;
			}

			set
			{
				this.fileName = value;
				this.RaisePropertyChanged(() => this.FileName);
			}
		}

		private double fileProgressPercent;
		public double FileProgressPercent
		{
			get
			{
				return this.fileProgressPercent;
			}

			set
			{
				this.fileProgressPercent = value;
				this.RaisePropertyChanged(() => this.FileProgressPercent);
			}
		}

		private string fileSize;
		public string FileSize
		{
			get
			{
				return this.fileSize;
			}

			set
			{
				this.fileSize = value;
				this.RaisePropertyChanged(() => this.FileSize);
			}
		}

		private string fileElapsedTime;
		public string FileElapsedTime
		{
			get
			{
				return this.fileElapsedTime;
			}

			set
			{
				this.fileElapsedTime = value;
				this.RaisePropertyChanged(() => this.FileElapsedTime);
			}
		}

		private string fileEta;
		public string FileEta
		{
			get
			{
				return this.fileEta;
			}

			set
			{
				this.fileEta = value;
				this.RaisePropertyChanged(() => this.FileEta);
			}
		}

		private double currentFps;
		public double CurrentFps
		{
			get
			{
				return this.currentFps;
			}

			set
			{
				this.currentFps = value;
				this.RaisePropertyChanged(() => this.CurrentFps);
			}
		}

		private double averageFps;
		public double AverageFps
		{
			get
			{
				return this.averageFps;
			}

			set
			{
				this.averageFps = value;
				this.RaisePropertyChanged(() => this.AverageFps);
			}
		}

		private bool showPassProgress;
		public bool ShowPassProgress
		{
			get
			{
				return this.showPassProgress;
			}

			set
			{
				this.showPassProgress = value;
				this.RaisePropertyChanged(() => this.ShowPassProgress);
			}
		}

		private string passProgressLabel;
		public string PassProgressLabel
		{
			get
			{
				return this.passProgressLabel;
			}

			set
			{
				this.passProgressLabel = value;
				this.RaisePropertyChanged(() => this.PassProgressLabel);
			}
		}

		private double passProgressPercent;
		public double PassProgressPercent
		{
			get
			{
				return this.passProgressPercent;
			}

			set
			{
				this.passProgressPercent = value;
				this.RaisePropertyChanged(() => this.PassProgressPercent);
			}
		}

		// Should only fire when user manually closes the window, not when closed through WindowManager.
		public override void OnClosing()
		{
			// Setting this config value will prevent the details window from appearing on the next encode.
			Config.EncodeDetailsWindowOpen = false;
			base.OnClosing();
		}
	}
}
