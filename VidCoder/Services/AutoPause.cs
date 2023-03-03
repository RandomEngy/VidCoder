using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Timers;
using System.Diagnostics;
using System.Collections.Specialized;
using Microsoft.AnyContainer;
using VidCoder.ViewModel;
using System.IO;
using VidCoder.Resources;

namespace VidCoder.Services;

public class AutoPause : IAutoPause
{
	private const double ProcessPollIntervalMsec = 5000;

	private readonly IProcesses processes;
	private readonly IAppLogger logger;
	private readonly StatusService statusService;
	private HashSet<string> startingProcesses;

	private Timer pollTimer;
	private AutoPauseState state = AutoPauseState.NoEncode;
	private AutoPauseReason autoPauseReason;
	private bool lowDiskSpaceCheckFailureLogged = false;

	private object syncLock = new object();

	private enum AutoPauseState
	{
		NoEncode,
		EncodeRunningNormally,
		EncodeRunningForced,
		ManuallyPaused,
		AutoPaused
	}

	private enum AutoPauseReason
	{
		LowBattery,
		LowDiskSpace,
		ProcessPresent
	}

	public AutoPause(IProcesses processes, IAppLogger logger, StatusService statusService)
	{
		this.processes = processes;
		this.logger = logger;
		this.statusService = statusService;
	}

	public event EventHandler PauseEncoding;

	public event EventHandler ResumeEncoding;

	public void ReportStart()
	{
		lock (this.syncLock)
		{
			var startingProcessNames = from p in this.processes.GetProcesses()
									   select p.ProcessName;

			this.startingProcesses = new HashSet<string>(startingProcessNames);

			this.state = AutoPauseState.EncodeRunningNormally;
			this.StartPolling();
		}
	}

	public void ReportStop()
	{
		lock (this.syncLock)
		{
			this.state = AutoPauseState.NoEncode;
			this.StopPolling();
		}
	}

	public void ReportPause()
	{
		lock (this.syncLock)
		{
			this.state = AutoPauseState.ManuallyPaused;

			// When manually pausing, we never resume automatically so we don't need to poll.
			this.StopPolling();
		}
	}

