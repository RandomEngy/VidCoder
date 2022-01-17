﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using VidCoderCommon;

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
			string logsFolder = CommonUtilities.LogsFolder;

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


			foreach (DirectoryInfo handBrakeTempDirectory in handBrakeTempDirectories)
			{
				bool deleteFolder = false;
				try
				{
					string processIdString = handBrakeTempDirectory.Name.Substring(3);
					var process = Process.GetProcessById(int.Parse(processIdString, CultureInfo.InvariantCulture));
					if (process.HasExited)
					{
						deleteFolder = true;
					}
				}
				catch (ArgumentException)
				{
					// If there is no process with that ID running anymore, attempt cleanup on the temp files.
					deleteFolder = true;
				}
				catch (Exception)
				{
					// Do not attempt cleanup.
				}

				if (deleteFolder)
				{
					try
					{
						FileUtilities.DeleteDirectory(handBrakeTempDirectory.FullName);
					}
					catch (Exception)
					{
						// If cleanup fails, will be attempted next time.
					}
				}
			}
		}
	}
}
