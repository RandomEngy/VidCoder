using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using PipeMethodCalls;
using VidCoderCommon;
using VidCoderCommon.Model;
using VidCoderCommon.Utilities;

namespace VidCoderWorker
{
	class Program
	{
		private const double ParentCheckInterval = 5000;

		private static SemaphoreSlim encodeComplete;
		private static System.Timers.Timer parentCheckTimer;

		static async Task Main(string[] args)
		{
			try
			{
				////Debugger.Launch();

				if (args.Length < 3)
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

				JsonSettings.SetDefaultSerializationSettings();

				PipeName = args[1];

				var action = (HandBrakeWorkerAction)Enum.Parse(typeof(HandBrakeWorkerAction), args[2]);

				parentCheckTimer = new System.Timers.Timer();
				parentCheckTimer.Interval = ParentCheckInterval;
				parentCheckTimer.AutoReset = true;
				parentCheckTimer.Elapsed += (o, e) =>
					{
						try
						{
							if (!ProcessExists(parentProcId))
							{
								if (action == HandBrakeWorkerAction.Encode && HandBrakeEncodeWorker.CurrentWorker != null && HandBrakeEncodeWorker.CurrentWorker.StopEncodeIfPossible())
								{
									// If we are able to stop the encode, we will do so. Cleanup should
									// happen with the encode complete callback.
									Console.WriteLine("Parent no longer exists, stopping encode.");
								}
							}
						}
						catch (Exception exception)
						{
							WorkerErrorLogger.LogError("Exception in parentCheckTimer. Elapsed: " + exception.ToString(), isError: true);
							throw;
						}
					};

				parentCheckTimer.Start();

				StartService(action);

				encodeComplete = new SemaphoreSlim(0, 1);
				await encodeComplete.WaitAsync().ConfigureAwait(false);
			}
			catch (Exception exception)
			{
				WorkerErrorLogger.LogError("Exception in Main: " + exception, isError: true);
				throw;
			}
		}

		private static async void StartService(HandBrakeWorkerAction action)
		{
			try
			{
				IPipeServer server;
				if (action == HandBrakeWorkerAction.Encode)
				{
					PipeServerWithCallback<IHandBrakeEncodeWorkerCallback, IHandBrakeEncodeWorker> encodeServer = null;
					Lazy<IHandBrakeEncodeWorker> lazyEncodeWorker = new Lazy<IHandBrakeEncodeWorker>(() => new HandBrakeEncodeWorker(encodeServer.Invoker));
					encodeServer = new PipeServerWithCallback<IHandBrakeEncodeWorkerCallback, IHandBrakeEncodeWorker>(PipeName, () => lazyEncodeWorker.Value);
#if DEBUG
					encodeServer.SetLogger(message => System.Diagnostics.Debug.WriteLine(message));
#endif
					server = encodeServer;
				}
				else if (action == HandBrakeWorkerAction.Scan)
				{
					PipeServerWithCallback<IHandBrakeScanWorkerCallback, IHandBrakeScanWorker> scanServer = null;
					Lazy<IHandBrakeScanWorker> lazyScanWorker = new Lazy<IHandBrakeScanWorker>(() => new HandBrakeScanWorker(scanServer.Invoker));
					scanServer = new PipeServerWithCallback<IHandBrakeScanWorkerCallback, IHandBrakeScanWorker>(PipeName, () => lazyScanWorker.Value);
#if DEBUG
					scanServer.SetLogger(message => System.Diagnostics.Debug.WriteLine(message));
#endif
					server = scanServer;
				}
				else
				{
					throw new ArgumentException("Unrecognized action: " + action, nameof(action));
				}

				Task connectionTask = server.WaitForConnectionAsync();

				// Write a line to let the client know we are ready for connections
				Console.WriteLine($"Pipe '{PipeName}' is open");

				await connectionTask.ConfigureAwait(false);
				await server.WaitForRemotePipeCloseAsync().ConfigureAwait(false);
			}
			catch (Exception exception)
			{
				WorkerErrorLogger.LogError("Exception in StartService: " + exception, isError: true);
			}
		}

		public static string PipeName { get; set; }

		public static void SignalEncodeComplete()
		{
			encodeComplete.Release();
		}

		private static void PrintUsage()
		{
			Console.WriteLine("Usage: VidCoderWorker <parentProcId> <pipeName> <action>");
		}

		private static bool ProcessExists(int id)
		{
			Process[] processes1 = Process.GetProcessesByName("VidCoder");
			Process[] processes2 = Process.GetProcessesByName("VidCoder.vshost");
			return processes1.Any(p => p.Id == id) || processes2.Any(p => p.Id == id);
		}
	}
}
