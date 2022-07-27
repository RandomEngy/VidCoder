using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VidCoderCommon.Model;
using VidCoderCommon.Services;
using VidCoderFileWatcher.Model;

namespace VidCoderFileWatcher.Services
{
	public class WatcherService : IWatcherCommands
	{
		private readonly IBasicLogger logger;

		public WatcherService(IBasicLogger logger)
		{
			this.logger = logger;
		}

		public void RefreshFromWatchedFolders()
		{
			this.logger.Log("Refreshing file list...");
			List<WatchedFolder> folders = WatcherStorage.GetWatchedFolders(WatcherDatabase.Connection);

			foreach(WatchedFolder folder in folders)
			{
				this.logger.Log("Folder: " + folder.Path);
			}
		}

		public void Ping()
		{
		}
	}
}
