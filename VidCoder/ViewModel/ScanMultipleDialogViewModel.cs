using System;
using System.Collections.Generic;
using DynamicData;
using HandBrake.Interop.Interop.Json.Scan;
using Microsoft.AnyContainer;
using VidCoder.Extensions;
using VidCoder.Model;
using ReactiveUI;
using VidCoder.Services;
using VidCoder.Services.HandBrakeProxy;
using System.Text.Json;
using VidCoderCommon.Utilities;

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
		private IScanProxy scanProxy;
		private IAppLogger scanLogger;
		private int scansDoneOnCurrentScanProxy;
		private SourcePath currentPath;
		private bool allowClose;

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
			this.currentPath = this.pathsToScan[this.currentJobIndex];

			// Workaround to fix hang when scanning too many items at once
			if (this.scansDoneOnCurrentScanProxy > 40)
			{
				this.scanProxy.Dispose();
				this.scanProxy = null;
				this.scansDoneOnCurrentScanProxy = 0;
			}

			this.EnsureScanProxyCreated();

			this.scanLogger = StaticResolver.Resolve<AppLoggerFactory>().ResolveRemoteScanLogger(this.currentPath.Path);
            this.scanProxy.StartScan(this.currentPath.Path, scanLogger);
		}

		private void EnsureScanProxyCreated()
		{
			if (this.scanProxy == null)
			{
				this.scanProxy = Utilities.CreateScanProxy();
				this.scanProxy.ScanProgress += (sender, args) =>
				{
					lock (this.currentJobIndexLock)
					{
						this.currentJobProgress = args.Value;
						this.RaisePropertyChanged(nameof(this.Progress));
					}
				};

				this.scanProxy.ScanCompleted += (sender, args) =>
				{
					if (args.Value != null)
					{
						JsonScanObject scanObject = JsonSerializer.Deserialize<JsonScanObject>(args.Value, JsonOptions.Plain);
						this.ScanResults.Add(new ScanResult { SourcePath = this.currentPath, VideoSource = scanObject.ToVideoSource() });
					}
					else
					{
						this.ScanResults.Add(new ScanResult { SourcePath = this.currentPath });
					}

					lock (this.currentJobIndexLock)
					{
						this.currentJobIndex++;
						this.currentJobProgress = 0;
						this.RaisePropertyChanged(nameof(this.Progress));
						this.scanLogger.Dispose();

						if (this.ScanFinished)
						{
							this.scanProxy.Dispose();

							// After the scan has finished, allow closing the dialog
							this.allowClose = true;
							DispatchUtilities.BeginInvoke(() =>
							{
								this.Cancel.Execute(null);
							});
						}
						else
						{
							this.scansDoneOnCurrentScanProxy++;
							this.ScanNextJob();
						}
					}
				};
			}
		}

		public override bool OnClosing()
		{
			// When the user first tries to close, use that as a signal to cancel. Then when the scanning shuts down cleanly, allow the window to close.
			return this.allowClose;
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
