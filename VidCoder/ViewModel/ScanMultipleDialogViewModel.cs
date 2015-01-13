using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HandBrake.Interop;
using VidCoder.Services;
using VidCoder.ViewModel.Components;

namespace VidCoder.ViewModel
{
	using HandBrake.Interop.SourceData;

	/// <summary>
	/// View model for dialog that shows when scanning multiple files.
	/// </summary>
	public class ScanMultipleDialogViewModel : OkCancelDialogViewModel
	{
		private IList<string> pathsToScan;
		private int currentJobIndex;
		private float currentJobProgress;
		private object currentJobIndexLock = new object();

		public ScanMultipleDialogViewModel(IList<string> pathsToScan)
		{
			this.pathsToScan = pathsToScan;
			this.ScanResults = new List<HandBrakeInstance>();
			this.ScanNextJob();
		}

		public double Progress
		{
			get
			{
				return ((this.currentJobIndex + this.currentJobProgress) * 100.0) / this.pathsToScan.Count;
			}
		}

		public bool CancelPending { get; set; }

		public bool ScanFinished
		{
			get
			{
				lock (this.currentJobIndexLock)
				{
					return this.currentJobIndex >= this.pathsToScan.Count || this.CancelPending;
				}
			}
		}

		public List<HandBrakeInstance> ScanResults { get; private set; }

		public void ScanNextJob()
		{
			string path = pathsToScan[currentJobIndex];

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
					this.ScanResults.Add(onDemandInstance);
					//jobVM.HandBrakeInstance = onDemandInstance;

					//if (onDemandInstance.Titles.Count > 0)
					//{
					//	Title titleToEncode = Utilities.GetFeatureTitle(onDemandInstance.Titles, onDemandInstance.FeatureTitle);

					//	jobVM.Job.Title = titleToEncode.TitleNumber;
					//	jobVM.Job.Length = titleToEncode.Duration;
					//	jobVM.Job.ChapterStart = 1;
					//	jobVM.Job.ChapterEnd = titleToEncode.Chapters.Count;
					//}

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

			onDemandInstance.StartScan(path, Config.PreviewCount, 0);
		}
	}
}
