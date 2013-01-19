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
		private static WorkerLogger logger = new WorkerLogger();

		private static ManualResetEventSlim encodeComplete;
		private static System.Timers.Timer parentCheckTimer;

		static void Main(string[] args)
		{
			try
			{
				logger.Log("Worker process started.");

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

				Guid pipeGuid;
				if (!Guid.TryParse(args[1], out pipeGuid))
				{
					PrintUsage();
					return;
				}

				string pipeGuidString = args[1];

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
							logger.Log("Exception in parentCheckTimer.Elapsed: " + exception.ToString());
							throw;
						}
					};

				parentCheckTimer.Start();

				logger.Log("Started check timer.");

				ServiceHost host = null;
				try
				{
					host = new ServiceHost(
						typeof (HandBrakeEncoder),
						new Uri[]
							{
								new Uri("net.pipe://localhost/" + pipeGuidString)
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
					logger.Log("Encode complete has been signaled. Closing host.");

					host.Close();
				}
				catch (CommunicationException exception)
				{
					logger.Log("Exception when trying to establish pipe service: " + exception.ToString());
					if (host != null)
					{
						host.Abort();
					}
				}
				catch (TimeoutException exception)
				{
					logger.Log("Exception when trying to establish pipe service: " + exception.ToString());
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
				logger.Log("Exception in Main: " + exception.ToString());
				throw;
			}
			finally
			{
				logger.Dispose();
			}
		}

		public static WorkerLogger Logger
		{
			get
			{
				return logger;
			}
		}

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
