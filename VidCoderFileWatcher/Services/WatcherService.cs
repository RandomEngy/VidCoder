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

		private readonly object sync = new object();

		private readonly IList<FileSystemWatcher> fileSystemWatchers = new List<FileSystemWatcher>();

		private readonly Action scheduleFileProcess;

		private readonly IList<string> filesPendingCreate = new List<string>();

		private readonly IList<string> filesPendingDelete = new List<string>();

		public WatcherService(IBasicLogger logger)
		{
			this.logger = logger;
			Action processPendingFiles = () =>
			{
				this.ProcessPendingFiles();
			};
			
			this.scheduleFileProcess = processPendingFiles.Debounce(500);
			this.RefreshPickers();
		}

		public void RefreshFromWatchedFolders()
		{
			lock (this.sync)
			{
				this.logger.Log("Refreshing file list...");

				List<WatchedFolder> folders = WatcherStorage.GetWatchedFolders(WatcherDatabase.Connection);
				IDictionary<string, MarkableFile> existingFiles = this.GetFilesInWatchedFolders(folders);
				Dictionary<string, WatchedFile> entries = WatcherStorage.GetWatchedFiles(WatcherDatabase.Connection);

				// Go over all entries, marking any matches along the way
				var entriesToRemove = new List<string>();
				foreach (string path in entries.Keys)
				{
					if (existingFiles.TryGetValue(path, out MarkableFile file))
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
				var filesToEnqueue = new List<string>();
				foreach (string path in existingFiles.Keys)
				{
					if (!existingFiles[path].HasEntry)
					{
						filesToEnqueue.Add(path);
					}
				}

				this.QueueInMainProcess(filesToEnqueue);
				this.RefreshFileSystemWatchers(folders);
			}
		}

		private void RefreshFileSystemWatchers(IEnumerable<WatchedFolder> folders)
		{
			foreach (FileSystemWatcher watcher in this.fileSystemWatchers)
			{
				watcher.Dispose();
			}

			this.fileSystemWatchers.Clear();

			foreach (WatchedFolder folder in folders)
			{
				var watcher = new FileSystemWatcher(folder.Path);
				watcher.NotifyFilter = NotifyFilters.FileName;
				watcher.Created += OnFileCreated;
				watcher.Renamed += OnFileRenamed;
				watcher.Deleted += OnFileDeleted;
				watcher.Filter = string.Empty;
				watcher.IncludeSubdirectories = true;
				watcher.EnableRaisingEvents = true;

				this.fileSystemWatchers.Add(watcher);
			}
		}

		private void OnFileCreated(object sender, FileSystemEventArgs e)
		{
			lock (this.sync)
			{
				this.logger.Log("Detected new file: " + e.FullPath);
				this.filesPendingCreate.Add(e.FullPath);
				this.scheduleFileProcess();
			}
		}

		private void OnFileRenamed(object sender, RenamedEventArgs e)
		{
			lock (this.sync)
			{
				this.logger.Log($"File renamed from {e.OldFullPath} to {e.FullPath}");
				this.filesPendingCreate.Add(e.FullPath);
				this.filesPendingDelete.Add(e.OldFullPath);
				this.scheduleFileProcess();
			}
		}

		private void OnFileDeleted(object sender, FileSystemEventArgs e)
		{
			lock (this.sync)
			{
				this.logger.Log("Detected file removed: " + e.FullPath);
				this.filesPendingDelete.Add(e.FullPath);
				this.scheduleFileProcess();
			}
		}

		/// <summary>
		/// Processes the files in filesPendingCreate and filesPendingDelete.
		/// </summary>
		private void ProcessPendingFiles()
		{
			lock (this.sync)
			{
				this.QueueInMainProcess(this.filesPendingCreate);
				this.filesPendingCreate.Clear();

				WatcherStorage.RemoveEntries(WatcherDatabase.Connection, filesPendingDelete);
				this.filesPendingDelete.Clear();
			}
		}

		/// <summary>
		/// Queue the given files in the main process.
		/// </summary>
		/// <param name="paths">The paths to the video files/folders to queue.</param>
		private void QueueInMainProcess(IList<string> paths)
		{
			if (paths.Count == 0)
			{
				return;
			}

			WatcherStorage.AddEntries(WatcherDatabase.Connection, paths);
			this.logger.Log("Sending video(s) to be queued:");
			foreach (string path in paths)
			{
				this.logger.Log("    " + path);
			}

			// TODO - signal main VidCoder process to start these
		}

		/// <summary>
		/// Gets the source paths (files or folders) in the currently watched folders.
		/// </summary>
		/// <returns>The source paths in the currently watched folders.</returns>
		private IDictionary<string, MarkableFile> GetFilesInWatchedFolders(IEnumerable<WatchedFolder> folders)
		{
			var files = new Dictionary<string, MarkableFile>(StringComparer.OrdinalIgnoreCase);

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
					files.Add(sourcePath.Path, new MarkableFile { Path = sourcePath.Path });
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

		private class MarkableFile
		{
			public string Path { get; set; }

			public bool HasEntry { get; set; }
		}
	}
}
