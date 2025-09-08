using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using HandBrake.Interop.Interop;
using PipeMethodCalls;
using VidCoderCommon;
using VidCoderCommon.Services;

namespace VidCoderWorker;

public class HandBrakeWorkerBase<TCallback> : IHandBrakeWorker
	where TCallback : class, IHandBrakeWorkerCallback
{
	private const int LogMessageSendDelayMs = 100;

	// These are saved when SetUpWorker is called and used later.
	private double currentCpuThrottlingFraction;

	// Batch log messages so the pipe messaging back to UI process is less chatty
	private readonly List<string> logMessageList = new();

	private bool logMessageSendScheduled;

	private readonly object logListLock = new();

	public HandBrakeWorkerBase(IPipeInvoker<TCallback> callback)
	{
		this.CallbackInvoker = callback ?? throw new ArgumentNullException(nameof(callback));
	}

	protected IPipeInvoker<TCallback> CallbackInvoker { get; }

	protected HandBrakeInstance Instance { get; set; }

	protected ILogger Logger { get; set; }

	protected int PassedVerbosity { get; set; }

	protected int PassedPreviewCount { get; set; }

	protected double PassedMinTitleDurationSeconds { get; set; }

	public void SetUpWorker(
		int verbosity,
		int previewCount,
		bool useDvdNav,
		double minTitleDurationSeconds,
		double cpuThrottlingFraction,
		string tempFolder)
	{
#if DEBUG_REMOTE
		Debugger.Launch();
#endif

		this.PassedVerbosity = verbosity;
		this.PassedPreviewCount = previewCount;
		this.PassedMinTitleDurationSeconds = minTitleDurationSeconds;
		this.currentCpuThrottlingFraction = cpuThrottlingFraction;

		this.Logger = new WorkerLogger<TCallback>(this.CallbackInvoker);

		try
		{
			if (!string.IsNullOrEmpty(tempFolder))
			{
				Environment.SetEnvironmentVariable("TMP", tempFolder, EnvironmentVariableTarget.Process);
			}

			this.ApplyCpuThrottling(onlyIfReduced: true);

			HandBrakeUtils.MessageLogged += (o, e) =>
			{
				lock (this.logListLock)
				{
					this.logMessageList.Add(e.Message);
					this.ScheduleMessageSendIfNeeded();
				}
			};

			HandBrakeUtils.ErrorLogged += (o, e) =>
			{
				// If we run into an error, immediately send any pending batch of log messages
				this.StartSendingPendingLogMessages();
				this.CallbackError(e.Message);
			};

			HandBrakeUtils.SetDvdNav(useDvdNav);
		}
		catch (Exception exception)
		{
			this.CallbackError(exception.ToString());
			throw;
		}
	}

	public void TearDownWorker()
	{
		Task.Run(async () =>
		{
			this.Instance?.Dispose();
			HandBrakeUtils.DisposeGlobal();

			await this.SendPendingLogMessagesAsync().ConfigureAwait(false);

			Program.SignalEncodeComplete();
		});
	}

	private async void ScheduleMessageSendIfNeeded()
	{
		if (!this.logMessageSendScheduled)
		{
			this.logMessageSendScheduled = true;

			await Task.Delay(LogMessageSendDelayMs).ConfigureAwait(false);

			this.StartSendingPendingLogMessages();
			this.logMessageSendScheduled = false;
		}
	}

	private async void StartSendingPendingLogMessages()
	{
		await this.SendPendingLogMessagesAsync().ConfigureAwait(false);
	}

	protected async Task SendPendingLogMessagesAsync()
	{
		string messages = null;
		lock (this.logListLock)
		{
			if (this.logMessageList.Count > 0)
			{
				messages = string.Join(Environment.NewLine, this.logMessageList);
				this.logMessageList.Clear();
			}
		}

		if (messages != null)
		{
			try
			{
				await this.CallbackInvoker.InvokeAsync(c => c.OnMessageLogged(messages));
			}
			catch (Exception)
			{
				// Ignore error if we can't log.
			}
		}
	}

	private async void CallbackError(string errorMessage)
	{
		try
		{
			await this.CallbackInvoker.InvokeAsync(c => c.OnErrorLogged(errorMessage));
		}
		catch (Exception)
		{
			// Ignore error if we can't log it.
		}
	}

	public string Ping()
	{
		return "OK";
	}

	protected async void CleanUpAndSignalCompletion()
	{
		this.Instance?.Dispose();
		HandBrakeUtils.DisposeGlobal();

		await this.SendPendingLogMessagesAsync().ConfigureAwait(false);

		Program.SignalEncodeComplete();
	}

	protected void ApplyCpuThrottling(bool onlyIfReduced)
	{
		if (!onlyIfReduced || this.currentCpuThrottlingFraction < 1.0)
		{
			int coresToUse = (int)Math.Round(Environment.ProcessorCount * this.currentCpuThrottlingFraction);
			if (coresToUse < 1)
			{
				coresToUse = 1;
			}

			if (coresToUse > Environment.ProcessorCount)
			{
				coresToUse = Environment.ProcessorCount;
			}

			Process process = Process.GetCurrentProcess();
			long affinityMask = 0x0;
			for (int i = 0; i < coresToUse; i++)
			{
				affinityMask |= (uint)(1 << i);
			}

#pragma warning disable CA1416 // Validate platform compatibility
			process.ProcessorAffinity = (IntPtr)affinityMask;
#pragma warning restore CA1416 // Validate platform compatibility
		}
	}

	protected async void MakeOneWayCallback(Expression<Action<TCallback>> action)
	{
		try
		{
			await this.CallbackInvoker.InvokeAsync(action);
		}
		catch (Exception)
		{
		}
	}

	public void UpdateCpuThrottling(double cpuThrottlingFraction)
	{
		this.currentCpuThrottlingFraction = cpuThrottlingFraction;
		this.ApplyCpuThrottling(onlyIfReduced: false);
	}
}
