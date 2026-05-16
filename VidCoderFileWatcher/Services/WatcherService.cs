using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VidCoderCommon.Model;
using VidCoderCommon.Services;
using VidCoderCommon.Utilities;
using VidCoderFileWatcher.Model;
using Timer = System.Timers.Timer;

namespace VidCoderFileWatcher.Services;

public class WatcherService : IWatcherCommands
{
	private readonly IBasicLogger logger;
	private Dictionary<string, StrippedPicker>? pickers;
	private readonly Dictionary<string, PickerFileFilter> pickerFileFilters = new();
	private static readonly StrippedPicker DefaultPicker = new()
	{
		Name = "Default",
		IgnoreFilesBelowMbEnabled = false,
		VideoFileExtensions = PickerDefaults.VideoFileExtensions
	};

	private readonly SemaphoreSlim sync = new(1, 1);
	private readonly SemaphoreSlim processCommunicationSync = new(1, 1);

	private readonly IList<IDisposable> folderWatchers = new List<IDisposable>();

	/// <summary>
	/// Files that are being monitored for stability. Once they are stable, they will be queued in the main process.
	/// We keep track of them here so we can check them on a timer.
	/// </summary>
	private readonly IList<WatchedFileBatch> pendingStability = new List<WatchedFileBatch>();

	private Timer? pendingStabilityCheckTimer = null;

	private class WatchedFileBatch
	{
		public WatchedFileBatch(WatchedFolder watchedFolder, IList<string> files)
		{
			this.WatchedFolder = watchedFolder;
			this.Files = files;
		}

		public WatchedFolder WatchedFolder { get; }

		public IList<string> Files { get; }

		public IDictionary<string, long> LastFileSizes { get; } = new Dictionary<string, long>();
	}

	public WatcherService(IBasicLogger logger)
	{
		this.logger = logger;

		////System.Diagnostics.Debugger.Launch();
	}

	public async void RefreshFromWatchedFolders()
	{
		await this.RefreshFromWatchedFoldersAsync().ConfigureAwait(false);
	}

	public async Task RefreshFromWatchedFoldersAsync()
	{
		await this.sync.WaitAsync().ConfigureAwait(false);
		try
		{
			this.logger.Log("Refreshing file list...");
			this.PopulatePickersIfNeeded();

			List<WatchedFolder> folders = WatcherStorage.GetWatchedFolders(WatcherDatabase.Connection);

			var folderEnumerator = new FolderEnumerator(folders, this, this.logger, logVerbose: true);
			await folderEnumerator.CheckFolders();

			this.RefreshFolderWatchers(folders);
		}
		finally
		{
			this.sync.Release();
		}
	}

	private void RefreshFolderWatchers(IList<WatchedFolder> folders)
	{
		foreach (IDisposable watcher in this.folderWatchers)
		{
			watcher.Dispose();
		}

		string mode = DatabaseConfig.Get<string>("WatcherMode", "FileSystemWatcher", WatcherDatabase.Connection);

		this.folderWatchers.Clear();

		if (mode == "Polling")
		{
			this.logger.Log("Starting PollingFolderWatcher");
			this.folderWatchers.Add(new PollingFolderWatcher(this, folders, this.logger));
		}
		else
		{
			// FileSystemWatcher
			foreach (WatchedFolder folder in folders)
			{
				this.logger.Log("Starting SystemFolderWatcher for " + folder.Path);
				var watcher = new SystemFolderWatcher(this, folder, this.logger);
				this.folderWatchers.Add(watcher);
			}
		}
	}

	/// <summary>
	/// Restores stability monitoring for all files marked as Found.
	/// </summary>
	public void RestoreFoundFiles()
	{
		Dictionary<string, WatchedFile> foundFiles = WatcherStorage.GetFilesWithStatus(WatcherDatabase.Connection, WatchedFileStatus.Found);
		if (foundFiles.Count == 0)
		{
			return;
		}

		this.logger.Log($"Restoring stability monitoring for {foundFiles.Count} found file(s) from database.");

		List<WatchedFolder> folders = WatcherStorage.GetWatchedFolders(WatcherDatabase.Connection);
		var batches = new Dictionary<string, WatchedFileBatch>();

		// Group the found files by their watched folder
		foreach (WatchedFile file in foundFiles.Values)
		{
			WatchedFolder? folder = folders.FirstOrDefault(f => file.Path.StartsWith(f.Path, StringComparison.OrdinalIgnoreCase));
			if (folder != null)
			{
				if (!batches.ContainsKey(folder.Path))
				{
					batches.Add(folder.Path, new WatchedFileBatch(folder, new List<string>()));
				}

				batches[folder.Path].Files.Add(file.Path);
			}
		}

		// Restart monitoring for stability of found files.
		foreach (WatchedFileBatch batch in batches.Values)
		{
			this.logger.Log($"Restarting stability monitoring for {batch.Files.Count} found file(s) in folder '{batch.WatchedFolder.Path}'.");
			this.StartMonitoringFoundFiles(batch.WatchedFolder, batch.Files);
		}
	}

