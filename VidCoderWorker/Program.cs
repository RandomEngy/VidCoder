using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VidCoderWorker
{
	using System.Diagnostics;
	using System.ServiceModel;
	using System.Threading;
	using HandBrake.Interop;

	class Program
	{
		private const double ParentCheckInterval = 5000;

		private static ManualResetEventSlim encodeComplete;
		private static System.Timers.Timer parentCheckTimer;

		static void Main(string[] args)
		{
			if (args.Length < 1)
			{
				PrintUsage();
				return;
			}

			int parentProcId;
			if (!int.TryParse(args[0], out parentProcId))
			{
				PrintUsage();
				return;
			}

			parentCheckTimer = new System.Timers.Timer();
			parentCheckTimer.Interval = ParentCheckInterval;
			parentCheckTimer.AutoReset = true;
			parentCheckTimer.Elapsed += (o, e) =>
			    {
					if (!ProcessExists(parentProcId))
					{
						// If we couldn't stop the process, just wait until next tick. May have not started yet or may
						// already be in the process of closing.
						if (HandBrakeEncoder.CurrentEncoder != null && HandBrakeEncoder.CurrentEncoder.StopEncodeIfPossible())
						{
							// If we are able to stop the encode, we will do so. Cleanup should
							// happen with the encode complete callback.
							Console.WriteLine("Parent no longer exists, stopping encode.");
						}
					}
			    };

			parentCheckTimer.Start();

			using (var host = new ServiceHost(
				typeof(HandBrakeEncoder),
				new Uri[]
					{
						new Uri("net.pipe://localhost")
					}))
			{
				int processId = Process.GetCurrentProcess().Id;
				string pipeName = "VidCoderWorker_" + processId;

				host.AddServiceEndpoint(
					typeof(IHandBrakeEncoder),
					new NetNamedPipeBinding(),
					pipeName);

				host.Open();

				encodeComplete = new ManualResetEventSlim(false);
				Console.WriteLine("Service state is " + host.State + " on pipe " + pipeName);
				encodeComplete.Wait();

				host.Close();
			}
		}

		public static void SignalEncodeComplete()
		{
			encodeComplete.Set();
		}

		private static void PrintUsage()
		{
			Console.WriteLine("Must be called with process ID of parent.");
		}

		private static bool ProcessExists(int id)
		{
			Process[] processes1 = Process.GetProcessesByName("VidCoder");
			Process[] processes2 = Process.GetProcessesByName("VidCoder.vshost");
			return processes1.Any(p => p.Id == id) || processes2.Any(p => p.Id == id);
		}
	}
}
