using PipeMethodCalls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using VidCoder.Model;
using VidCoder.Services;
using VidCoderCommon;
using VidCoderCommon.Model;
using Timer = System.Timers.Timer;

namespace VidCoder
{
	public abstract class RemoteProxyBase<TWork, TCallback> : IHandBrakeWorkerCallback
		where TWork : class, IHandBrakeWorker
		where TCallback : class, IHandBrakeWorkerCallback
	{
		// Ping interval (6s) longer than timeout (5s) so we don't have two overlapping pings
		private const double PingTimerIntervalMs = 6000;

		private const int ConnectionRetryIntervalMs = 1000;
		private const int ConnectionRetries = 10;

		private const string PipeNamePrefix = "VidCoderWorker.";

#if DEBUG_REMOTE
		private static readonly TimeSpan LastUpdateTimeoutWindow = TimeSpan.FromSeconds(200000);
#else
		private static readonly TimeSpan LastUpdateTimeoutWindow = TimeSpan.FromSeconds(20);
#endif

		private string pipeName;

		private bool crashLogged;

		private Process worker;

		// Timer that pings the worker process periodically to see if it's still alive.
		private Timer pingTimer;

		protected PipeClientWithCallback<TWork, TCallback> Client;

		//protected IHandBrakeWorker Channel { get; set; }

		protected IAppLogger Logger	{ get; set; }

		protected DateTimeOffset LastWorkerCommunication { get; set; }

		// Lock to take before interacting with the worker process or changing encoding state.
		protected SemaphoreSlim ProcessLock { get; } = new SemaphoreSlim(1, 1);

		protected bool Running { get; set; }

		protected abstract HandBrakeWorkerAction Action { get; }

		protected abstract TCallback CallbackInstance { get; }

		// Executes the given proxy operation, stopping the encode and logging if a communication problem occurs.
		protected async Task ExecuteProxyOperationAsync(Func<Task> action, string operationName)
		{
			try
			{
				await action().ConfigureAwait(false);
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

		protected abstract void OnOperationEnd(bool error);

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
					this.Logger.LogError($"Operation '{operationName}' failed: " + exception);
					this.LogAndClearWorkerMessages();
				}
			}
			else
			{
				this.Logger.LogError($"Operation '{operationName}' failed, worker process has crashed.");
			}

			this.EndOperation(error: true);
		}

		private async Task<bool> ConnectToPipeAsync()
		{
			for (int i = 0; i < ConnectionRetries; i++)
			{
				try
				{
					this.Client = new PipeClientWithCallback<TWork, TCallback>(this.pipeName, () => this.CallbackInstance);

					// With extended logging, log the pipe messages.
					if (Config.LogVerbosity >= 2)
					{
						this.Client.SetLogger(message => 
							{
								this.Logger.Log(message);
#if DEBUG
								System.Diagnostics.Debug.WriteLine(message);
#endif
							}
						);
					}

					await this.Client.ConnectAsync().ConfigureAwait(false);

					await this.Client.InvokeAsync(x => x.Ping());
					return true;
				}
				catch (Exception)
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

				await Task.Delay(ConnectionRetryIntervalMs);
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

		protected async Task RunOperationAsync(Expression<Action<TWork>> actionExpression)
		{
			this.Running = true;

			this.LastWorkerCommunication = DateTimeOffset.UtcNow;
			this.pipeName = PipeNamePrefix + Guid.NewGuid();
			var startInfo = new ProcessStartInfo(
				"VidCoderWorker.exe",
				Process.GetCurrentProcess().Id.ToString(CultureInfo.InvariantCulture) + " " + this.pipeName + " " + this.Action);
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
			await this.ProcessLock.WaitAsync();
			try
			{
				await this.ExecuteProxyOperationAsync(async () =>
				{
					connectionSucceeded = await this.ConnectToPipeAsync();
					if (!connectionSucceeded)
					{
						return;
					}

					await this.Client.InvokeAsync(x => x.SetUpWorker(
						Config.LogVerbosity,
						Config.PreviewCount,
						Config.EnableLibDvdNav,
						Config.MinimumTitleLengthSeconds,
						Config.CpuThrottlingFraction,
						FileUtilities.OverrideTempFolder ? FileUtilities.TempFolderOverride : null));

					await this.Client.InvokeAsync(actionExpression);
				}, "Connect");
			}
			finally
			{
				this.ProcessLock.Release();
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

			this.pingTimer.Elapsed += async (o, e) =>
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
						CancellationTokenSource cancellationTokenSource = new CancellationTokenSource(5000);
						await this.Client.InvokeAsync(x => x.Ping(), cancellationTokenSource.Token);

						lock (this.ProcessLock)
						{
							this.LastWorkerCommunication = DateTimeOffset.UtcNow;
						}
					}
					catch (Exception exception)
					{
#if !DEBUG
						this.HandlePingError(exception);
#endif
					}
				}
			};

			this.pingTimer.Start();
		}

		protected async Task ExecuteWorkerCallAsync(Expression<Action<TWork>> action, string operationName)
		{
			await this.ProcessLock.WaitAsync();
			try
			{
				if (this.Client != null)
				{
					await this.ExecuteProxyOperationAsync(() => this.Client.InvokeAsync(action), operationName);
				}
				else
				{
					this.Logger.LogError($"Could not execute operation '{operationName}', Channel does not exist.");
				}
			}
			finally
			{
				this.ProcessLock.Release();
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
