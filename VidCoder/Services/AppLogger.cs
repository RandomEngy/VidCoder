﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using HandBrake.Interop.Interop;
using Microsoft.AnyContainer;
using VidCoder.Model;
using VidCoderCommon;

namespace VidCoder.Services
{
	public class AppLogger : IAppLogger
	{
		private StreamWriter logFileWriter;
		private readonly IAppLogger parent;
		private readonly object disposeLock = new object();

		public event EventHandler<EventArgs<LogEntry>> EntryLogged;

		public AppLogger(IAppLogger parent, string baseFileName)
		{
			this.parent = parent;

			string logFolder = CommonUtilities.LogsFolder;

			string logFileNameAffix;
			if (baseFileName != null)
			{
				logFileNameAffix = baseFileName;
			}
			else
			{
				logFileNameAffix = "Combined";
			}

			this.LogPath = Path.Combine(logFolder, DateTimeOffset.Now.ToString("yyyy-MM-dd HH.mm.ss ") + logFileNameAffix + ".txt");

			this.TryCreateLogFileWriter();

			var initialEntry = new LogEntry
			{
				LogType = LogType.Message,
				Source = LogSource.VidCoder,
				Text = "# VidCoder " + Utilities.VersionString
			};

			this.AddEntry(initialEntry, logParent: false);
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

		public object LogLock { get; } = new object();

		/// <summary>
		/// Suspends the file writer. Should be called while holding the LogLock
		/// </summary>
		public void SuspendWriter()
		{
			this.logFileWriter?.Close();
			this.logFileWriter = null;
		}

		/// <summary>
		/// Resumes the file writer. Should be called while holding the LogLock
		/// </summary>
		public void ResumeWriter()
		{
			if (!this.Closed)
			{
				this.TryCreateLogFileWriter();
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

		public void ShowStatus(string message)
		{
			StaticResolver.Resolve<StatusService>().Show(message);
		}

		protected virtual void Dispose(bool disposing)
		{
			lock (this.disposeLock)
			lock (this.LogLock)
			{
				if (!this.Closed)
				{
					this.Closed = true;
					if (disposing)
					{
						this.logFileWriter?.Dispose();
						this.logFileWriter = null;
					}
				}
			}
		}

		public void Dispose()
		{
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		public bool Closed { get; private set; }

		public void AddEntry(LogEntry entry, bool logParent = true)
		{
			lock (this.disposeLock)
			{
				if (this.Closed)
				{
					return;
				}
			}

			lock (this.LogLock)
			{
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
	}
}