	public void ReportResume()
	{
		lock (this.syncLock)
		{
			if (this.state == AutoPauseState.AutoPaused)
			{
				// If forced to resume, disable auto-pausing
				this.StopPolling();

				this.state = AutoPauseState.EncodeRunningForced;
			}
			else
			{
				// If resuming normally, start polling again
				this.StartPolling();

				this.state = AutoPauseState.EncodeRunningNormally;
			}
		}
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

	private void PollTimerElapsed(object sender, ElapsedEventArgs e)
	{
		lock (this.syncLock)
		{
			if (this.state != AutoPauseState.EncodeRunningNormally && this.state != AutoPauseState.AutoPaused)
			{
				// If we are in an invalid state, bail.
				return;
			}

			bool shouldBePausedForLowBattery = this.ShouldBePausedForLowBattery();
			bool shouldBePausedForLowDiskSpace = this.ShouldBePausedForLowDiskSpace();
			string autoPauseProcess = this.GetProcessToAutoPauseOn();

			if (this.state == AutoPauseState.EncodeRunningNormally)
			{
				// Encode is running normally. See if we need to auto-pause
				if (shouldBePausedForLowBattery)
				{
					this.InvokeAutoPause(MainRes.AutoPauseLowBattery, AutoPauseReason.LowBattery);
				}
				else if (shouldBePausedForLowDiskSpace)
				{
					this.InvokeAutoPause(string.Format(MainRes.AutoPauseLowDiskSpace, Config.AutoPauseLowDiskSpaceGb), AutoPauseReason.LowDiskSpace);
				}
				else if (autoPauseProcess != null)
				{
					this.InvokeAutoPause(string.Format(MainRes.AutoPauseProcessDetected, autoPauseProcess), AutoPauseReason.ProcessPresent);
				}
			}
			else if (this.state == AutoPauseState.AutoPaused)
			{
				if (!shouldBePausedForLowBattery && !shouldBePausedForLowDiskSpace && autoPauseProcess == null)
				{
					this.state = AutoPauseState.EncodeRunningNormally;
					string resumeMessage = GetResumeReasonMessage(this.autoPauseReason);
					this.logger.Log(resumeMessage);
					this.statusService.Show(resumeMessage);
					SystemSleepManagement.PreventSleep();
					this.ResumeEncoding?.Invoke(this, EventArgs.Empty);
				}
			}
		}
	}

	private bool ShouldBePausedForLowBattery()
	{
		if (!Config.AutoPauseLowBattery)
		{
			return false;
		}

		SystemPowerStatus.PowerState state = SystemPowerStatus.PowerState.GetPowerState();

		if (state == null || state.BatteryFlag == SystemPowerStatus.BatteryFlag.NoSystemBattery || state.BatteryFlag == SystemPowerStatus.BatteryFlag.Unknown)
		{
			return false; // Only run if we have a battery.
		}

		if (state.ACLineStatus == SystemPowerStatus.ACLineStatus.Offline && (state.BatteryFlag == SystemPowerStatus.BatteryFlag.Low || state.BatteryFlag == SystemPowerStatus.BatteryFlag.Critical))
		{
			SystemSleepManagement.AllowSleep();
			return true;
		}

		return false;
	}

	private bool ShouldBePausedForLowDiskSpace()
	{
		if (!Config.AutoPauseLowDiskSpace)
		{
			return false;
		}

		var processingService = StaticResolver.Resolve<ProcessingService>();

		var drives = new HashSet<string>();
		foreach (EncodeJobViewModel encodeJobViewModel in processingService.EncodingJobs)
		{
			try
			{
				string drive = Path.GetPathRoot(encodeJobViewModel.Job.FinalOutputPath);
				if (!string.IsNullOrEmpty(drive) && !drive.StartsWith(@"\") && !drives.Contains(drive))
				{
					drives.Add(drive);
				}
			}
			catch (Exception exception)
			{
				if (!this.lowDiskSpaceCheckFailureLogged)
				{
					this.logger.LogError("Could not get drive root for " + encodeJobViewModel.Job.FinalOutputPath + Environment.NewLine + exception);
					this.lowDiskSpaceCheckFailureLogged = true;
				}
			}
		}

		foreach (string drive in drives)
		{
			try
			{
				DriveInfo driveInfo = new DriveInfo(drive);
				if (driveInfo.AvailableFreeSpace < (long)Config.AutoPauseLowDiskSpaceGb * 1024 * 1024 * 1024)
				{
					return true;
				}
			}
			catch (Exception exception)
			{
				if (!this.lowDiskSpaceCheckFailureLogged)
				{
					this.logger.LogError("Could not get drive info for " + drive + Environment.NewLine + exception);
					this.lowDiskSpaceCheckFailureLogged = true;
				}
			}
		}

		return false;
	}

	private void InvokeAutoPause(string message, AutoPauseReason reason)
	{
		this.state = AutoPauseState.AutoPaused;
		this.autoPauseReason = reason;
		this.logger.Log(message);
		this.statusService.Show(message);
		this.PauseEncoding?.Invoke(this, EventArgs.Empty);
	}

	private static string GetResumeReasonMessage(AutoPauseReason pauseReason)
	{
		switch (pauseReason)
		{
			case AutoPauseReason.LowBattery:
				return MainRes.AutoPauseResumeBattery;
			case AutoPauseReason.LowDiskSpace:
				return MainRes.AutoPauseResumeDiskSpace;
			case AutoPauseReason.ProcessPresent:
				return MainRes.AutoPauseResumeProcess;
			default:
				return string.Empty;
		}
	}

	private string GetProcessToAutoPauseOn()
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
