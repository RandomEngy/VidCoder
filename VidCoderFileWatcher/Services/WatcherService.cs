using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VidCoderCommon.Model;
using VidCoderCommon.Services;
using VidCoderCommon.Utilities;
using VidCoderFileWatcher.Model;

namespace VidCoderFileWatcher.Services
{
	public class WatcherService : IWatcherCommands
	{
		private readonly IBasicLogger logger;
		private Dictionary<string, StrippedPicker> pickers;
		private static readonly StrippedPicker DefaultPicker = new StrippedPicker
		{
			Name = "Default",
			IgnoreFilesBelowMbEnabled = false,
			VideoFileExtensions = PickerDefaults.VideoFileExtensions
		};

		private readonly SemaphoreSlim sync = new SemaphoreSlim(1, 1);
		private readonly SemaphoreSlim processCommunicationSync = new SemaphoreSlim(1, 1);

		private readonly IList<FolderWatcher> folderWatchers = new List<FolderWatcher>();

		public WatcherService(IBasicLogger logger)
		{
			this.logger = logger;
			this.RefreshPickers();

			////System.Diagnostics.Debugger.Launch();
		}

		public async void RefreshFromWatchedFolders()
		{
			await this.sync.WaitAsync().ConfigureAwait(false);
			try
			{
				this.logger.Log("Refreshing file list...");

				List<WatchedFolder> folders = WatcherStorage.GetWatchedFolders(WatcherDatabase.Connection);
				IDictionary<string, ExistingFile> existingFiles = this.GetFilesInWatchedFolders(folders);
				Dictionary<string, WatchedFile> entries = WatcherStorage.GetWatchedFiles(WatcherDatabase.Connection);

				// Go over all entries, marking any matches along the way
				var entriesToRemove = new List<string>();
				foreach (string path in entries.Keys)
				{
					if (existingFiles.TryGetValue(path, out ExistingFile file))
					{
						file.HasEntry = true;
					}
					else
					{
						// The file for this entry no longer exists. We can clean up the entry.
						entriesToRemove.Add(path);
					}
				}

				if (entriesToRemove.Count > 0)
				{
					this.logger.Log("Removing entries for file(s) that no longer exist:");
					foreach (string path in entriesToRemove)
					{
						this.logger.Log("    " + path);
					}

					WatcherStorage.RemoveEntries(WatcherDatabase.Connection, entriesToRemove);
				}

				// Any files without matching entries are new and should be enqueued
				var filesToEnqueue = new Dictionary<WatchedFolder, List<string>>();
				foreach (string path in existingFiles.Keys)
				{
					ExistingFile file = existingFiles[path];
					if (!file.HasEntry)
					{
						if (!filesToEnqueue.ContainsKey(file.Folder))
						{
							filesToEnqueue.Add(file.Folder, new List<string>());
						}

						filesToEnqueue[file.Folder].Add(file.Path);
					}
				}

				foreach (KeyValuePair<WatchedFolder, List<string>> pair in filesToEnqueue)
				{
					await this.QueueInMainProcess(pair.Key, pair.Value).ConfigureAwait(false);
				}

				this.RefreshFolderWatchers(folders);
			}
			finally
			{
				this.sync.Release();
			}
		}

		private void RefreshFolderWatchers(IEnumerable<WatchedFolder> folders)
		{
			foreach (FolderWatcher watcher in this.folderWatchers)
			{
				watcher.Dispose();
			}

			this.folderWatchers.Clear();

			foreach (WatchedFolder folder in folders)
			{
				var watcher = new FolderWatcher(this, folder, this.logger);
				this.folderWatchers.Add(watcher);
			}
		}

