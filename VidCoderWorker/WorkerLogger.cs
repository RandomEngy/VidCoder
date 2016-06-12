using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VidCoderCommon;
using VidCoderCommon.Services;

namespace VidCoderWorker
{
	public class WorkerLogger : ILogger
	{
		private readonly IHandBrakeEncoderCallback callback;

		public WorkerLogger(IHandBrakeEncoderCallback callback)
		{
			this.callback = callback;
		}

		public void Log(string message)
		{
			this.callback.OnMessageLogged(message);
		}

		public void LogError(string message)
		{
			this.callback.OnErrorLogged(message);
		}
	}
}
