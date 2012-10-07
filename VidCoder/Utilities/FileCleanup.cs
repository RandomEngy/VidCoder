using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace VidCoder
{
	public static class FileCleanup
	{
		public static void CleanPreviewFileCache()
		{
			try
			{
				var cacheFolder = new DirectoryInfo(Utilities.ImageCacheFolder);
				DirectoryInfo[] processDirectories = cacheFolder.GetDirectories();

				int ourPid = Process.GetCurrentProcess().Id;

				Process[] processes = Process.GetProcesses();
				var pidSet = new HashSet<int>(processes.Select(p => p.Id));

				foreach (DirectoryInfo processDirectory in processDirectories)
				{
					int directoryPid;
					if (int.TryParse(processDirectory.Name, out directoryPid))
					{
						if (directoryPid == ourPid || !pidSet.Contains(directoryPid))
						{
							try
							{
								Utilities.DeleteDirectory(processDirectory.FullName);
							}
							catch (IOException)
							{
								// Ignore failed cleanup. Move on to the next folder.
							}
						}
					}
				}
			}
			catch (IOException)
			{
				// Ignore failed cleanup. Will get done some other time.
			}
		}

		public static void CleanOldLogs()
		{
			string logsFolder = Path.Combine(Utilities.AppFolder, "Logs");
			string[] logFiles = Directory.GetFiles(logsFolder);

			// Files are named by date so are in chronological order. Keep the most recent 10 files.
			for (int i = 0; i < logFiles.Length - 10; i++)
			{
				try
				{
					File.Delete(logFiles[i]);
				}
				catch (IOException)
				{
					// Just ignore failed deletes. They'll get cleaned up some other time.
				}
			}
		}
	}
}
