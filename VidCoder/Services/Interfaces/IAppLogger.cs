using System;
using System.Collections.Generic;
using VidCoder.Model;
using VidCoderCommon.Services;

namespace VidCoder.Services
{
	public interface IAppLogger : ILogger, IDisposable
	{
		void ClearLog();
		void AddEntry(LogEntry entry, bool logParent = true);
		object LogLock { get; }
		List<LogEntry> LogEntries { get; }
		string LogPath { get; }

		event EventHandler<EventArgs<LogEntry>> EntryLogged;
		event EventHandler Cleared;
		void ShowStatus(string message);
		void LogWorker(string message, bool isError);
		void Log(IEnumerable<LogEntry> entries);

		/// <summary>
		/// Suspends the file writer. Should be called while holding the LogLock
		/// </summary>
		void SuspendWriter();

		/// <summary>
		/// Resumes the file writer. Should be called while holding the LogLock
		/// </summary>
		void ResumeWriter();
	}
}
