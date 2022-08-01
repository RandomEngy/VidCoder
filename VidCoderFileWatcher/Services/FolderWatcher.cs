using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VidCoderCommon.Model;
using VidCoderCommon.Utilities;
using VidCoderFileWatcher.Model;
using Timer = System.Timers.Timer;

namespace VidCoderFileWatcher.Services
{
	public class FolderWatcher : IDisposable
	{
		private bool isDisposed;

		private FileSystemWatcher watcher;

		private readonly IList<string> filesPendingWriteComplete = new List<string>();
		private readonly IList<string> filesPendingSend = new List<string>();
		private readonly IList<string> filesPendingRemoveEntry = new List<string>();
		private readonly WatcherService watcherService;
		private readonly WatchedFolder watchedFolder;
		private readonly IBasicLogger logger;

		private readonly SemaphoreSlim sync = new SemaphoreSlim(1, 1);

		private Timer? pendingCheckTimer;

		public FolderWatcher(WatcherService watcherService, WatchedFolder watchedFolder, IBasicLogger logger)
		{
			this.watcherService = watcherService;
			this.watchedFolder = watchedFolder;
			this.logger = logger;


			this.watcher = new FileSystemWatcher(watchedFolder.Path);
			this.watcher.NotifyFilter = NotifyFilters.FileName;
			this.watcher.Created += OnFileCreated;
			this.watcher.Renamed += OnFileRenamed;
			this.watcher.Deleted += OnFileDeleted;
			this.watcher.Filter = string.Empty;
			this.watcher.IncludeSubdirectories = true;
			this.watcher.EnableRaisingEvents = true;
		}

		private async void OnFileCreated(object sender, FileSystemEventArgs e)
		{
			await this.sync.WaitAsync().ConfigureAwait(false);
			try
			{
				this.logger.Log("Detected new file: " + e.FullPath);
				this.filesPendingWriteComplete.Add(e.FullPath);
				this.SetUpPendingTimer();
			}
			finally
			{
				this.sync.Release();
			}
		}

		private async void OnFileRenamed(object sender, RenamedEventArgs e)
		{
			await this.sync.WaitAsync().ConfigureAwait(false);
			try
			{
				this.logger.Log($"File renamed from {e.OldFullPath} to {e.FullPath}");
				this.filesPendingWriteComplete.Add(e.FullPath);
				this.filesPendingRemoveEntry.Add(e.OldFullPath);
				this.SetUpPendingTimer();
			}
			finally
			{
				this.sync.Release();
			}
		}

		private async void OnFileDeleted(object sender, FileSystemEventArgs e)
		{
			await this.sync.WaitAsync().ConfigureAwait(false);
			try
			{
				this.logger.Log("Detected file removed: " + e.FullPath);
				this.filesPendingRemoveEntry.Add(e.FullPath);
				this.SetUpPendingTimer();
			}
			finally
			{
				this.sync.Release();
			}
		}

		private static bool IsFileLocked(string filePath)
		{
			FileStream? stream = null;

			try
			{
				stream = File.OpenRead(filePath);
			}
			catch (Exception)
			{
				return true;
			}
			finally
			{
				stream?.Close();
			}

			return false;
		}

		private void SetUpPendingTimer()
		{
			if (this.pendingCheckTimer == null)
			{
				this.pendingCheckTimer = new Timer
				{
					AutoReset = true,
					Interval = 500
				};

				this.pendingCheckTimer.Elapsed += OnPendingCheckTimerElapsed;
				this.pendingCheckTimer.Start();
			}
		}

		private async void OnPendingCheckTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
		{
			await this.sync.WaitAsync().ConfigureAwait(false);
			try
			{
				foreach (string file in this.filesPendingWriteComplete)
				{
					if (!IsFileLocked(file))
					{
						this.logger.Log(file + " is no longer locked");
						this.filesPendingWriteComplete.Remove(file);
						this.filesPendingSend.Add(file);
					}
				}

				if (this.filesPendingSend.Count > 0)
				{
					await this.watcherService.QueueInMainProcess(this.watchedFolder, this.filesPendingSend).ConfigureAwait(false);
					this.filesPendingSend.Clear();
				}

				WatcherStorage.RemoveEntries(WatcherDatabase.Connection, filesPendingRemoveEntry);
				this.filesPendingRemoveEntry.Clear();

				if (this.filesPendingWriteComplete.Count == 0)
				{
					this.pendingCheckTimer.Dispose();
					this.pendingCheckTimer = null;
				}
				else
				{
					this.pendingCheckTimer.Interval = 1000;
				}
			}
			finally
			{
				this.sync.Release();
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
}
