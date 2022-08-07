using Microsoft.AnyContainer;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VidCoder.Model;
using VidCoderCommon.Model;

namespace VidCoder.Services
{
	/// <summary>
	/// Listens for job events and updates status on the watched files database.
	/// </summary>
	public class WatchedFileStatusTracker
	{
		private ProcessingService processingService;

		public WatchedFileStatusTracker(ProcessingService processingService)
		{
			this.processingService = processingService;
		}

		public void Start()
		{
			this.processingService.JobCompleted += this.OnJobCompleted;
			this.processingService.JobRemovedFromQueue += this.OnJobRemovedFromQueue;

			SQLiteConnection connection = Database.Connection;

			// Queue up anything that hasn't been encoded yet
			Dictionary<string, WatchedFile> plannedFiles = WatcherStorage.GetPlannedFiles(connection);

			foreach (var job in this.processingService.EncodeQueue.Items)
			{
				string path = job.Job.SourcePath;
				if (plannedFiles.ContainsKey(path))
				{
					plannedFiles.Remove(path);
				}
			}

			if (plannedFiles.Count > 0)
			{
				// Key is the folder path, value is the list of files added under that path.
				List<WatchedFolder> folders = WatcherStorage.GetWatchedFolders(connection);
				var folderMap = new Dictionary<string, List<string>>();
				foreach (var folder in folders)
				{
					folderMap.Add(folder.Path, new List<string>());
				}

				foreach (WatchedFile watchedFile in plannedFiles.Values)
				{
					string folderPath = null;

					// Find out what folder we're in
					foreach (WatchedFolder folder in folders)
					{
						if (watchedFile.Path.StartsWith(folder.Path, StringComparison.OrdinalIgnoreCase))
						{
							folderPath = folder.Path;
							break;
						}
					}

					if (folderPath == null)
					{
						StaticResolver.Resolve<IAppLogger>().LogError("Could not find folder for watched file " + watchedFile.Path);
					}
					else
					{
						folderMap[folderPath].Add(watchedFile.Path);
					}
				}

				foreach (var pair in folderMap)
				{
					if (pair.Value.Count > 0)
					{
						WatchedFolder watchedFolder = folders.First(f => f.Path.Equals(pair.Key));
						this.processingService.QueueFromFileWatcher(pair.Key, pair.Value.ToArray(), watchedFolder.Preset, watchedFolder.Picker);
					}
				}
			}
		}

		public void Stop()
		{
			this.processingService.JobCompleted -= this.OnJobCompleted;
			this.processingService.JobRemovedFromQueue -= this.OnJobRemovedFromQueue;
		}

		private void OnJobCompleted(object sender, Model.JobCompletedEventArgs e)
		{
			if (e.Reason == Model.EncodeCompleteReason.Finished)
			{
				WatcherStorage.UpdateEntryStatus(
					Database.Connection,
					e.JobViewModel.Job.SourcePath,
					e.ResultStatus == Model.EncodeResultStatus.Succeeded ? WatchedFileStatus.Succeeded : WatchedFileStatus.Failed);
			}
		}

		private void OnJobRemovedFromQueue(object sender, EventArgs<ViewModel.EncodeJobViewModel> e)
		{
			WatcherStorage.UpdateEntryStatus(
				Database.Connection,
				e.Value.Job.SourcePath,
				WatchedFileStatus.Canceled);
		}
	}
}
