using System;
using System.Collections.Generic;
using VidCoder.Model;
using VidCoderCommon.Services;

namespace VidCoder.Services
{
	public interface IAppLogger : ILogger, IDisposable
	{
		void AddEntry(LogEntry entry, bool logParent = true);
		object LogLock { get; }
		string LogPath { get; set; }

		event EventHandler<EventArgs<LogEntry>> EntryLogged;
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

		bool Closed { get; }
	}
}
