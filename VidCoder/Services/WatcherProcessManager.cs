﻿using PipeMethodCalls;
using PipeMethodCalls.NetJson;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using VidCoder.Model;
using VidCoderCommon;
using VidCoderFileWatcher.Model;
using Timer = System.Timers.Timer;

namespace VidCoder.Services
{
	public class WatcherProcessManager : ReactiveObject
	{
		private const double ProcessPollIntervalMsec = 10000;
		private const string FileWatcherExecutableName = "VidCoderFileWatcher";

		/// <summary>
		/// The poll timer. Will be null if the the status is Stopped or when the status is Starting and the pipe connection is being established.
		/// Otherwise, will be running and firing Ping events.
		/// </summary>
		private Timer pollTimer;

		private PipeClient<IWatcherCommands> pipeClient;

		private static readonly string FileWatcherFolder = CommonUtilities.DebugMode ? CommonUtilities.ProgramFolder : CommonUtilities.LocalAppFolder;
		private static readonly string FileWatcherFullPath = Path.Combine(FileWatcherFolder, FileWatcherExecutableName + ".exe");

		private IProcesses processes;
		private readonly IAppLogger logger;

		/// <summary>
		/// The watcher process. Will be null iff the status is Stopped.
		/// </summary>
		private Process watcherProcess;

		public WatcherProcessManager(IProcesses processes, IAppLogger logger)
		{
			this.processes = processes;
			this.logger = logger;
			this.RefreshStatus();
			if (this.Status == WatcherProcessStatus.Stopped && Config.WatcherEnabled)
			{
				this.Start(refreshStatus: false);
			}
		}

		private WatcherProcessStatus status = Config.WatcherEnabled ? WatcherProcessStatus.Stopped : WatcherProcessStatus.Disabled;
		public WatcherProcessStatus Status
		{
			get { return this.status; }
			private set { this.RaiseAndSetIfChanged(ref this.status, value); }
		}

		public void Start(bool refreshStatus = true)
		{
			if (refreshStatus)
			{
				this.RefreshStatus();
			}

			if (this.Status == WatcherProcessStatus.Stopped || this.Status == WatcherProcessStatus.Disabled)
			{
				var startInfo = new ProcessStartInfo
				{
					FileName = FileWatcherFullPath,
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					UseShellExecute = false,
					CreateNoWindow = true
				};

				this.Status = WatcherProcessStatus.Starting;
				this.watcherProcess = Process.Start(startInfo);

				Task.Run(async () =>
				{
					// When the process writes out a line, its pipe server is ready.
					await this.watcherProcess.StandardOutput.ReadLineAsync();
					await this.ConnectToServiceAsync();
					this.Status = WatcherProcessStatus.Running;
					this.StartPolling();
				});
			}
		}

		public void Stop()
		{
			this.RefreshStatus();
			if (this.Status != WatcherProcessStatus.Stopped && this.Status != WatcherProcessStatus.Disabled)
			{
				if (this.pipeClient != null)
				{
					this.pipeClient.Dispose();
					this.pipeClient = null;
				}

				this.watcherProcess.Kill();
				this.SetStatusToStopped();
			}
		}

		public void RefreshStatus()
		{
			WatcherProcessStatus oldStatus = this.Status;

			if (this.Status == WatcherProcessStatus.Starting)
			{
				// Check if the process has exited
				if (this.watcherProcess.HasExited)
				{
					this.SetStatusToStopped();
				}
			}
			else
			{
				this.watcherProcess = this.GetRunningFileWatcher();
				if (this.watcherProcess != null && !this.watcherProcess.HasExited)
				{
					this.Status = WatcherProcessStatus.Running;
					if (oldStatus == WatcherProcessStatus.Stopped || oldStatus == WatcherProcessStatus.Disabled)
					{
						Task.Run(async () =>
						{
							await this.ConnectToServiceAsync();
							this.Status = WatcherProcessStatus.Running;
							this.StartPolling();
						});
					}
				}
				else
				{
					this.SetStatusToStopped();
				}
			}
		}

		private async Task ConnectToServiceAsync()
		{
			this.pipeClient = new PipeClient<IWatcherCommands>(new NetJsonPipeSerializer(), CommonUtilities.FileWatcherPipeName);
			await this.pipeClient.ConnectAsync();
			await this.pipeClient.InvokeAsync(commands => commands.Ping());
		}

		private void SetStatusToStopped()
		{
			this.Status = Config.WatcherEnabled ? WatcherProcessStatus.Stopped : WatcherProcessStatus.Disabled;
			this.StopPolling();
			this.watcherProcess = null;
			this.pipeClient = null;
		}

		private void StartPolling()
		{
			this.pollTimer = new Timer(ProcessPollIntervalMsec);
			this.pollTimer.Elapsed += this.PollTimerElapsed;

			this.pollTimer.Start();
		}

		private void StopPolling()
		{
			if (this.pollTimer != null)
			{
				this.pollTimer.Stop();
				this.pollTimer.Elapsed -= this.PollTimerElapsed;
				this.pollTimer = null;
			}
		}

		private async void PollTimerElapsed(object sender, ElapsedEventArgs e)
		{
			try
			{
				if (this.pipeClient != null)
				{
					var cts = new CancellationTokenSource(5000);
					await this.pipeClient.InvokeAsync(commands => commands.Ping(), cts.Token);
				}
			}
			catch (Exception exception)
			{
				this.logger.LogError("File watcher process is unresponsive." + Environment.NewLine + exception.ToString());
				this.logger.Log("Restarting file watcher.");

				this.Stop();
				this.Start();
			}
		}

		private Process GetRunningFileWatcher()
		{
			return this.processes.GetProcesses().FirstOrDefault(process =>
				process.ProcessName == FileWatcherExecutableName
				&& process.MainModule.FileName.Equals(FileWatcherFullPath, StringComparison.OrdinalIgnoreCase));
		}
	}
}
