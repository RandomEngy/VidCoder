using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HandBrake.Interop;
using VidCoder.Services;

namespace VidCoder.ViewModel
{
	using HandBrake.Interop.SourceData;

	/// <summary>
	/// View model for dialog that shows when scanning multiple files.
	/// </summary>
	public class ScanMultipleDialogViewModel : OkCancelDialogViewModel
	{
		private List<EncodeJobViewModel> itemsToScan;
		private int currentJobIndex;
		private float currentJobProgress;
		private object currentJobIndexLock = new object();

		public ScanMultipleDialogViewModel(List<EncodeJobViewModel> itemsToScan)
		{
			this.itemsToScan = itemsToScan;
			this.ScanNextJob();
		}

		public double Progress
		{
			get
			{
				return ((this.currentJobIndex + this.currentJobProgress) * 100.0) / this.itemsToScan.Count;
			}
		}

		public bool CancelPending { get; set; }

		public bool ScanFinished
		{
			get
			{
				lock (this.currentJobIndexLock)
				{
					return this.currentJobIndex >= this.itemsToScan.Count || this.CancelPending;
				}
			}
		}

		public void ScanNextJob()
		{
			EncodeJobViewModel jobVM = itemsToScan[currentJobIndex];

			var onDemandInstance = new HandBrakeInstance();
			onDemandInstance.Initialize(Config.LogVerbosity);
			onDemandInstance.ScanProgress += (o, e) =>
				{
					lock (this.currentJobIndexLock)
					{
						this.currentJobProgress = e.Progress;
						this.RaisePropertyChanged(() => this.Progress);
					}
				};
			onDemandInstance.ScanCompleted += (o, e) =>
				{
					jobVM.HandBrakeInstance = onDemandInstance;

					if (onDemandInstance.Titles.Count > 0)
					{
						Title titleToEncode = Utilities.GetFeatureTitle(onDemandInstance.Titles, onDemandInstance.FeatureTitle);

						jobVM.Job.Title = titleToEncode.TitleNumber;
						jobVM.Job.Length = titleToEncode.Duration;
						jobVM.Job.ChapterStart = 1;
						jobVM.Job.ChapterEnd = titleToEncode.Chapters.Count;
					}

					lock (this.currentJobIndexLock)
					{
						this.currentJobIndex++;
						this.currentJobProgress = 0;
						this.RaisePropertyChanged(() => this.Progress);

						if (this.ScanFinished)
						{
							DispatchService.BeginInvoke(() =>
							{
								this.CancelCommand.Execute(null);
							});
						}
						else
						{
							this.ScanNextJob();
						}
					}
				};

			onDemandInstance.StartScan(jobVM.Job.SourcePath, Config.PreviewCount, 0);
		}
	}
}
