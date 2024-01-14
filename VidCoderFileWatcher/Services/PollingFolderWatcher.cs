using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using VidCoderCommon.Model;
using VidCoderFileWatcher.Model;

namespace VidCoderFileWatcher.Services;

/// <summary>
/// Watches a folder using polling.
/// </summary>
public class PollingFolderWatcher : IDisposable
{
	private bool isDisposed;

	private readonly WatcherService watcherService;
	private readonly IList<WatchedFolder> watchedFolders;
	private readonly IBasicLogger logger;

	private readonly System.Timers.Timer timer;

	private bool checkingFolders = false;

	public PollingFolderWatcher(WatcherService watcherService, IList<WatchedFolder> watchedFolders, IBasicLogger logger)
	{
		this.watcherService = watcherService;
		this.watchedFolders = watchedFolders;
		this.logger = logger;

		int pollIntervalSeconds = DatabaseConfig.Get<int>("WatcherPollIntervalSeconds", 5, WatcherDatabase.Connection);

		this.timer = new System.Timers.Timer(pollIntervalSeconds * 1000);
		this.timer.Elapsed += this.OnTimerElapsed;
		this.timer.Start();
	}

	private async void OnTimerElapsed(object? sender, ElapsedEventArgs e)
	{
		if (this.checkingFolders)
		{
			// Don't start another check while a previous one is still in progress.
			return;
		}

		var folderEnumerator = new FolderEnumerator(this.watchedFolders, this.watcherService, this.logger, logVerbose: false);

		this.checkingFolders = true;
		await folderEnumerator.CheckFolders();
		this.checkingFolders = false;
	}

	protected virtual void Dispose(bool disposing)
	{
		if (!this.isDisposed)
		{
			if (disposing)
			{
				this.timer.Dispose();
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
