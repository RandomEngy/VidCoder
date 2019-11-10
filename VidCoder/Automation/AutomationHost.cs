using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PipeMethodCalls;
using VidCoderCommon;
using VidCoderCLI;

namespace VidCoder.Automation
{
	public static class AutomationHost
	{
		private static PipeServer<IVidCoderAutomation> server;
		private static CancellationTokenSource cancellationTokenSource;

		public static async void StartListening()
		{
			cancellationTokenSource = new CancellationTokenSource();

			try
			{
				while (true)
				{
					using (server = new PipeServer<IVidCoderAutomation>(
						"VidCoderAutomation" + (CommonUtilities.Beta ? "Beta" : string.Empty),
						() => new VidCoderAutomation()))
					{
						await server.WaitForConnectionAsync().ConfigureAwait(false);
						await server.WaitForRemotePipeCloseAsync().ConfigureAwait(false);
					}
				}
			}
			catch (Exception)
			{
			}
		}

		public static void StopListening()
		{
			cancellationTokenSource?.Cancel();
		}
	}
}
