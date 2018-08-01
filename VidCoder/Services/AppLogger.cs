using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using HandBrake.Interop.Interop;
using HandBrake.Interop.Interop.EventArgs;
using VidCoder.Model;

namespace VidCoder.Services
{
	public class AppLogger : IDisposable, IAppLogger
	{
		private List<LogEntry> logEntries = new List<LogEntry>();
		private StreamWriter logFile;
		private bool disposed;
		private IAppLogger parent;

		private object logLock = new object();
		private object disposeLock = new object();

		public event EventHandler<EventArgs<LogEntry>> EntryLogged;
		public event EventHandler Cleared;

		public AppLogger() : this(null, null)
		{
		}

		public AppLogger(IAppLogger parent, string baseFileName)
		{
			this.parent = parent;

			HandBrakeUtils.MessageLogged += this.OnMessageLogged;
			HandBrakeUtils.ErrorLogged += this.OnErrorLogged;

			string logFolder = Path.Combine(Utilities.AppFolder, "Logs");
			if (!Directory.Exists(logFolder))
			{
				Directory.CreateDirectory(logFolder);
			}

			string logFileNamePrefix;
			if (baseFileName != null)
			{
				logFileNamePrefix = baseFileName + " ";
			}
			else
			{
				logFileNamePrefix = string.Empty;
			}

			this.LogPath = Path.Combine(logFolder, logFileNamePrefix + DateTimeOffset.Now.ToString("yyyy-MM-dd HH.mm.ss") + ".txt");

			try
			{
				this.logFile = new StreamWriter(this.LogPath);
			}
			catch (PathTooLongException)
			{
				parent.LogError("Could not create logger for encode. File path was too long.");
				// We won't have an underlying log file.
			}
			catch (IOException exception)
			{
				this.LogError("Could not open file for logger." + Environment.NewLine + exception);
			}

			var initialEntry = new LogEntry
			{
				LogType = LogType.Message,
				Source = LogSource.VidCoder,
				Text = "## VidCoder " + Utilities.VersionString
			};

			this.AddEntry(initialEntry);
		}

		public string LogPath { get; set; }

		public object LogLock
		{
			get
			{
				return logLock;
			}
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

		public void Log(IEnumerable<LogEntry> entries)
		{
			foreach (var entry in entries)
			{
				this.AddEntry(entry);
			}
		}

		public void LogError(string message)
		{
			var entry = new LogEntry
			{
				LogType = LogType.Error,
				Source = LogSource.VidCoder,
				Text = "# " + message
			};

			this.AddEntry(entry);
		}

		public void LogWorker(string message, bool isError)
		{
			var entry = new LogEntry
			{
				LogType = isError ? LogType.Error : LogType.Message,
				Source = LogSource.VidCoderWorker,
				Text = "* " + message
			};

			this.AddEntry(entry);
		}

		public void ClearLog()
		{
			lock (this.LogLock)
			{
				this.LogEntries.Clear();
			}

			if (this.Cleared != null)
			{
				this.Cleared(this, EventArgs.Empty);
			}
		}

		public void ShowStatus(string message)
		{
			Ioc.Get<StatusService>().Show(message);
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
					this.logFile?.Close();
				}
			}
		}

		public void AddEntry(LogEntry entry, bool logParent = true)
		{
			lock (this.disposeLock)
			{
				if (this.disposed)
				{
					return;
				}
			}

			lock (this.LogLock)
			{
				this.LogEntries.Add(entry);
			}

			if (this.EntryLogged != null)
			{
				this.EntryLogged(this, new EventArgs<LogEntry>(entry));
			}

			try
			{
				this.logFile?.WriteLine(entry.Text);
				this.logFile?.Flush();

				if (this.parent != null && logParent)
				{
					this.parent.AddEntry(entry);
				}
			}
			catch (ObjectDisposedException)
			{
			}
		}

		private void OnMessageLogged(object sender, MessageLoggedEventArgs e)
		{
			var entry = new LogEntry
			{
				LogType = LogType.Message,
				Source = LogSource.HandBrake,
				Text = e.Message
			};

			// Parent will also get this message
			this.AddEntry(entry, logParent: false);
		}

		private void OnErrorLogged(object sender, MessageLoggedEventArgs e)
		{
			var entry = new LogEntry
			{
				LogType = LogType.Error,
				Source = LogSource.HandBrake,
				Text = "ERROR: " + e.Message
			};

			// Parent will also get this message
			this.AddEntry(entry, logParent: false);
		}
	}
}
