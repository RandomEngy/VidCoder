using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using HandBrake.Interop.Interop;
using HandBrake.Interop.Interop.EventArgs;
using Microsoft.AnyContainer;
using VidCoder.Model;

namespace VidCoder.Services
{
	public class AppLogger : IDisposable, IAppLogger
	{
		private StreamWriter logFileWriter;
		private bool disposed;
		private IAppLogger parent;

		private object logLock = new object();
		private object disposeLock = new object();

		public event EventHandler<EventArgs<LogEntry>> EntryLogged;
		public event EventHandler Cleared;

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

			string logFileNameAffix;
			if (baseFileName != null)
			{
				logFileNameAffix = baseFileName;
			}
			else
			{
				logFileNameAffix = "VidCoderApplication";
			}

			this.LogPath = Path.Combine(logFolder, DateTimeOffset.Now.ToString("yyyy-MM-dd HH.mm.ss ") + logFileNameAffix + ".txt");

			this.TryCreateLogFileWriter();

			var initialEntry = new LogEntry
			{
				LogType = LogType.Message,
				Source = LogSource.VidCoder,
				Text = "# VidCoder " + Utilities.VersionString
			};

			this.AddEntry(initialEntry);
		}

		private void TryCreateLogFileWriter()
		{
			try
			{
				this.logFileWriter = new StreamWriter(new FileStream(this.LogPath, FileMode.Append, FileAccess.Write));
			}
			catch (PathTooLongException)
			{
				this.parent?.LogError("Could not create logger for encode. File path was too long.");
				// We won't have an underlying log file.
			}
			catch (IOException exception)
			{
				this.LogError("Could not open file for logger." + Environment.NewLine + exception);
			}
		}

		public string LogPath { get; set; }

		public object LogLock
		{
			get
			{
				return logLock;
			}
		}

		/// <summary>
		/// Suspends the file writer. Should be called while holding the LogLock
		/// </summary>
		public void SuspendWriter()
		{
			this.logFileWriter?.Close();
		}

		/// <summary>
		/// Resumes the file writer. Should be called while holding the LogLock
		/// </summary>
		public void ResumeWriter()
		{
			this.TryCreateLogFileWriter();
		}

		public List<LogEntry> LogEntries { get; } = new List<LogEntry>();

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
				Text = "ERROR: " + message
			};

			this.AddEntry(entry);
		}

		public void LogWorker(string message, bool isError)
		{
			var logType = isError ? LogType.Error : LogType.Message;

			var entry = new LogEntry
			{
				LogType = logType,
				Source = LogSource.VidCoderWorker,
				Text = LogEntryClassificationUtilities.GetEntryPrefix(logType, LogSource.VidCoderWorker) + message
			};

			this.AddEntry(entry);
		}

		public void ClearLog()
		{
			lock (this.LogLock)
			{
				this.LogEntries.Clear();
			}

			this.Cleared?.Invoke(this, EventArgs.Empty);
		}

		public void ShowStatus(string message)
		{
			StaticResolver.Resolve<StatusService>().Show(message);
		}

		/// <summary>
		/// Frees any resources associated with this object.
		/// </summary>
		public void Dispose()
		{
			lock (this.disposeLock)
			lock (this.LogLock)
			{
				if (!this.disposed)
				{
					this.disposed = true;
					this.logFileWriter?.Close();
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
				// See if log line needs to have continuation markers added
				string[] entryLines = entry.Text.Split(new[] {"\r\n", "\r", "\n"}, StringSplitOptions.None);
				if (entryLines.Length > 1)
				{
					var builder = new StringBuilder(entryLines[0]);
					for (int i = 1; i < entryLines.Length; i++)
					{
						var entryLine = entryLines[i];

						builder.AppendLine();
						builder.Append("｜");
						builder.Append(entryLine);
					}

					entry.Text = builder.ToString();
				}

				this.LogEntries.Add(entry);

				this.EntryLogged?.Invoke(this, new EventArgs<LogEntry>(entry));

				try
				{
					this.logFileWriter?.WriteLine(entry.Text);
					this.logFileWriter?.Flush();

					if (this.parent != null && logParent)
					{
						this.parent.AddEntry(entry);
					}
				}
				catch (ObjectDisposedException)
				{
				}
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
