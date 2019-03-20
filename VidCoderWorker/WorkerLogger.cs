using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PipeMethodCalls;
using VidCoderCommon;
using VidCoderCommon.Services;

namespace VidCoderWorker
{
	public class WorkerLogger<TCallback> : ILogger
		where TCallback : class, IHandBrakeWorkerCallback
	{
		private readonly IPipeInvoker<TCallback> callbackInvoker;

		public WorkerLogger(IPipeInvoker<TCallback> callbackInvoker)
		{
			this.callbackInvoker = callbackInvoker;
		}

		public async void Log(string message)
		{
			try
			{
				await this.callbackInvoker.InvokeAsync(c => c.OnMessageLogged(message));
			}
			catch (Exception)
			{
			}
		}

		public async void LogError(string message)
		{
			try
			{
				await this.callbackInvoker.InvokeAsync(c => c.OnErrorLogged(message));
			}
			catch (Exception)
			{
			}
		}
	}
}
