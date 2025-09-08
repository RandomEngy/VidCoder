using HandBrake.Interop.Interop.Json.Scan;
using Microsoft.AnyContainer;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using VidCoder.Extensions;
using VidCoder.Model;
using VidCoder.Resources;
using VidCoder.Services.HandBrakeProxy;
using VidCoderCommon.Model;
using VidCoderCommon.Utilities;

namespace VidCoder.Services;

public class QueueAdderService : ReactiveObject
{
	/// <summary>
	/// The number of scans to complete before adding to the queue.
	/// </summary>
	private const int QueueAddChunkSize = 5;

	private readonly Queue<PendingScan> pendingScans = new();
	private readonly List<ScanResult> scanResults = new();
	private readonly List<string> failedFiles = new();
	private int scansCompletedThisSession = 0;
	private float currentJobProgress;
	private object sync = new();
	private IScanProxy scanProxy;
	private IAppLogger scanLogger;
	private int scansDoneOnCurrentScanProxy;
	private PendingScan currentScan;
	private bool isCanceled = false;
	private bool startPending = false;

	public double Progress
	{
		get
		{
			return ((this.scansCompletedThisSession + this.currentJobProgress) * 100.0) / (this.pendingScans.Count + this.scansCompletedThisSession + 1) ;
		}
	}

	public string ProgressLabel
	{
		get
		{
			int total = this.pendingScans.Count + this.scansCompletedThisSession + 1;
			int current = this.scansCompletedThisSession + 1;

			return string.Format(MainRes.QueueProgressLabel, current, total);
		}
	}

	private bool isScanning;
	public bool IsScanning
	{
		get => this.isScanning;
		set => this.RaiseAndSetIfChanged(ref this.isScanning, value);
	}

	private ReactiveCommand<Unit, Unit> cancel;
	public ICommand Cancel
	{
		get
		{
			return this.cancel ?? (this.cancel = ReactiveCommand.Create(
				() =>
				{
					lock (this.sync)
					{
						if (this.IsScanning)
						{
							// Update state so that when the next scan finishes it cleans up.
							this.isCanceled = true;
							this.IsScanning = false;
							this.pendingScans.Clear();
							this.scanResults.Clear();
						}
					}
				}));
		}
	}

	private void ScanNextJob()
	{
		this.currentScan = this.pendingScans.Dequeue();

		// Workaround to fix hang when scanning too many items at once
		if (this.scansDoneOnCurrentScanProxy > 35)
		{
			this.scanProxy.Dispose();
			this.scanProxy = null;
			this.scansDoneOnCurrentScanProxy = 0;
		}

		this.EnsureScanProxyCreated();

		string sourcePath = this.currentScan.SourcePath.Path;
		this.scanLogger = StaticResolver.Resolve<AppLoggerFactory>().ResolveRemoteScanLogger(sourcePath);
		this.scanProxy.StartScan(sourcePath, scanLogger);

		this.UpdateProgress();
	}

	private void EnsureScanProxyCreated()
	{
		if (this.scanProxy == null)
		{
			this.scanProxy = Utilities.CreateScanProxy();
			this.scanProxy.ScanProgress += (sender, args) =>
			{
				lock (this.sync)
				{
					this.currentJobProgress = args.Value;
					this.RaisePropertyChanged(nameof(this.Progress));
				}
			};

			this.scanProxy.ScanCompleted += (sender, args) =>
			{
				lock (this.sync)
				{
					if (this.isCanceled)
					{
						this.scanProxy.Dispose();
						this.scanProxy = null;
						this.scanLogger.Dispose();

						return;
					}

					if (args.Value != null)
					{
						JsonScanObject scanObject = JsonSerializer.Deserialize<JsonScanObject>(args.Value, JsonOptions.Plain);
						this.scanResults.Add(
							new ScanResult
							{
								SourcePath = this.currentScan.SourcePath,
								VideoSource = scanObject.ToVideoSource(),
								JobInstructions = this.currentScan.JobInstructions
							});

						if (this.currentScan.JobInstructions.Start)
						{
							this.startPending = true;
						}

						if (this.scanResults.Count >= QueueAddChunkSize)
						{
							this.AddResultsToQueue();
						}
					}
					else
					{
						// Add to list of failed files, pop a dialog at the end
						this.failedFiles.Add(this.currentScan.SourcePath.Path);
					}

					this.scansCompletedThisSession++;
					this.currentJobProgress = 0;
					this.scanLogger.Dispose();

					if (this.pendingScans.Count == 0)
					{
						this.scanProxy.Dispose();
						this.scanProxy = null;
						this.IsScanning = false;
						this.AddResultsToQueue();

						if (this.failedFiles.Count > 0)
						{
							Utilities.MessageBox.Show(
								string.Format(MainRes.QueueMultipleScanErrorMessage, string.Join(Environment.NewLine, this.failedFiles)),
								MainRes.QueueMultipleScanErrorTitle,
								MessageBoxButton.OK,
								MessageBoxImage.Warning);
							this.failedFiles.Clear();
						}
					}
					else
					{
						this.scansDoneOnCurrentScanProxy++;
						this.ScanNextJob();
					}

					this.UpdateProgress();
				}
			};
		}
	}

	public void ScanAndAddToQueue(IList<SourcePathWithMetadata> paths, JobInstructions jobInstructions)
	{
		lock (this.sync)
		{
			if (paths.Count == 0)
			{
				return;
			}

			bool needsStart = !this.IsScanning;

			this.isCanceled = false;

			foreach (SourcePathWithMetadata path in paths)
			{
				this.pendingScans.Enqueue(new PendingScan { SourcePath = path, JobInstructions = jobInstructions });
			}


			if (needsStart)
			{
				this.scansCompletedThisSession = 0;
				this.IsScanning = true;
				this.ScanNextJob();
			}
			else
			{
				this.UpdateProgress();
			}
		}
	}

	private void UpdateProgress()
	{
		this.RaisePropertyChanged(nameof(this.Progress));
		this.RaisePropertyChanged(nameof(this.ProgressLabel));
	}

	private void AddResultsToQueue()
	{
		if (this.scanResults.Count == 0)
		{
			return;
		}

		var scanResultsLocal = new List<ScanResult>(this.scanResults);

		DispatchUtilities.BeginInvoke(() =>
		{
			StaticResolver.Resolve<ProcessingService>().QueueFromScanResults(scanResultsLocal, this.startPending);
			this.startPending = false;
		});

		this.scanResults.Clear();
	}
}
