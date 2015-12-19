using System;
using System.Collections.Generic;
using HandBrake.ApplicationServices.Interop;
using VidCoder.Extensions;
using VidCoder.Model;
using ReactiveUI;

namespace VidCoder.ViewModel
{
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
			this.ScanResults = new List<VideoSource>();
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

		public List<VideoSource> ScanResults { get; private set; }

		public void ScanNextJob()
		{
			string path = this.pathsToScan[this.currentJobIndex];

			var onDemandInstance = new HandBrakeInstance();
			onDemandInstance.Initialize(Config.LogVerbosity);
			onDemandInstance.ScanProgress += (o, e) =>
				{
					lock (this.currentJobIndexLock)
					{
						this.currentJobProgress = (float)e.Progress;
						this.RaisePropertyChanged(nameof(this.Progress));
					}
				};
			onDemandInstance.ScanCompleted += (o, e) =>
				{
					this.ScanResults.Add(onDemandInstance.Titles.ToVideoSource());
					onDemandInstance.Dispose();

					lock (this.currentJobIndexLock)
					{
						this.currentJobIndex++;
						this.currentJobProgress = 0;
						this.RaisePropertyChanged(nameof(this.Progress));

						if (this.ScanFinished)
						{
							DispatchUtilities.BeginInvoke(() =>
							{
								this.Cancel.Execute(null);
							});
						}
						else
						{
							this.ScanNextJob();
						}
					}
				};

			onDemandInstance.StartScan(path, Config.PreviewCount, TimeSpan.FromSeconds(Config.MinimumTitleLengthSeconds), 0);
		}
	}
}
