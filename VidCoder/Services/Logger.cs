using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HandBrake.Interop;
using System.IO;
using VidCoder.Model;
using VidCoder.ViewModel;

namespace VidCoder.Services
{
	public class Logger : IDisposable, ILogger
	{
		private List<LogEntry> logEntries = new List<LogEntry>();
		private StreamWriter logFile;
		private bool disposed;
		private object logLock = new object();
		private object disposeLock = new object();

		public event EventHandler<EventArgs<LogEntry>> EntryLogged;
		public event EventHandler Cleared;

		public Logger()
		{
			HandBrakeUtils.MessageLogged += this.OnMessageLogged;
			HandBrakeUtils.ErrorLogged += this.OnErrorLogged;

			string logFolder = Path.Combine(Utilities.AppFolder, "Logs");
			if (!Directory.Exists(logFolder))
			{
				Directory.CreateDirectory(logFolder);
			}

			string logFilePath = Path.Combine(logFolder, DateTime.UtcNow.ToString("yyyy-MM-dd HH-mm-ss") + ".txt");
			this.logFile = new StreamWriter(logFilePath);

			var initialEntry = new LogEntry
			{
				LogType = LogType.Message,
				Source = LogSource.VidCoder,
				Text = "## VidCoder " + Utilities.CurrentVersion + " (" + Utilities.Architecture + ")"
			};

			this.AddEntry(initialEntry);
		}

		public List<LogEntry> LogEntries
		{
			get
			{
				return this.logEntries;
			}
		}

		public void Log(string message)
		{
			string prefix = string.IsNullOrEmpty(message) ? string.Empty : "# ";

			var entry = new LogEntry
			{
			    LogType = LogType.Message,
				Source = LogSource.VidCoder,
				Text = prefix + message
			};

			this.AddEntry(entry);
		}

		public void ClearLog()
		{
			this.LogEntries.Clear();

			if (this.Cleared != null)
			{
				this.Cleared(this, new EventArgs());
			}
		}

		/// <summary>
		/// Frees any resources associated with this object.
		/// </summary>
		public void Dispose()
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

		private void AddEntry(LogEntry entry)
		{
			lock (this.disposeLock)
			{
				if (this.disposed)
				{
					return;
				}
			}

			lock (this.logLock)
			{
				this.LogEntries.Add(entry);
			}

			if (this.EntryLogged != null)
			{
				this.EntryLogged(this, new EventArgs<LogEntry>(entry));
			}

			this.logFile.WriteLine(entry.Text);
			this.logFile.Flush();
		}

		private void OnMessageLogged(object sender, MessageLoggedEventArgs e)
		{
			var entry = new LogEntry
			{
				LogType = LogType.Message,
				Source = LogSource.HandBrake,
				Text = e.Message
			};

			this.AddEntry(entry);
		}

		private void OnErrorLogged(object sender, MessageLoggedEventArgs e)
		{
			var entry = new LogEntry
			{
				LogType = LogType.Error,
				Source = LogSource.HandBrake,
				Text = "ERROR: " + e.Message
			};

			this.AddEntry(entry);
		}
	}
}
