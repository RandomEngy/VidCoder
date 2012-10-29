using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Timers;
using System.Diagnostics;
using System.Collections.Specialized;

namespace VidCoder.Services
{
	public class ProcessAutoPause : IProcessAutoPause
	{
		private const double ProcessPollIntervalMsec = 2000;

		private IProcesses processes;
		private ILogger logger;

		private HashSet<string> startingProcesses;

		private Timer pollTimer;
		private bool polling;
		private bool encoding;
		private bool autoPaused;

		private object syncLock = new object();

		public ProcessAutoPause(IProcesses processes, ILogger logger)
		{
			this.processes = processes;
			this.logger = logger;
		}

		public event EventHandler PauseEncoding;

		public event EventHandler ResumeEncoding;

		public void ReportStart()
		{
			var startingProcessNames = from p in this.processes.GetProcesses()
									   select p.ProcessName;

			this.startingProcesses = new HashSet<string>(startingProcessNames);

			this.encoding = true;
			this.autoPaused = false;
			this.StartPolling();
		}

		public void ReportStop()
		{
			lock (this.syncLock)
			{
				this.encoding = false;
				this.StopPolling();
			}
		}

		public void ReportPause()
		{
			lock (this.syncLock)
			{
				// When manually pausing, we never resume automatically so we don't need to poll.
				this.StopPolling();
			}
		}

		public void ReportResume()
		{
			lock (this.syncLock)
			{
				if (this.autoPaused)
				{
					// If forced to resume, disable auto-pausing
					this.StopPolling();
				}
				else
				{
					// If resuming normally, start polling again
					this.StartPolling();
				}
			}
		}

		private void StartPolling()
		{
			this.pollTimer = new Timer(ProcessPollIntervalMsec);
			this.pollTimer.Elapsed += new ElapsedEventHandler(this.PollTimerElapsed);

			this.polling = true;
			this.pollTimer.Start();
		}

		private void StopPolling()
		{
			this.polling = false;
			this.pollTimer.Stop();
		}

		private void PollTimerElapsed(object sender, ElapsedEventArgs e)
		{
			lock (this.syncLock)
			{
				if (this.encoding && this.polling)
				{
					string autoPauseProcess = this.AutoPauseProcess();

					if (this.autoPaused)
					{
						if (autoPauseProcess == null)
						{
							this.autoPaused = false;
							this.logger.Log("Automatically resuming, processes are gone.");

							if (this.ResumeEncoding != null)
							{
								this.ResumeEncoding(this, new EventArgs());
							}
						}
					}
					else
					{
						if (autoPauseProcess != null)
						{
							this.autoPaused = true;
							this.logger.Log("Automatically pausing, process detected: " + autoPauseProcess);
							if (this.PauseEncoding != null)
							{
								this.PauseEncoding(this, new EventArgs());
							}
						}
					}
				}
			}
		}

		private string AutoPauseProcess()
		{
			Process[] processes = this.processes.GetProcesses();
			List<string> autoPauseList = CustomConfig.AutoPauseProcesses;

			if (autoPauseList != null)
			{
				foreach (string autoPauseProcessName in autoPauseList)
				{
					// If the process is present now but not when we started, pause it.
					if (processes.Count(p => p.ProcessName == autoPauseProcessName) > 0 &&
						!this.startingProcesses.Contains(autoPauseProcessName))
					{
						return autoPauseProcessName;
					}
				}
			}

			return null;
		}
	}
}
