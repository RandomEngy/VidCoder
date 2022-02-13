using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VidCoderCommon.Services
{
	public class ElevatedSetupLogger
	{
		private StreamWriter logWriter;

		public ElevatedSetupLogger(string type)
		{
			string logFolder = CommonUtilities.LogsFolder;
			string logFileName = DateTimeOffset.Now.ToString("yyyy-MM-dd HH.mm.ss ") + type + ".txt";

			string logFilePath = Path.Combine(logFolder, logFileName);

			this.logWriter = new StreamWriter(logFilePath);
		}

		public void Log(string message)
		{
			this.logWriter.WriteLine(message);
		}

		public void Close()
		{
			this.logWriter.Close();
		}
	}
}
