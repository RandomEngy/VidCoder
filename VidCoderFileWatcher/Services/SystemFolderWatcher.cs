using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VidCoderCommon.Model;
using VidCoderCommon.Utilities;
using VidCoderFileWatcher.Model;
using Timer = System.Timers.Timer;

namespace VidCoderFileWatcher.Services;

/// <summary>
/// Watches a folder with the FileSystemWatcher OS notifications.
/// </summary>
public class SystemFolderWatcher : IDisposable
{
	private bool isDisposed;

	private FileSystemWatcher watcher;

	private readonly IList<string> filesPendingWriteComplete = new List<string>();
	private readonly IList<string> filesPendingSend = new List<string>();
	private readonly IList<string> filesPendingRemoveEntry = new List<string>();
	private readonly WatcherService watcherService;
	private readonly WatchedFolder watchedFolder;
	private readonly IBasicLogger logger;

	private static readonly SemaphoreSlim sync = new SemaphoreSlim(1, 1);

	private Timer? pendingCheckTimer;

	public SystemFolderWatcher(WatcherService watcherService, WatchedFolder watchedFolder, IBasicLogger logger)
	{
		this.watcherService = watcherService;
		this.watchedFolder = watchedFolder;
		this.logger = logger;


		this.watcher = new FileSystemWatcher(watchedFolder.Path);
		this.watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName;
		this.watcher.Created += OnFileCreated;
		this.watcher.Renamed += OnFileRenamed;
		this.watcher.Deleted += OnFileDeleted;
		this.watcher.Filter = string.Empty;
		this.watcher.IncludeSubdirectories = true;
		this.watcher.EnableRaisingEvents = true;
	}

	private async void OnFileCreated(object sender, FileSystemEventArgs e)
	{
		await sync.WaitAsync().ConfigureAwait(false);
		try
		{
			this.logger.Log("Detected new file/folder: " + e.FullPath);
			this.HandleAndFilterNewFileOrFolder(e.FullPath);
		}
		finally
		{
			sync.Release();
		}
	}



	private async void OnFileRenamed(object sender, RenamedEventArgs e)
	{
		await sync.WaitAsync().ConfigureAwait(false);
		try
		{
			this.logger.Log($"File/folder renamed from {e.OldFullPath} to {e.FullPath}");
			this.HandleAndFilterNewFileOrFolder(e.FullPath);
			this.HandleDeletedFileOrFolder(e.OldFullPath);
		}
		finally
		{
			sync.Release();
		}
	}

	private async void OnFileDeleted(object sender, FileSystemEventArgs e)
	{
		await sync.WaitAsync().ConfigureAwait(false);
		try
		{
			this.logger.Log("Detected file/folder removed: " + e.FullPath);
			this.HandleDeletedFileOrFolder(e.FullPath);
		}
		finally
		{
			sync.Release();
		}
	}

	private void HandleAndFilterNewFileOrFolder(string path)
	{
		if (CommonFileUtilities.IsDirectory(path))
		{
			this.logger.Log("Path is a folder. Searching for video sources inside.");
			List<SourcePathWithType> paths = this.watcherService.GetPathsInWatchedFolder(path, this.watchedFolder);
			foreach (var sourcePath in paths)
			{
				this.logger.Log("Found new source: " + sourcePath.Path);
				this.HandleAndFilterNewFile(sourcePath.Path);
			}
		}
		else
		{
			this.HandleAndFilterNewFile(path);
		}
	}

	private void HandleDeletedFileOrFolder(string path)
	{
		WatchedFile fileEntry = WatcherStorage.GetFileEntry(WatcherDatabase.Connection, path);
		if (fileEntry == null)
		{
			// If there is no direct entry for it, see if this is a folder and there are sub-items under it.
			List<string> paths = WatcherStorage.GetFileEntriesUnderFolder(WatcherDatabase.Connection, path);
			this.logger.Log("Path is a folder. Searching for video sources inside.");
			foreach (var sourcePath in paths)
			{
				this.logger.Log("Found entry for deleted source: " + sourcePath);
				this.HandleDeletedFile(sourcePath);
			}
		}
		else
		{
			// If there is a direct entry, delete it.
			this.HandleDeletedFile(path);
		}
	}

	/// <summary>
	/// Handles a new file, applying filters to it.
	/// </summary>
	/// <param name="path">The path to the new video file.</param>
	/// <returns>True if the file should be added.</returns>
	private void HandleAndFilterNewFile(string path)
	{
		if (this.watcherService.FilePassesPicker(this.watchedFolder, path))
		{
			WatchedFile entry = WatcherStorage.GetFileEntry(WatcherDatabase.Connection, path);
			if (entry != null && entry.Status != WatchedFileStatus.Planned)
			{
				return;
			}

			if (this.filesPendingWriteComplete.Contains(path))
			{
				return;
			}

			this.filesPendingWriteComplete.Add(path);
			this.SetUpPendingTimer();
		}
	}

	private void HandleDeletedFile(string path)
	{
		if (!this.filesPendingRemoveEntry.Contains(path))
		{
			this.filesPendingRemoveEntry.Add(path);
			this.SetUpPendingTimer();
		}
	}

	private void SetUpPendingTimer()
	{
		if (this.pendingCheckTimer == null)
		{
			this.pendingCheckTimer = new Timer
			{
				AutoReset = false,
				Interval = 500
			};

			this.pendingCheckTimer.Elapsed += OnPendingCheckTimerElapsed;
			this.pendingCheckTimer.Start();
		}
	}

	private async void OnPendingCheckTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
	{
		await sync.WaitAsync().ConfigureAwait(false);
		try
		{
			this.logger.Log("Checking lock for files pending write complete");

			for (int i = this.filesPendingWriteComplete.Count - 1; i >= 0; i--)
			{
				string file = this.filesPendingWriteComplete[i];
				if (!CommonFileUtilities.IsFileLocked(file))
				{
					this.logger.Log(file + " is no longer locked");
					this.filesPendingWriteComplete.RemoveAt(i);
					this.filesPendingSend.Add(file);
				}
			}

			if (this.filesPendingRemoveEntry.Count > 0)
			{
				await this.watcherService.NotifyMainProcessOfFilesRemoved(this.filesPendingRemoveEntry).ConfigureAwait(false);
				this.filesPendingRemoveEntry.Clear();
			}

			if (this.filesPendingSend.Count > 0)
			{
				await this.watcherService.QueueInMainProcess(this.watchedFolder, this.filesPendingSend).ConfigureAwait(false);
				this.filesPendingSend.Clear();
			}

			if (this.filesPendingWriteComplete.Count == 0)
			{
				if (this.pendingCheckTimer != null)
				{
					this.pendingCheckTimer.Dispose();
					this.pendingCheckTimer = null;
				}
			}
			else
			{
				if (this.pendingCheckTimer != null)
				{
					this.pendingCheckTimer.Interval = 1000;
					this.pendingCheckTimer.Start();
				}
			}
		}
		catch (Exception exception)
		{
			this.logger.Log(exception.ToString());
		}
		finally
		{
			sync.Release();
		}
	}

	protected virtual void Dispose(bool disposing)
	{
		if (!this.isDisposed)
		{
			if (disposing)
			{
				this.watcher.Dispose();
			}

			this.isDisposed = true;
		}
	}

	public void Dispose()
	{
		// Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
		this.Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}
}
