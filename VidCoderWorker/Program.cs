using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VidCoderWorker
{
	using System.Diagnostics;
	using System.ServiceModel;
	using System.Threading;

	class Program
	{
		private static ManualResetEvent encodeComplete;

		static void Main(string[] args)
		{
			using (var host = new ServiceHost(
				typeof(HandBrakeEncoder),
				new Uri[]
					{
						new Uri("net.pipe://localhost")
					}))
			{
				int processId = Process.GetCurrentProcess().Id;

				//host.AddServiceEndpoint(
				//    typeof(IHandBrakeEncoder),
				//    new NetNamedPipeBinding(),
				//    "VidCoderWorker");

				host.AddServiceEndpoint(
					typeof(IHandBrakeEncoder),
					new NetNamedPipeBinding(),
					"VidCoderWorker_" + processId);

				host.Open();

				encodeComplete = new ManualResetEvent(false);
				Console.WriteLine("Service is available.");
				encodeComplete.WaitOne();

				host.Close();
			}
		}

		public static void SignalEncodeComplete()
		{
			encodeComplete.Set();
		}
	}
}
