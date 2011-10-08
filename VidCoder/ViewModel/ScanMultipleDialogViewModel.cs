using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HandBrake.Interop;
using System.Threading;
using VidCoder.Services;
using VidCoder.Properties;

namespace VidCoder.ViewModel
{
	/// <summary>
	/// View model for dialog that shows when scanning multiple files.
	/// </summary>
	public class ScanMultipleDialogViewModel : OkCancelDialogViewModel
	{
		private List<EncodeJobViewModel> itemsToScan;
		private int currentJobIndex;
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
				return ((double)this.currentJobIndex * 100.0) / this.itemsToScan.Count;
			}
		}

		public override bool CanClose
		{
			get
			{
				lock (this.currentJobIndexLock)
				{
					return this.currentJobIndex >= this.itemsToScan.Count;
				}
			}
		}

		public void ScanNextJob()
		{
			EncodeJobViewModel jobVM = itemsToScan[currentJobIndex];

			HandBrakeInstance onDemandInstance = new HandBrakeInstance();
			onDemandInstance.Initialize(Settings.Default.LogVerbosity);
			onDemandInstance.ScanCompleted += (o, e) =>
			{
				jobVM.HandBrakeInstance = onDemandInstance;

				if (onDemandInstance.Titles.Count > 0)
				{
					jobVM.Job.Length = onDemandInstance.Titles[0].Duration;
					jobVM.Job.ChapterStart = 1;
					jobVM.Job.ChapterEnd = onDemandInstance.Titles[0].Chapters.Count;
				}

				lock (this.currentJobIndexLock)
				{
					this.currentJobIndex++;
					this.RaisePropertyChanged("Progress");

					if (this.currentJobIndex >= this.itemsToScan.Count)
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

			onDemandInstance.StartScan(jobVM.Job.SourcePath, 10, 1);
		}
	}
}
