using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using HandBrake.ApplicationServices.Interop.EventArgs;
using HandBrake.ApplicationServices.Interop.Json.Scan;
using VidCoder.Model;
using VidCoder.Resources;
using VidCoder.Services;
using VidCoderCommon;
using VidCoderCommon.Model;
using Timer = System.Timers.Timer;

namespace VidCoder
{
	public class RemoteEncodeProxy : RemoteProxyBase, IHandBrakeWorkerCallback, IEncodeProxy
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

		private ManualResetEventSlim encodeStartEvent;
		private ManualResetEventSlim encodeEndEvent;

		[XmlIgnore]
		public bool IsEncodeStarted { get; private set; }

		public void StartEncode(
			VCJob job,
			IAppLogger logger,
			bool preview,
			int previewNumber,
			int previewSeconds,
			double overallSelectedLengthSeconds)
		{
			this.Logger = logger;

			this.encodeStartEvent = new ManualResetEventSlim(false);
			this.encodeEndEvent = new ManualResetEventSlim(false);

			this.StartOperation(channel =>
			{
				channel.StartEncode(
					job,
					preview ? previewNumber : -1,
					previewSeconds,
					EncodingRes.DefaultChapterName);
			});
		}

		// Used for debug testing of encode JSON.
		public void StartEncode(string encodeJson, IAppLogger logger)
		{
			this.Logger = logger;

			this.encodeStartEvent = new ManualResetEventSlim(false);
			this.encodeEndEvent = new ManualResetEventSlim(false);

			this.StartOperation(channel =>
			{
				channel.StartEncodeFromJson(encodeJson);
			});
		}

		public void PauseEncode()
		{
			this.ExecuteWorkerCall(channel => channel.PauseEncode());
		}

		public void ResumeEncode()
		{
			this.ExecuteWorkerCall(channel => channel.ResumeEncode());
		}

		public void StopEncode()
		{
			this.ExecuteWorkerCall(channel => channel.StopEncode());
		}

		// This can be called at any time: it will stop the encode ASAP and wait for encode to be stopped before returning.
		// Usually called before exiting the program.
		public override void StopAndWait()
		{
			this.encodeStartEvent.Wait();
			bool waitForEnd;

			lock (this.ProcessLock)
			{
				bool connected = this.Channel != null;
				waitForEnd = connected;

				if (this.Running && connected)
				{
					this.ExecuteProxyOperation(() => this.Channel.StopEncode());

					// If stopping the encode failed, don't wait for the encode to end.
					waitForEnd = this.Running;
				}
			}

			if (waitForEnd)
			{
				this.encodeEndEvent.Wait();
			}
		}

		public void OnScanProgress(float fractionComplete)
		{
			throw new NotImplementedException();
		}

		public void OnScanComplete(string scanJson)
		{
			throw new NotImplementedException();
		}

		public void OnEncodeStarted()
		{
			this.IsEncodeStarted = true;
			this.EncodeStarted?.Invoke(this, EventArgs.Empty);

			this.encodeStartEvent.Set();
		}

		public void OnEncodeProgress(float averageFrameRate, float currentFrameRate, TimeSpan estimatedTimeLeft, float fractionComplete, int passId, int pass, int passCount)
		{
			// Dispatch to avoid deadlocks on callbacks
			DispatchUtilities.BeginInvoke(() =>
				{
					lock (this.ProcessLock)
					{
						this.LastWorkerCommunication = DateTimeOffset.UtcNow;

						if (this.Running && this.EncodeProgress != null)
						{
							this.EncodeProgress(
								this,
								new EncodeProgressEventArgs(fractionComplete, currentFrameRate, averageFrameRate, estimatedTimeLeft, passId, pass, passCount));
						}
					}
				});
		}

		public void OnEncodeComplete(bool error)
		{
			lock (this.ProcessLock)
			{
				this.EndOperation(error);
			}

			this.encodeEndEvent.Set();
		}

		protected override void OnOperationEnd(bool error)
		{
			this.EncodeCompleted?.Invoke(
				this,
				new EncodeCompletedEventArgs(error));
		}
	}
}
