using PipeMethodCalls;
using PipeMethodCalls.NetJson;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using VidCoder.Model;
using VidCoder.Services;
using VidCoderCommon;
using VidCoderCommon.Model;
using Timer = System.Timers.Timer;

namespace VidCoder
{
	public abstract class RemoteProxyBase<TWork, TCallback> : IHandBrakeWorkerCallback, IDisposable
		where TWork : class, IHandBrakeWorker
		where TCallback : class, IHandBrakeWorkerCallback
	{
		// Ping interval (11s) longer than timeout (10s) so we don't have two overlapping pings
		private const double PingTimerIntervalMs = 11000;

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

		private IDisposable processPrioritySubscription;
		private IDisposable cpuThrottlingSubscription;

		// Timer that pings the worker process periodically to see if it's still alive.
		private Timer pingTimer;

		protected PipeClientWithCallback<TWork, TCallback> Client;

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
		protected void EndOperation(VCEncodeResultCode result)
		{
			if (this.Running)
			{
				this.Running = false;

				// Inform caller that the operation has completed.
				this.OnOperationEnd(result);
			}
		}

		/// <summary>
		/// They are done with the proxy object. Tear down the worker process.
		/// </summary>
		public void Dispose()
		{
			this.CleanUpWorkerProcess();
			this.pingTimer?.Dispose();
			this.processPrioritySubscription?.Dispose();
			this.cpuThrottlingSubscription?.Dispose();
		}

		protected abstract void OnOperationEnd(VCEncodeResultCode result);

		private void HandleWorkerCommunicationError(Exception exception, string operationName)
		{
			if (!this.Running)
			{
				this.Logger.LogError($"Operation '{operationName}' failed, encode is not running.");

				return;
			}

			// Figure out what went wrong and populate result code
			VCEncodeResultCode result;
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

					result = VCEncodeResultCode.ErrorHandBrakeProcessCrashed;
				}
				else
				{
					this.Logger.LogError($"Operation '{operationName}' failed: " + exception);
					this.LogAndClearWorkerMessages();
					result = VCEncodeResultCode.ErrorProcessCommunication;
				}
			}
			else
			{
				this.Logger.LogError($"Operation '{operationName}' failed, worker process has crashed.");
				result = VCEncodeResultCode.ErrorHandBrakeProcessCrashed;
			}

			// Go clean up the worker process. If there is another job we can start fresh.
			this.CleanUpWorkerProcess();

