using System;
namespace VidCoder.Services
{
	public interface ILogger : IDisposable
	{
		void ClearLog();
		void Log(string message);
		string LogText { get; }
	}
}