		/// <summary>
		/// Queue the given files in the main process.
		/// </summary>
		/// <param name="watchedFolder">The folder being watched</param>
		/// <param name="files">The paths to the video files/folders to queue, and some metadata.</param>
		public async Task QueueInMainProcess(WatchedFolder watchedFolder, IList<string> files)
		{
			if (files.Count == 0)
			{
				return;
			}

			WatcherStorage.AddEntries(WatcherDatabase.Connection, files);
			this.logger.Log("Sending video(s) to be queued:");
			foreach (string file in files)
			{
				this.logger.Log("    " + file);
			}

			await this.processCommunicationSync.WaitAsync().ConfigureAwait(false);
			try
			{
				if (!VidCoderLauncher.LaunchVidCoderIfNotRunning())
				{
					// If VidCoder was already running, notify it of the added files. Otherwise, we just let the launch code find the watched file entries and queue them.
					await VidCoderLauncher.SetupAndRunActionAsync(a => a.EncodeWatchedFiles(watchedFolder.Path, files.ToArray(), watchedFolder.Preset, watchedFolder.Picker));
				}
			}
			catch (Exception exception)
			{
				this.logger.Log(exception.ToString());
			}
			finally
			{
				this.processCommunicationSync.Release();
			}
		}

		public async Task NotifyMainProcessOfFilesRemoved(WatchedFolder watchedFolder, IList<string> removedFiles)
		{
			if (removedFiles.Count == 0)
			{
				return;
			}

			WatcherStorage.RemoveEntries(WatcherDatabase.Connection, removedFiles);
			this.logger.Log("Notifying main process of removed files:");
			foreach (string file in removedFiles)
			{
				this.logger.Log("    " + file);
			}


			await this.processCommunicationSync.WaitAsync().ConfigureAwait(false);
			try
			{
				await VidCoderLauncher.SetupAndRunActionAsync(a => a.NotifyRemovedFiles(watchedFolder.Path, removedFiles.ToArray()));
			}
			catch (Exception exception)
			{
				this.logger.Log(exception.ToString());
			}
			finally
			{
				this.processCommunicationSync.Release();
			}
		}

		/// <summary>
		/// Gets the source paths (files or folders) in the currently watched folders.
		/// </summary>
		/// <returns>The source paths in the currently watched folders.</returns>
		private IDictionary<string, ExistingFile> GetFilesInWatchedFolders(IEnumerable<WatchedFolder> folders)
		{
			var files = new Dictionary<string, ExistingFile>(StringComparer.OrdinalIgnoreCase);

			foreach (WatchedFolder folder in folders)
			{
				this.logger.Log("Enumerating folder: " + folder.Path);

				StrippedPicker picker;
				if (this.pickers.ContainsKey(folder.Picker))
				{
					picker = this.pickers[folder.Picker];
				}
				else
				{
					picker = DefaultPicker;
				}

				ISet<string> videoExtensions = CommonFileUtilities.ParseVideoExtensionList(picker.VideoFileExtensions);
				(List<SourcePathWithType> sourcePaths, List<string> inacessibleDirectories) = CommonFileUtilities.GetFilesOrVideoFolders(
					folder.Path, videoExtensions,
					picker.IgnoreFilesBelowMbEnabled ? picker.IgnoreFilesBelowMb : 0);

				foreach (string inaccessibleDirectory in inacessibleDirectories)
				{
					this.logger.Log("Could not access directory: " + inaccessibleDirectory);
				}

				foreach (SourcePathWithType sourcePath in sourcePaths)
				{
					this.logger.Log("    " + sourcePath.Path);
					files.Add(
						sourcePath.Path,
						new ExistingFile
						{
							Path = sourcePath.Path,
							Folder = folder
						});
				}
			}

			return files;
		}

		public void Ping()
		{
		}

		private void RefreshPickers()
		{
			// TODO - thread protection
			this.pickers = new Dictionary<string, StrippedPicker>();
			foreach (StrippedPicker picker in WatcherRepository.GetPickerList())
			{
				this.pickers.Add(picker.Name, picker);
			}
		}

		private class ExistingFile
		{
			public string Path { get; set; }

			public bool HasEntry { get; set; }

			public WatchedFolder Folder { get; set; }
		}
	}
}