	/// <summary>
	/// Marks the given files as found, and monitors them for stability.
	/// The folder watcher implementation calls this to let us know that some files were found in a watched folder.
	/// Marks the files as "Found" in the database, and monitors them for stability. Once they are stable (not locked, size not changing)
	/// they will be queued in the main process.
	/// </summary>
	/// <param name="watchedFolder">The folder being watched</param>
	/// <param name="files">The paths to the video files/folders to monitor, and some metadata.</param>
	public void MarkFound(WatchedFolder watchedFolder, IList<string> files)
	{
		this.logger.Log($"Found new file(s) in watched folder '{watchedFolder.Path}':");
		foreach (string file in files)
		{
			this.logger.Log("    " + file);
		}

		WatcherStorage.AddEntries(WatcherDatabase.Connection, files);

		// Make a copy of the list so we are not affected by the passed in list changing after this point.
		this.StartMonitoringFoundFiles(watchedFolder, [.. files]);
	}

	private void StartMonitoringFoundFiles(WatchedFolder watchedFolder, IList<string> files)
	{
		var pendingFiles = new HashSet<string>(
			this.pendingStability.SelectMany(b => b.Files),
			StringComparer.OrdinalIgnoreCase);

		var filesToMonitor = files.Where(file => !pendingFiles.Contains(file)).ToList();
		if (filesToMonitor.Count == 0)
		{
			return;
		}

		var batch = new WatchedFileBatch(watchedFolder, filesToMonitor);
		foreach (string file in filesToMonitor)
		{
			try
			{
				var info = new FileInfo(file);
				if (!info.Exists)
				{
					this.logger.Log($"File no longer exists, cancelling: {file}");
					continue;
				}

				batch.LastFileSizes[file] = info.Length;
			}
			catch (Exception exception)
			{
				// If we can't even check the file info, we won't be able to encode it
				this.logger.Log($"Exeception checking file info, cancelling: {file}" + Environment.NewLine + exception.ToString());
				continue;
			}
		}

		this.pendingStability.Add(batch);
		this.SetUpStabilityCheckTimer();
	}

	private void SetUpStabilityCheckTimer()
	{
		if (this.pendingStabilityCheckTimer == null)
		{
			double stabilityCheckIntervalSeconds = DatabaseConfig.Get<double>("WatcherStabilityCheckIntervalSeconds", 1.0, WatcherDatabase.Connection);

			this.pendingStabilityCheckTimer = new Timer(stabilityCheckIntervalSeconds * 1000);
			this.pendingStabilityCheckTimer.Elapsed += this.OnPendingStabilityCheckTimerElapsed;
			this.pendingStabilityCheckTimer.Start();
		}
	}

	private async void OnPendingStabilityCheckTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
	{
		await this.sync.WaitAsync().ConfigureAwait(false);
		try
		{
			var batchesToRemove = new List<WatchedFileBatch>();
			foreach (WatchedFileBatch batch in this.pendingStability)
			{
				var filesToQueue = new List<string>();
				var filesToRemove = new List<string>();
				foreach (string file in batch.Files)
				{
					try
					{
						var info = new FileInfo(file);
						if (!info.Exists)
						{
							this.logger.Log($"File no longer exists, cancelling: {file}");
							filesToRemove.Add(file);
							continue;
						}

						if (CommonFileUtilities.IsFileLocked(file))
						{
							this.logger.Log($"File is still locked, waiting: {file}");
							continue;
						}

						long newFileSize = info.Length;
						long lastFileSize = batch.LastFileSizes[file];

						if (newFileSize == 0)
						{
							this.logger.Log($"File size is 0, waiting: {file}");
							continue;
						}

						if (newFileSize != lastFileSize)
						{
							this.logger.Log($"File size changed from {lastFileSize} to {newFileSize}, waiting: {file}");
							batch.LastFileSizes[file] = newFileSize;
							continue;
						}

						filesToQueue.Add(file);
					}
					catch (Exception exception)
					{
						// If we can't even check the file info, we won't be able to encode it
						this.logger.Log($"Exeception checking file info, cancelling: {exception}");
						filesToRemove.Add(file);
						continue;
					}
				}

				foreach (var file in filesToRemove)
				{
					batch.Files.Remove(file);
				}

				if (filesToRemove.Count > 0)
				{
					WatcherStorage.RemoveEntries(WatcherDatabase.Connection, filesToRemove);
				}

				foreach (var file in filesToQueue)
				{
					batch.Files.Remove(file);
				}

				if (filesToQueue.Count > 0)
				{
					await this.QueueInMainProcess(batch.WatchedFolder, filesToQueue).ConfigureAwait(false);
				}

				// All files accounted for. Remove the entry.
				if (batch.Files.Count == 0)
				{
					batchesToRemove.Add(batch);
				}
			}

			foreach (var batch in batchesToRemove)
			{
				this.pendingStability.Remove(batch);
			}
		}
		finally
		{
			this.sync.Release();
		}


		if (this.pendingStability.Count == 0 && this.pendingStabilityCheckTimer != null)
		{
			this.pendingStabilityCheckTimer.Stop();
			this.pendingStabilityCheckTimer.Dispose();
			this.pendingStabilityCheckTimer = null;
		}
	}

