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
			try
			{
				if (args.Length < 2)
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

				PipeGuidString = args[1];
				Guid pipeGuid;
				if (!Guid.TryParse(PipeGuidString, out pipeGuid))
				{
					PrintUsage();
					return;
				}

				parentCheckTimer = new System.Timers.Timer();
				parentCheckTimer.Interval = ParentCheckInterval;
				parentCheckTimer.AutoReset = true;
				parentCheckTimer.Elapsed += (o, e) =>
					{
						try
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
						}
						catch (Exception exception)
						{
							WorkerLogger.Log("Exception in parentCheckTimer.Elapsed: " + exception.ToString(), isError: true);
							throw;
						}
					};

				parentCheckTimer.Start();

				ServiceHost host = null;
				try
				{
					host = new ServiceHost(
						typeof (HandBrakeEncoder),
						new Uri[]
							{
								new Uri("net.pipe://localhost/" + PipeGuidString)
							});
					string pipeName = "VidCoderWorker";

					host.AddServiceEndpoint(
						typeof (IHandBrakeEncoder),
						new NetNamedPipeBinding(),
						pipeName);

					host.Open();

					encodeComplete = new ManualResetEventSlim(false);
					Console.WriteLine("Service state is " + host.State + " on pipe " + pipeName);
					encodeComplete.Wait();

					host.Close();
				}
				catch (CommunicationException exception)
				{
					WorkerLogger.Log("Exception when trying to establish pipe service: " + exception, isError: true);
					if (host != null)
					{
						host.Abort();
					}
				}
				catch (TimeoutException exception)
				{
					WorkerLogger.Log("Exception when trying to establish pipe service: " + exception, isError: true);
					if (host != null)
					{
						host.Abort();
					}
				}
				catch (Exception)
				{
					if (host != null)
					{
						host.Abort();
					}

					throw;
				}
			}
			catch (Exception exception)
			{
				WorkerLogger.Log("Exception in Main: " + exception, isError: true);
				throw;
			}
		}

		public static string PipeGuidString { get; set; }

		public static void SignalEncodeComplete()
		{
			encodeComplete.Set();
		}

		private static void PrintUsage()
		{
			Console.WriteLine("Must be called with process ID of parent and the pipe GUID.");
		}

		private static bool ProcessExists(int id)
		{
			Process[] processes1 = Process.GetProcessesByName("VidCoder");
			Process[] processes2 = Process.GetProcessesByName("VidCoder.vshost");
			return processes1.Any(p => p.Id == id) || processes2.Any(p => p.Id == id);
		}
	}
}
