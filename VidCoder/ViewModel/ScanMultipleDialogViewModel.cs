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

			var onDemandInstance = new HandBrakeInstance();
			onDemandInstance.Initialize(Config.LogVerbosity);
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
					this.RaisePropertyChanged(() => this.Progress);

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

			onDemandInstance.StartScan(jobVM.Job.SourcePath, 10, 0);
		}
	}
}
