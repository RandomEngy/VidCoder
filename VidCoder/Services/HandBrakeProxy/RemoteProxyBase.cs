using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VidCoder.Model;
using VidCoder.Services;
using VidCoderCommon;
using Timer = System.Timers.Timer;

namespace VidCoder
{
	public abstract class RemoteProxyBase
	{
		// Ping interval (6s) longer than timeout (5s) so we don't have two overlapping pings
		private const double PingTimerIntervalMs = 6000;

		private const int ConnectionRetryIntervalMs = 1000;
		private const int ConnectionRetries = 10;

		private const string PipeNamePrefix = "VidCoderWorker.";

#if DEBUG_REMOTE
		private static readonly TimeSpan OperationTimeout = TimeSpan.FromSeconds(500000);
		private static readonly TimeSpan PipeTimeout = TimeSpan.FromSeconds(100000);
		private static readonly TimeSpan LastUpdateTimeoutWindow = TimeSpan.FromSeconds(200000);
#else
		private static readonly TimeSpan OperationTimeout = TimeSpan.FromSeconds(5);
		private static readonly TimeSpan PipeTimeout = TimeSpan.FromSeconds(10);
		private static readonly TimeSpan LastUpdateTimeoutWindow = TimeSpan.FromSeconds(20); // Do not check in
#endif

		private DuplexChannelFactory<IHandBrakeWorker> pipeFactory;
		private string pipeName;

		private bool crashLogged;

		private Process worker;

		// Timer that pings the worker process periodically to see if it's still alive.
		private Timer pingTimer;

		protected IHandBrakeWorker Channel { get; set; }

		protected IAppLogger Logger	{ get; set; }

		protected DateTimeOffset LastWorkerCommunication { get; set; }

		// Lock to take before interacting with the worker process or changing encoding state.
		protected object ProcessLock { get; } = new object();

		protected bool Running { get; set; }

		// Executes the given proxy operation, stopping the encode and logging if a communication problem occurs.
		protected void ExecuteProxyOperation(Action action, string operationName)
		{
			try
			{
				action();
			}
			catch (Exception exception)
			{
				this.HandleWorkerCommunicationError(exception, operationName);
			}
		}

		// Ends the scan or encode with the given status.
		protected void EndOperation(bool error)
		{
			if (this.Running)
			{
				this.OnOperationEnd(error);

				this.pingTimer?.Dispose();

				// If the encode failed and the worker is still running, kill it.
				if (error && this.worker != null && !this.worker.HasExited)
				{
					try
					{
						this.worker.Kill();
					}
					catch (InvalidOperationException exception)
					{
						this.Logger.LogError("Could not kill unresponsive worker process: " + exception);
					}
					catch (Win32Exception exception)
					{
						this.Logger.LogError("Could not kill unresponsive worker process: " + exception);
					}
				}

				this.Running = false;
			}
		}

		// TODO: Is this only needed for encoding?
		protected abstract void OnOperationEnd(bool error);

		// TODO: Is this only needed for encoding?
		public abstract void StopAndWait();

		private void HandleWorkerCommunicationError(Exception exception, string operationName)
		{
			if (!this.Running)
			{
				this.Logger.LogError($"Operation '{operationName}' failed, encode is not running.");

				return;
			}

			if (!this.crashLogged)
			{
				if (this.worker.HasExited)
				{
					List<LogEntry> logs = this.GetWorkerMessages();
					this.ClearWorkerMessages();

					int errors = logs.Count(l => l.LogType == LogType.Error);

					if (errors > 0)
					{
						this.Logger.LogError($"Operation '{operationName}' failed. Worker process crashed. Details:");
						this.Logger.Log(logs);
					}
					else
					{
						this.Logger.LogError($"Operation '{operationName}' failed. Worker process exited unexpectedly with code " + this.worker.ExitCode + ". This may be due to a HandBrake engine crash.");
					}
				}
				else
				{
					this.Logger.LogError($"Operation '{operationName}' failed. Lost communication with worker process: " + exception);
					this.LogAndClearWorkerMessages();
				}
			}
			else
			{
				this.Logger.LogError($"Operation '{operationName}' failed, worker process has crashed.");
			}

			this.EndOperation(error: true);
		}

		private bool ConnectToPipe()
		{
			for (int i = 0; i < ConnectionRetries; i++)
			{
				try
				{
					var binding = new NetNamedPipeBinding
					{
						OpenTimeout = PipeTimeout,
						CloseTimeout = PipeTimeout,
						SendTimeout = PipeTimeout,
						ReceiveTimeout = PipeTimeout
					};

					this.pipeFactory = new DuplexChannelFactory<IHandBrakeWorker>(
						this,
						binding,
						new EndpointAddress("net.pipe://localhost/" + this.pipeName));

					this.Channel = this.pipeFactory.CreateChannel();
					this.Channel.Ping();

					return true;
				}
				catch (EndpointNotFoundException)
				{
				}

				if (this.worker.HasExited)
				{
					List<LogEntry> logs = this.GetWorkerMessages();
					int errors = logs.Count(l => l.LogType == LogType.Error);

					this.Logger.LogError("Worker exited before a connection could be established.");
					if (errors > 0)
					{
						this.Logger.Log(logs);
					}

					return false;
				}

				Thread.Sleep(ConnectionRetryIntervalMs);
			}

			this.Logger.LogError("Connection to worker failed after " + ConnectionRetries + " retries. Unable to find endpoint.");
			this.LogAndClearWorkerMessages();

			return false;
		}

