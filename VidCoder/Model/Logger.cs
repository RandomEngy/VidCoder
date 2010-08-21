using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HandBrake.Interop;
using System.IO;
using VidCoder.ViewModel;

namespace VidCoder.Model
{
	public class Logger : IDisposable
	{
		private StreamWriter logFile;
		private bool disposed;
		private StringBuilder logBuilder;
		private object logLock = new object();

		public Logger()
		{
			HandBrakeInstance.MessageLogged += this.OnMessageLogged;
			HandBrakeInstance.ErrorLogged += this.OnErrorLogged;

			string logFolder = Path.Combine(Utilities.AppFolder, "Logs");
			if (!Directory.Exists(logFolder))
			{
				Directory.CreateDirectory(logFolder);
			}

			string logFilePath = Path.Combine(logFolder, DateTime.UtcNow.ToString("yyyy-MM-dd HH-mm-ss") + ".txt");
			this.logFile = new StreamWriter(logFilePath);

			this.logBuilder = new StringBuilder();
			this.RecordMessage("## VidCoder " + Utilities.CurrentVersion);
		}

		public string LogText
		{
			get
			{
				lock (this.logLock)
				{
					return logBuilder.ToString();
				}
			}
		}

		public void Log(string message)
		{
			this.RecordMessage(message);
		}

		public void ClearLog()
		{
			this.logBuilder.Clear();
		}

		/// <summary>
		/// Frees any resources associated with this object.
		/// </summary>
		public void Dispose()
		{
			if (!disposed)
			{
				this.disposed = true;
				this.logFile.Close();
			}
		}

		private void OnMessageLogged(object sender, MessageLoggedEventArgs e)
		{
			this.RecordMessage(e.Message);
		}

		private void OnErrorLogged(object sender, MessageLoggedEventArgs e)
		{
			this.RecordMessage("ERROR: " + e.Message);
		}

		private void RecordMessage(string message)
		{
			lock (this.logLock)
			{
				this.logBuilder.Append(message);
				this.logBuilder.Append(Environment.NewLine);
			}

			var logWindow = WindowManager.FindWindow(typeof(LogViewModel)) as LogViewModel;
			if (logWindow != null)
			{
				logWindow.RefreshLogs();
			}

			this.logFile.WriteLine(message);
			this.logFile.Flush();
		}
	}
}