			this.EndOperation(result);
		}

		private async void CleanUpWorkerProcess()
		{
			// First save copies of the worker process and client pipe
			Process workerToCleanUp = this.worker;
			this.worker = null;

			var clientToCleanUp = this.Client;
			this.Client = null;

			// Then go clean them up asynchronously
			if (clientToCleanUp != null)
			{
				// Tell the worker to tear down
				try
				{
					await clientToCleanUp.InvokeAsync(x => x.TearDownWorker()).ConfigureAwait(false);

					// Wait a bit for the pipe to close
					using (CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(3)))
					{
						await clientToCleanUp.WaitForRemotePipeCloseAsync(cts.Token).ConfigureAwait(false);
					}
				}
				catch (Exception)
				{
				}

				// Dispose the communication pipe.
				clientToCleanUp.Dispose();
				clientToCleanUp = null;
			}

			// If the worker is still running, kill it.
			if (workerToCleanUp != null && !workerToCleanUp.HasExited)
			{
				// Give a bit of time for the worker process to shut down
				await Task.Delay(TimeSpan.FromSeconds(3)).ConfigureAwait(false);

				if (!workerToCleanUp.HasExited)
				{
					try
					{
						workerToCleanUp.Kill();
					}
					catch (Exception exception)
					{
						this.Logger.LogError("Could not kill unresponsive worker process: " + exception);
					}
				}
			}

			this.worker = null;
		}

		private async Task<bool> ConnectToPipeAsync()
		{
			for (int i = 0; i < ConnectionRetries; i++)
			{
				try
				{
					this.Client = new PipeClientWithCallback<TWork, TCallback>(new NetJsonPipeSerializer(), this.pipeName, () => this.CallbackInstance);

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
			Task.Run(async () =>
			{
				await this.ProcessLock.WaitAsync().ConfigureAwait(false);
				try
				{
					// If the ping times out something may be wrong. If the worker process has exited or we haven't heard an
					// update from it in 10 seconds we assume something is wrong.
					if (this.worker == null || this.worker.HasExited || this.LastWorkerCommunication < DateTimeOffset.UtcNow - LastUpdateTimeoutWindow)
					{
						this.HandleWorkerCommunicationError(exception, "Ping");
					}
				}
				finally
				{
					this.ProcessLock.Release();
				}
			});
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

			bool prepareSucceeded = await this.PrepareWorkerProcessAsync();
			if (!prepareSucceeded)
			{
				return;
			}

			await this.ProcessLock.WaitAsync();
			try
			{
				await this.ExecuteProxyOperationAsync(async () =>
				{
					await this.Client.InvokeAsync(actionExpression);
				}, "Invoke");
			}
			finally
			{
				this.ProcessLock.Release();
			}
		}

		private async Task<bool> PrepareWorkerProcessAsync()
		{
			if (this.Client != null)
			{
				// We've already got a worker process ready to go.
				return true;
			}

			this.LastWorkerCommunication = DateTimeOffset.UtcNow;
			this.pipeName = PipeNamePrefix + Guid.NewGuid();
			var startInfo = new ProcessStartInfo
			{
				FileName = "VidCoderWorker.exe",
				Arguments = Process.GetCurrentProcess().Id.ToString(CultureInfo.InvariantCulture) + " " + this.pipeName + " " + this.Action,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
				CreateNoWindow = true
			};
			this.worker = Process.Start(startInfo);
			this.worker.PriorityClass = CustomConfig.WorkerProcessPriority;

			// When the process writes out a line, its pipe server is ready and can be contacted for
			// work. Reading line blocks until this happens.
			this.Logger.Log("Worker ready: " + this.worker.StandardOutput.ReadLine());
			bool connectionSucceeded = false;

			this.Logger.Log("Connecting to process " + this.worker.Id + " on pipe " + this.pipeName);
			await this.ProcessLock.WaitAsync().ConfigureAwait(false);
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
				}, "Connect");
			}
			finally
			{
				this.ProcessLock.Release();
			}

			if (!connectionSucceeded)
			{
				this.EndOperation(VCEncodeResultCode.ErrorProcessCommunication);
				return false;
			}

			this.pingTimer = new Timer
			{
				AutoReset = true,
				Interval = PingTimerIntervalMs
			};

			this.pingTimer.Elapsed += async (o, e) =>
			{
				await this.ProcessLock.WaitAsync().ConfigureAwait(false);
				try
				{
					if (!this.Running)
					{
						return;
					}
				}
				finally
				{
					this.ProcessLock.Release();
				}

				if (this.Running)
				{
					try
					{
						CancellationTokenSource cancellationTokenSource = new CancellationTokenSource(10000);
						await this.Client.InvokeAsync(x => x.Ping(), cancellationTokenSource.Token);

						await this.ProcessLock.WaitAsync().ConfigureAwait(false);
						try
						{
							this.LastWorkerCommunication = DateTimeOffset.UtcNow;
						}
						finally
						{
							this.ProcessLock.Release();
						}
					}
#if DEBUG
					catch
					{
					}
#else
					catch (Exception exception)
					{
						this.HandlePingError(exception);
					}
#endif
				}
			};

			this.pingTimer.Start();

			this.processPrioritySubscription = Config.Observables.WorkerProcessPriority.Skip(1).Subscribe(async _ =>
			{
				try
				{
					if (this.worker != null && this.Running)
					{
						this.worker.PriorityClass = CustomConfig.WorkerProcessPriority;
					}
				}
				catch
				{
					// Could not dynamically modify worker process priority. Ignore.
				}
			});

			this.cpuThrottlingSubscription = Config.Observables.CpuThrottlingFraction.Skip(1).Subscribe(async cpuThrottlingFraction =>
			{
				try
				{
					if (this.worker != null && this.Running)
					{
						await this.ExecuteWorkerCallAsync(w => w.UpdateCpuThrottling(cpuThrottlingFraction), "UpdateCpuThrottling");
					}
				}
				catch
				{
					// Could not dynamically update CPU throttling. Ignore.
				}
			});

			return true;
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