		private void HandlePingError(Exception exception)
		{
			lock (this.ProcessLock)
			{
				// If the ping times out something may be wrong. If the worker process has exited or we haven't heard an
				// update from it in 10 seconds we assume something is wrong.
				if (this.worker.HasExited || this.LastWorkerCommunication < DateTimeOffset.UtcNow - LastUpdateTimeoutWindow)
				{
					this.HandleWorkerCommunicationError(exception, "Ping");
				}
			}
		}

		private void LogAndClearWorkerMessages()
		{
			List<LogEntry> logs = this.GetWorkerMessages();
			if (logs.Count > 0)
			{
				this.Logger.Log(logs);
				this.ClearWorkerMessages();
			}
		}

		protected void StartOperation(Action<IHandBrakeWorker> action)
		{
			var task = new Task(() =>
			{
				this.LastWorkerCommunication = DateTimeOffset.UtcNow;
				this.pipeName = PipeNamePrefix + Guid.NewGuid().ToString();
				var startInfo = new ProcessStartInfo(
					"VidCoderWorker.exe",
					Process.GetCurrentProcess().Id.ToString(CultureInfo.InvariantCulture) + " " + this.pipeName);
				startInfo.RedirectStandardOutput = true;
				startInfo.UseShellExecute = false;
				startInfo.CreateNoWindow = true;
				this.worker = Process.Start(startInfo);
				this.worker.PriorityClass = CustomConfig.WorkerProcessPriority;

				// When the process writes out a line, its pipe server is ready and can be contacted for
				// work. Reading line blocks until this happens.
				this.Logger.Log("Worker ready: " + this.worker.StandardOutput.ReadLine());
				bool connectionSucceeded = false;

				this.Logger.Log("Connecting to process " + this.worker.Id + " on pipe " + this.pipeName);
				lock (this.ProcessLock)
				{
					this.ExecuteProxyOperation(() =>
					{
						connectionSucceeded = this.ConnectToPipe();
						if (!connectionSucceeded)
						{
							return;
						}

						this.Channel.SetUpWorker(
							Config.LogVerbosity,
							Config.PreviewCount,
							Config.EnableLibDvdNav,
							Config.MinimumTitleLengthSeconds,
							Config.CpuThrottlingFraction,
							FileUtilities.OverrideTempFolder ? FileUtilities.TempFolderOverride : null);

						action(this.Channel);

						// After we do StartEncode (which can take a while), switch the timeout down to normal level to do pings
						var contextChannel = (IContextChannel)this.Channel;
						contextChannel.OperationTimeout = OperationTimeout;
					}, "Connect");
				}

				if (!connectionSucceeded)
				{
					this.EndOperation(error: true);
					return;
				}

				this.pingTimer = new Timer
				{
					AutoReset = true,
					Interval = PingTimerIntervalMs
				};

				this.pingTimer.Elapsed += (o, e) =>
				{
					lock (this.ProcessLock)
					{
						if (!this.Running)
						{
							return;
						}
					}

					if (this.Running)
					{
						try
						{
							this.Channel.Ping();

							lock (this.ProcessLock)
							{
								this.LastWorkerCommunication = DateTimeOffset.UtcNow;
							}
						}
						catch (CommunicationException exception)
						{
							this.HandlePingError(exception);
						}
						catch (TimeoutException exception)
						{
							this.HandlePingError(exception);
						}
					}
				};

				this.pingTimer.Start();
			});

			this.Running = true;
			task.Start();
		}

		protected void ExecuteWorkerCall(Action<IHandBrakeWorker> action, string operationName)
		{
			lock (this.ProcessLock)
			{
				if (this.Channel != null)
				{
					this.ExecuteProxyOperation(() => action(this.Channel), operationName);
				}
				else
				{
					this.Logger.LogError($"Could not execute operation '{operationName}', Channel does not exist.");
				}
			}
		}

		private List<LogEntry> GetWorkerMessages()
		{
			var messages = new List<LogEntry>();

			string workerLogFile = Path.Combine(Utilities.WorkerLogsFolder, worker.Id + ".txt");

			if (File.Exists(workerLogFile))
			{
				string[] lines = File.ReadAllLines(workerLogFile);
				foreach (string line in lines)
				{
					if (!string.IsNullOrWhiteSpace(line))
					{
						// TODO: distinguish errors from non errors. For now the only thing we log is errors since if everything is working
						// we can use normal channels.
						messages.Add(new LogEntry
						{
							LogType = LogType.Error,
							Source = LogSource.VidCoderWorker,
							Text = line
						});
					}
				}
			}

			return messages;
		}

		private void ClearWorkerMessages()
		{
			var workerLogsFolder = Utilities.WorkerLogsFolder;
			if (Directory.Exists(workerLogsFolder))
			{
				Directory.Delete(workerLogsFolder, recursive: true);
			}
		}
		public void OnMessageLogged(string message)
		{
			var entry = new LogEntry
			{
				LogType = LogType.Message,
				Source = LogSource.HandBrake,
				Text = message
			};

			this.Logger.AddEntry(entry);
		}

		public void OnVidCoderMessageLogged(string message)
		{
			var entry = new LogEntry
			{
				LogType = LogType.Message,
				Source = LogSource.VidCoderWorker,
				Text = message
			};

			this.Logger.AddEntry(entry);
		}

		public void OnErrorLogged(string message)
		{
			var entry = new LogEntry
			{
				LogType = LogType.Error,
				Source = LogSource.HandBrake,
				Text = message
			};

			this.Logger.AddEntry(entry);
		}

		public void OnException(string exceptionString)
		{
			this.Logger.LogError("Worker process crashed. Please report this error so it can be fixed in the future:" + Environment.NewLine + exceptionString);
			this.crashLogged = true;
		}
	}
}
