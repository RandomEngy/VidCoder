using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VidCoderCommon.Model;

namespace VidCoderCommon.Services
{
	/// <summary>
	/// Logs for a support process like installer or watcher.
	/// </summary>
	public class SupportLogger : IBasicLogger
	{
		private StreamWriter logWriter;

		private object sync = new object();

		public SupportLogger(string type)
		{
			string logFolder = CommonUtilities.LogsFolder;
			string logFileName = DateTimeOffset.Now.ToString("yyyy-MM-dd HH.mm.ss ") + type + ".txt";

			string logFilePath = Path.Combine(logFolder, logFileName);

			this.logWriter = new StreamWriter(logFilePath);
		}

		public void Log(string message)
		{
			string messageText = FormattableString.Invariant($"[{DateTimeOffset.Now.ToString("HH:mm:ss")}] {message}");

			lock (this.sync)
			{
				this.logWriter.WriteLine(messageText);
				this.logWriter.Flush();
			}
		}

		public void Close()
		{
			lock (this.sync)
			{
				this.logWriter.Close();
			}
		}
	}
}
