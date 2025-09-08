using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VidCoderWorker;

using System.Diagnostics;
using System.IO;

public static class WorkerErrorLogger
{
	private static object logLock = new();
	private static bool initialized;

	public static void LogError(string message, bool isError)
	{
		lock (logLock)
		{
			var workerLogsDirectory = Path.Combine(Path.GetTempPath(), "VidCoderWorkerLogs");
			if (!Directory.Exists(workerLogsDirectory))
			{
				Directory.CreateDirectory(workerLogsDirectory);
			}

			var logFileName = Path.Combine(workerLogsDirectory, Process.GetCurrentProcess().Id + ".txt");

			if (!initialized)
			{
				if (File.Exists(logFileName))
				{
					File.Delete(logFileName);
				}

				initialized = true;
			}

			using (var writer = new StreamWriter(logFileName, append: true))
			{
				writer.WriteLine("[" + DateTimeOffset.UtcNow.ToString("o") + "] " + message);
			}
		}
	}
}
