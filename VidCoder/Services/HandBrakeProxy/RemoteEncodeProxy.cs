using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HandBrake.Interop.Interop.EventArgs;
using PipeMethodCalls;
using VidCoder.Model;
using VidCoder.Resources;
using VidCoder.Services;
using VidCoderCommon;
using VidCoderCommon.Model;

namespace VidCoder
{
	public class RemoteEncodeProxy : RemoteProxyBase<IHandBrakeEncodeWorker, IHandBrakeEncodeWorkerCallback>, IEncodeProxy, IHandBrakeEncodeWorkerCallback
	{
		public event EventHandler EncodeStarted;

		/// <summary>
		/// Fires for progress updates when encoding.
		/// </summary>
		public event EventHandler<EncodeProgressEventArgs> EncodeProgress;

		/// <summary>
		/// Fires when an encode has completed.
		/// </summary>
		public event EventHandler<EncodeCompletedEventArgs> EncodeCompleted;

		private SemaphoreSlim encodeStartEvent;
		private SemaphoreSlim encodeEndEvent;

		public async Task StartEncodeAsync(
			VCJob job,
			IAppLogger logger,
			bool preview,
			int previewNumber,
			int previewSeconds,
			double overallSelectedLengthSeconds)
		{
			this.Logger = logger;

			this.encodeStartEvent = new SemaphoreSlim(0, 1);
			this.encodeEndEvent = new SemaphoreSlim(0, 1);

			await this.RunOperationAsync(worker =>
				worker.StartEncode(
					job,
					preview ? previewNumber : -1,
					previewSeconds,
					EncodingRes.DefaultChapterName)
			);
		}

		// Used for debug testing of encode JSON.
		public async Task StartEncodeAsync(string encodeJson, IAppLogger logger)
		{
			this.Logger = logger;

			this.encodeStartEvent = new SemaphoreSlim(0, 1);
			this.encodeEndEvent = new SemaphoreSlim(0, 1);

			await this.RunOperationAsync(worker => worker.StartEncodeFromJson(encodeJson));
		}

		public Task PauseEncodeAsync()
		{
			return this.ExecuteWorkerCallAsync(channel => channel.PauseEncode(), nameof(IHandBrakeEncodeWorker.PauseEncode));
		}

		public Task ResumeEncodeAsync()
		{
			return this.ExecuteWorkerCallAsync(channel => channel.ResumeEncode(), nameof(IHandBrakeEncodeWorker.ResumeEncode));
		}

		public Task StopEncodeAsync()
		{
			return this.ExecuteWorkerCallAsync(channel => channel.StopEncode(), nameof(IHandBrakeEncodeWorker.StopEncode));
		}

		// This can be called at any time: it will stop the encode ASAP and wait for encode to be stopped before returning.
		// Usually called before exiting the program.
		public async Task StopAndWaitAsync()
		{
			if (this.encodeStartEvent == null)
			{
				return;
			}

			this.encodeStartEvent.Wait();
			bool waitForEnd;

			await this.ProcessLock.WaitAsync().ConfigureAwait(false);
			try
			{
				bool connected = this.Client != null;
				waitForEnd = connected;

				if (this.Running && connected)
				{
					await this.ExecuteProxyOperationAsync(() => this.Client.InvokeAsync(x => x.StopEncode()), nameof(IHandBrakeEncodeWorker.StopEncode)).ConfigureAwait(false);

					// If stopping the encode failed, don't wait for the encode to end.
					waitForEnd = this.Running;
				}
			}
			finally
			{
				this.ProcessLock.Release();
			}

			if (waitForEnd)
			{
				await this.encodeEndEvent.WaitAsync().ConfigureAwait(false);
			}
		}

		public void OnEncodeStarted()
		{
			this.EncodeStarted?.Invoke(this, EventArgs.Empty);

			this.encodeStartEvent.Release();
		}

		public void OnEncodeProgress(float averageFrameRate, float currentFrameRate, TimeSpan estimatedTimeLeft, float fractionComplete, int passId, int pass, int passCount, string stateCode)
		{
			// Dispatch to avoid deadlocks on callbacks
			DispatchUtilities.BeginInvoke(async () =>
				{
					await this.ProcessLock.WaitAsync().ConfigureAwait(false);
					try
					{
						this.LastWorkerCommunication = DateTimeOffset.UtcNow;

						if (this.Running && this.EncodeProgress != null)
						{
							this.EncodeProgress(
								this,
								new EncodeProgressEventArgs(fractionComplete, currentFrameRate, averageFrameRate, estimatedTimeLeft, passId, pass, passCount, stateCode));
						}
					}
					finally
					{
						this.ProcessLock.Release();
					}
				});
		}

		public async void OnEncodeComplete(bool error)
		{
			await this.ProcessLock.WaitAsync().ConfigureAwait(false);
			try
			{
				this.EndOperation(error);
			}
			finally
			{
				this.ProcessLock.Release();
			}

			this.encodeEndEvent.Release();
		}

		protected override HandBrakeWorkerAction Action => HandBrakeWorkerAction.Encode;

		protected override IHandBrakeEncodeWorkerCallback CallbackInstance => this;

		protected override void OnOperationEnd(bool error)
		{
			this.EncodeCompleted?.Invoke(
				this,
				new EncodeCompletedEventArgs(error));
		}
	}
}
