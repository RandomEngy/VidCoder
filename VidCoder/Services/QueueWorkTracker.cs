using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AnyContainer;
using ReactiveUI;
using VidCoder.Extensions;
using VidCoder.Model;

namespace VidCoder.Services
{
	/// <summary>
	/// Tracks work for the purposes of generating overall queue encode progress and ETA.
	/// </summary>
    public class QueueWorkTracker : ReactiveObject
    {
	    private double completedQueueWork;
	    private double totalQueueCost;
		private Stopwatch elapsedQueueEncodeTime;
	    private long pollCount;
	    private TimeSpan overallEtaSpan;
	    private bool diagnosticsLogged;
	    private long completedItems;
	    private readonly object trackerLock = new object();

		private BehaviorSubject<long> completedItemsSubject = new BehaviorSubject<long>(0);

		/// <summary>
		/// All work since starting encoding that has finished or is still in the queue.
		/// </summary>
		private readonly List<JobWork> queuedWork = new List<JobWork>();

		public double OverallWorkCompletionRate { get; private set; }

	    public bool CanShowEta { get; private set; }

	    private double overallEncodeProgressFraction;
	    public double OverallEncodeProgressFraction
	    {
		    get { return this.overallEncodeProgressFraction; }
		    set { this.RaiseAndSetIfChanged(ref this.overallEncodeProgressFraction, value); }
	    }

	    private string estimatedTimeRemaining;
	    public string EstimatedTimeRemaining
	    {
		    get { return this.estimatedTimeRemaining; }
		    set { this.RaiseAndSetIfChanged(ref this.estimatedTimeRemaining, value); }
	    }

	    private TimeSpan overallEncodeTime;
	    public TimeSpan OverallEncodeTime
	    {
		    get { return this.overallEncodeTime; }
		    set { this.RaiseAndSetIfChanged(ref this.overallEncodeTime, value); }
	    }

	    private TimeSpan overallEta;
	    public TimeSpan OverallEta
	    {
		    get { return this.overallEta; }
		    set { this.RaiseAndSetIfChanged(ref this.overallEta, value); }
	    }

		public IObservable<long> CompletedItemsObservable => this.completedItemsSubject;

		/// <summary>
		/// Reports encode starting (does NOT fire for resuming from paused)
		/// </summary>
		public void ReportEncodeStart(IEnumerable<JobWork> initialWork)
		{
			lock (this.trackerLock)
			{
				this.diagnosticsLogged = false;
				this.CanShowEta = false;
				this.elapsedQueueEncodeTime = Stopwatch.StartNew();
				this.OverallWorkCompletionRate = 0;
				this.OverallEncodeProgressFraction = 0;
				this.pollCount = 0;
				this.completedQueueWork = 0.0;
				this.completedItems = 0;
				this.completedItemsSubject.OnNext(0);

				this.totalQueueCost = 0.0;

				this.queuedWork.Clear();

				foreach (var work in initialWork)
				{
					this.totalQueueCost += work.Cost;
					this.queuedWork.Add(work);
				}
			}
		}

		/// <summary>
		/// Reports encode stop (does NOT fire for pausing)
		/// </summary>
		public void ReportEncodeStop()
	    {
			lock (this.trackerLock)
			{
				this.elapsedQueueEncodeTime.Stop(); 
			}
	    }

	    public void ReportEncodePause()
	    {
			lock (this.trackerLock)
			{
				this.elapsedQueueEncodeTime?.Stop(); 
			}
	    }

	    public void ReportEncodeResume()
	    {
			lock (this.trackerLock)
			{
				this.elapsedQueueEncodeTime?.Start(); 
			}
	    }

		public void ReportAddedToQueue(JobWork work)
	    {
			lock (this.trackerLock)
			{
				this.totalQueueCost += work.Cost;
				this.queuedWork.Add(work); 
			}
		}

		public void ReportRemovedFromQueue(JobWork work)
	    {
			lock (this.trackerLock)
			{
				this.totalQueueCost -= work.Cost;
				this.queuedWork.Remove(work); 
			}
		}

		public void ReportFinished(JobWork work)
	    {
			lock (this.trackerLock)
			{
				this.completedQueueWork += work.Cost;
				this.completedItems++;
				this.completedItemsSubject.OnNext(this.completedItems);
			}
		}

		public void CalculateOverallEncodeProgress(double inProgressJobsCompletedWork)
	    {
			lock (this.trackerLock)
			{
				double totalCompletedWork = this.completedQueueWork + inProgressJobsCompletedWork;

				this.OverallEncodeProgressFraction = this.totalQueueCost > 0 ? totalCompletedWork / this.totalQueueCost : 0;

				double queueElapsedSeconds = this.elapsedQueueEncodeTime.Elapsed.TotalSeconds;
				this.OverallWorkCompletionRate = queueElapsedSeconds > 0 ? totalCompletedWork / queueElapsedSeconds : 0;

				// Only update encode time every 5th update.
				if (Interlocked.Increment(ref this.pollCount) % 5 == 1)
				{
					if (!this.CanShowEta && this.elapsedQueueEncodeTime != null && queueElapsedSeconds > 0.5 && this.OverallEncodeProgressFraction != 0.0)
					{
						this.CanShowEta = true;
					}

					if (this.CanShowEta)
					{
						if (this.OverallEncodeProgressFraction == 1)
						{
							this.EstimatedTimeRemaining = TimeSpan.Zero.FormatFriendly();
						}
						else if (this.OverallEncodeProgressFraction >= 0 && this.OverallEncodeProgressFraction <= 1.01)
						{
							if (this.OverallEncodeProgressFraction == 0)
							{
								this.overallEtaSpan = TimeSpan.MaxValue;
							}
							else
							{
								try
								{
									this.overallEtaSpan =
										TimeSpan.FromSeconds((long)(((1.0 - this.OverallEncodeProgressFraction) * queueElapsedSeconds) / this.OverallEncodeProgressFraction));
								}
								catch (OverflowException)
								{
									this.overallEtaSpan = TimeSpan.MaxValue;
								}
							}

							this.EstimatedTimeRemaining = this.overallEtaSpan.FormatFriendly();
						}
						else
						{
							// Something fishy is going on. Dump the queue work contents and all relevant variables.
							if (!this.diagnosticsLogged)
							{
								IAppLogger logger = StaticResolver.Resolve<IAppLogger>();

								logger.LogError("Invalid total progress fraction calculated." + Environment.NewLine +
												$"Total queue cost: {this.totalQueueCost}" + Environment.NewLine +
												$"Completed queue work: {this.completedQueueWork}" + Environment.NewLine +
												$"In progress work: {inProgressJobsCompletedWork}" + Environment.NewLine +
												$"Completed items: {this.completedItems}");
								for (int i = 0; i < this.queuedWork.Count; i++)
								{
									JobWork workItem = this.queuedWork[i];
									logger.LogError($"Queue item {i}: Cost: {workItem.Cost} Video Length: {workItem.JobVideoLength} Completed: {i < this.completedItems}");
								}

								this.diagnosticsLogged = true;
							}

							this.EstimatedTimeRemaining = TimeSpan.Zero.FormatFriendly();
						}
					}
				}

				this.OverallEncodeTime = this.elapsedQueueEncodeTime.Elapsed;
				this.OverallEta = this.overallEtaSpan; 
			}
		}
    }
}
