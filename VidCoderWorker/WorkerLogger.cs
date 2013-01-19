using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VidCoderWorker
{
	using System.Diagnostics;
	using System.IO;

	public class WorkerLogger
	{
		private const bool Enabled = false;

		private StreamWriter logFile;
		private bool disposed;
		private object logLock = new object();
		private object disposeLock = new object();

		public WorkerLogger()
		{
			if (Enabled)
			{
				string logFolder = Path.Combine(
					Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
					"VidCoder",
					"Logs",
					"Worker");
				if (!Directory.Exists(logFolder))
				{
					Directory.CreateDirectory(logFolder);
				}

				string logFilePath = Path.Combine(logFolder, DateTime.UtcNow.ToString("yyyy-MM-dd_HH.mm.ss") + "_PID_" + Process.GetCurrentProcess().Id + ".txt");
				this.logFile = new StreamWriter(logFilePath);
			}
		}

		public object LogLock
		{
			get
			{
				return logLock;
			}
		}

		public void Log(string message)
		{
			if (Enabled)
			{
				lock (this.disposeLock)
				{
					if (this.disposed)
					{
						return;
					}
				}

				this.logFile.WriteLine("[" + DateTimeOffset.UtcNow.ToString("o") + "] " + message);
				this.logFile.Flush();
			}
		}

		/// <summary>
		/// Frees any resources associated with this object.
		/// </summary>
		public void Dispose()
		{
			if (Enabled)
			{
				lock (this.disposeLock)
				{
					if (!this.disposed)
					{
						this.disposed = true;
						this.logFile.Close();
					}
				}
			}
		}
	}
}
