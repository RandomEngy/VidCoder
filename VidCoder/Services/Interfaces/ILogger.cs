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
		object LogLock { get; }
		List<LogEntry> LogEntries { get; }

		event EventHandler<EventArgs<LogEntry>> EntryLogged;
		event EventHandler Cleared;
		void ShowStatus(string message);
	}
}
