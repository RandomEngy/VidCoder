using System;
using System.Collections.Generic;
using DynamicData;
using HandBrake.Interop.Interop.Json.Scan;
using Microsoft.AnyContainer;
using Newtonsoft.Json;
using VidCoder.Extensions;
using VidCoder.Model;
using ReactiveUI;
using VidCoder.Services;
using VidCoder.Services.HandBrakeProxy;

namespace VidCoder.ViewModel
{
	/// <summary>
	/// View model for dialog that shows when scanning multiple files.
	/// </summary>
	public class ScanMultipleDialogViewModel : OkCancelDialogViewModel
	{
		private IList<SourcePath> pathsToScan;
		private int currentJobIndex;
		private float currentJobProgress;
		private object currentJobIndexLock = new object();

		public ScanMultipleDialogViewModel(IList<SourcePath> pathsToScan)
		{
			this.pathsToScan = pathsToScan;
			this.ScanResults = new List<ScanResult>();
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

		public List<ScanResult> ScanResults { get; private set; }

		private void ScanNextJob()
		{
			SourcePath path = this.pathsToScan[this.currentJobIndex];

		    IScanProxy scanProxy = Utilities.CreateScanProxy();
		    scanProxy.ScanProgress += (sender, args) =>
		    {
                lock (this.currentJobIndexLock)
                {
                    this.currentJobProgress = args.Value;
                    this.RaisePropertyChanged(nameof(this.Progress));
                }
            };

		    scanProxy.ScanCompleted += (sender, args) =>
		    {
		        if (args.Value != null)
		        {
		            JsonScanObject scanObject = JsonConvert.DeserializeObject<JsonScanObject>(args.Value);
		            this.ScanResults.Add(new ScanResult { SourcePath = path, VideoSource = scanObject.ToVideoSource() });
		        }
		        else
		        {
                    this.ScanResults.Add(new ScanResult { SourcePath = path });
		        }

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

			var scanLogger = StaticResolver.Resolve<AppLoggerFactory>().ResolveJobLogger("Scan");

            scanProxy.StartScan(path.Path, scanLogger);
		}

		public bool TryAddScanPaths(IList<SourcePath> additionalPaths)
		{
			lock (this.currentJobIndexLock)
			{
				if (this.ScanFinished)
				{
					return false;
				}

				this.pathsToScan.AddRange(additionalPaths);
				this.RaisePropertyChanged(nameof(this.Progress));

				return true;
			}
		}

	    public class ScanResult
	    {
            public SourcePath SourcePath { get; set; }

            public VideoSource VideoSource { get; set; }
	    }
	}
}
