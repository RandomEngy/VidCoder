using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

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
								FileUtilities.DeleteDirectory(processDirectory.FullName);
							}
							catch (Exception)
							{
								// Ignore failed cleanup. Move on to the next folder.
							}
						}
					}
				}
			}
			catch (Exception)
			{
				// Ignore failed cleanup. Will get done some other time.
			}
		}

		public static void CleanOldLogs()
		{
			string logsFolder = Path.Combine(Utilities.AppFolder, "Logs");

			if (!Directory.Exists(logsFolder))
			{
				return;
			}

			// Get all the log files
			var logFolderInfo = new DirectoryInfo(logsFolder);
			FileInfo[] logFiles = logFolderInfo.GetFiles("*.txt");

			// Delete logs older than 30 days
			foreach (FileInfo file in logFiles)
			{
				if (file.LastWriteTime < DateTime.Now.AddDays(-30))
				{
					try
					{
						File.Delete(file.FullName);
					}
					catch (IOException)
					{
						// Just ignore failed deletes. They'll get cleaned up some other time.
					}
					catch (UnauthorizedAccessException)
					{
						// Just ignore failed deletes. They'll get cleaned up some other time.
					}
				}
			}
		}

		public static void CleanHandBrakeTempFiles()
		{
			DirectoryInfo tempDirectoryInfo = new DirectoryInfo(Path.GetTempPath());
			DirectoryInfo[] handBrakeTempDirectories = tempDirectoryInfo.GetDirectories("hb.*");

			if (handBrakeTempDirectories.Length == 0)
			{
				return;
			}

			Regex regex = new Regex(@"^hb\.(?<processId>\d+)$");

			foreach (DirectoryInfo handBrakeTempDirectory in handBrakeTempDirectories)
			{
				Match match = regex.Match(handBrakeTempDirectory.Name);
				if (match.Success)
				{
					string processIdString = match.Groups["processId"].Captures[0].Value;
					try
					{
						var process = Process.GetProcessById(int.Parse(processIdString));
					}
					catch (ArgumentException)
					{
						// If there is no process with that ID running anymore, attempt cleanup on the temp files.

						try
						{
							FileUtilities.DeleteDirectory(handBrakeTempDirectory.FullName);
						}
						catch (Exception)
						{
							// If cleanup fails, will be attempted next time.
						}
					}
					catch (Exception)
					{
						// Do not attempt cleanup.
					}
				}
			}
		}
	}
}
