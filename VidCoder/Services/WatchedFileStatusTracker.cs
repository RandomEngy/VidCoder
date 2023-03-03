using Microsoft.AnyContainer;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VidCoder.Model;
using VidCoder.ViewModel;
using VidCoderCommon.Model;

namespace VidCoder.Services;

/// <summary>
/// Listens for job events and updates status on the watched files database.
/// </summary>
public class WatchedFileStatusTracker
{
	private ProcessingService processingService;

	private IAppLogger logger = StaticResolver.Resolve<IAppLogger>();

	public WatchedFileStatusTracker(ProcessingService processingService)
	{
		this.processingService = processingService;
	}

	public void Start()
	{
		this.processingService.JobCompleted += this.OnJobCompleted;
		this.processingService.JobRemovedFromQueue += this.OnJobRemovedFromQueue;
		this.processingService.JobQueueSkipped += this.OnJobQueueSkipped;

		SQLiteConnection connection = Database.Connection;

		// Queue up anything that hasn't been encoded yet
		Dictionary<string, WatchedFile> plannedFiles = WatcherStorage.GetPlannedFiles(connection);
		this.logger.LogDebug($"Starting WatchedFileStatusTracker. Found {plannedFiles.Count} planned files.");

		foreach (var job in this.processingService.EncodeQueue.Items)
		{
			string path = job.Job.SourcePath;
			if (plannedFiles.ContainsKey(path))
			{
				plannedFiles.Remove(path);
			}
		}

		this.logger.LogDebug($"{plannedFiles.Count} files were not already in queue; adding these now.");
		if (plannedFiles.Count > 0)
		{
			this.processingService.QueueWatchedFiles(plannedFiles.Values);
		}
	}

	public void Stop()
	{
		this.processingService.JobCompleted -= this.OnJobCompleted;
		this.processingService.JobRemovedFromQueue -= this.OnJobRemovedFromQueue;
		this.processingService.JobQueueSkipped -= this.OnJobQueueSkipped;
	}

	private void OnJobCompleted(object sender, JobCompletedEventArgs e)
	{
		if (e.JobViewModel.CompleteReason == EncodeCompleteReason.Finished)
		{
			WatcherStorage.UpdateEntryStatus(
				Database.Connection,
				e.JobViewModel.Job.SourcePath,
				e.ResultStatus == EncodeResultStatus.Succeeded ? WatchedFileStatus.Succeeded : WatchedFileStatus.Failed);
		}
	}

	private void OnJobRemovedFromQueue(object sender, EventArgs<EncodeJobViewModel> e)
	{
		WatcherStorage.UpdateEntryStatus(
			Database.Connection,
			e.Value.Job.SourcePath,
			WatchedFileStatus.Canceled);
	}

	private void OnJobQueueSkipped(object sender, EventArgs<string> e)
	{
		WatcherStorage.UpdateEntryStatus(
			Database.Connection,
			e.Value,
			WatchedFileStatus.Skipped);
	}
}
