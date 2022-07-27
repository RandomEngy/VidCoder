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
	/// Logs for a support process like installer or windows service.
	/// </summary>
	public class SupportLogger : IBasicLogger
	{
		private StreamWriter logWriter;

		public SupportLogger(string type)
		{
			string logFolder = @"C:\Users\david\AppData\Roaming\VidCoder-Beta\Logs";
			//string logFolder = CommonUtilities.LogsFolder;
			string logFileName = DateTimeOffset.Now.ToString("yyyy-MM-dd HH.mm.ss ") + type + ".txt";

			string logFilePath = Path.Combine(logFolder, logFileName);

			this.logWriter = new StreamWriter(logFilePath);
		}

		public void Log(string message)
		{
			this.logWriter.WriteLine(message);
			this.logWriter.Flush();
		}

		public void Close()
		{
			this.logWriter.Close();
		}
	}
}