	/// <summary>
	/// Queue the given files in the main process.
	/// </summary>
	/// <param name="watchedFolder">The folder being watched</param>
	/// <param name="files">The paths to the video files/folders to queue, and some metadata.</param>
	private async Task QueueInMainProcess(WatchedFolder watchedFolder, IList<string> files)
	{
		if (files.Count == 0)
		{
			return;
		}

		// Mark them as stable/planned
		WatcherStorage.UpdateEntryStatus(WatcherDatabase.Connection, files, WatchedFileStatus.Planned);

		this.logger.Log($"Sending video(s) to be queued with picker '{watchedFolder.Picker}' and preset '{watchedFolder.Preset}':");
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

	public async Task NotifyMainProcessOfFilesRemoved(IList<string> removedFiles)
	{
		if (removedFiles.Count == 0)
		{
			return;
		}

		WatcherStorage.RemoveEntries(WatcherDatabase.Connection, removedFiles);
		this.logger.Log("Detected removed files:");
		foreach (string file in removedFiles)
		{
			this.logger.Log("    " + file);
		}

		if (VidCoderLauncher.VidCoderIsRunning())
		{
			this.logger.Log("Notifying main process of removed files.");

			await this.processCommunicationSync.WaitAsync().ConfigureAwait(false);
			try
			{
				await VidCoderLauncher.RunActionAsync(a => a.NotifyRemovedFiles(removedFiles.ToArray()));
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
	}

	/// <summary>
	/// Gets the source paths (files or folder) in the given watched folder.
	/// </summary>
	/// <param name="path">The path to look under.</param>
	/// <param name="watchedFolder">The folder to look in.</param>
	/// <returns>The source paths in the folder.</returns>
	public List<SourcePathWithType> GetPathsInWatchedFolder(string path, WatchedFolder watchedFolder)
	{
		(List<SourcePathWithType> sourcePaths, List<string> inacessibleDirectories) = CommonFileUtilities.GetFilesOrVideoFolders(
			path,
			this.GetFileFilterForWatchedFolder(watchedFolder));

		foreach (string inaccessibleDirectory in inacessibleDirectories)
		{
			this.logger.Log("Could not access directory: " + inaccessibleDirectory);
		}

		return sourcePaths;
	}

	private StrippedPicker GetPickerForWatchedFolder(WatchedFolder watchedFolder)
	{
		this.PopulatePickersIfNeeded();
		if (this.pickers != null && this.pickers.ContainsKey(watchedFolder.Picker))
		{
			return this.pickers[watchedFolder.Picker];
		}
		else
		{
			return DefaultPicker;
		}
	}

	public PickerFileFilter GetFileFilterForWatchedFolder(WatchedFolder watchedFolder)
	{
		StrippedPicker picker = GetPickerForWatchedFolder(watchedFolder);

		if (this.pickerFileFilters.TryGetValue(watchedFolder.Picker, out PickerFileFilter? result))
		{
			return result;
		}
		else
		{
			var filter = new PickerFileFilter
			{
				Extensions = CommonFileUtilities.ParseVideoExtensionList(picker.VideoFileExtensions),
				IgnoreFilesBelowMb = picker.IgnoreFilesBelowMbEnabled ? picker.IgnoreFilesBelowMb : 0
			};
			this.pickerFileFilters[watchedFolder.Picker] = filter;
			return filter;
		}
	}

	public bool FilePassesPicker(WatchedFolder watchedFolder, string filePath)
	{
		try
		{
			PickerFileFilter filter = GetFileFilterForWatchedFolder(watchedFolder);
			return CommonFileUtilities.FilePassesPickerFilter(new FileInfo(filePath), filter);
		}
		catch (Exception exception)
		{
			this.logger.Log("Could not get file info to check if it passes the picker." + Environment.NewLine + exception.ToString());
			return true;
		}
	}

	public void Ping()
	{
	}

	private void PopulatePickersIfNeeded()
	{
		if (this.pickers == null)
		{
			this.pickers = new Dictionary<string, StrippedPicker>();
			foreach (StrippedPicker picker in WatcherRepository.GetPickerList())
			{
				if (this.pickers.TryGetValue(picker.Name, out StrippedPicker? existingPicker))
				{
					if (picker.IsModified && !existingPicker.IsModified)
					{
						this.pickers[picker.Name] = picker;
					}
				}
				else
				{
					this.pickers.Add(picker.Name, picker);
				}
			}
		}
	}

	public async void InvalidatePickers()
	{
		await this.sync.WaitAsync().ConfigureAwait(false);
		try
		{
			this.pickers = null;
			this.pickerFileFilters.Clear();
		}
		finally
		{
			this.sync.Release();
		}
	}

	private class ExistingFile
	{
		public ExistingFile(string path, WatchedFolder folder)
		{
			this.Path = path;
			this.Folder = folder;
		}

		public string Path { get; set; }

		public bool HasEntry { get; set; }

		public WatchedFolder Folder { get; set; }
	}
}
