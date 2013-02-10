using System;
using System.Collections.Generic;
using VidCoder.Model;

namespace VidCoder.Services
{
	public interface ILogger : IDisposable
	{
		void ClearLog();
		void Log(string message);
		void LogError(string message);
		void AddEntry(LogEntry entry);
		object LogLock { get; }
		List<LogEntry> LogEntries { get; }
		string LogPath { get; }

		event EventHandler<EventArgs<LogEntry>> EntryLogged;
		event EventHandler Cleared;
		void ShowStatus(string message);
		void LogWorker(string message, bool isError);
		void Log(IEnumerable<LogEntry> entries);
	}
}
