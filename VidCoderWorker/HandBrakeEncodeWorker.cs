using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using HandBrake.Interop.Interop;
using HandBrake.Interop.Interop.Json.Encode;
using HandBrake.Interop.Interop.Json.Scan;
using Newtonsoft.Json;
using PipeMethodCalls;
using VidCoderCommon;
using VidCoderCommon.Model;
using VidCoderCommon.Utilities;

namespace VidCoderWorker
{
	public class HandBrakeEncodeWorker : HandBrakeWorkerBase<IHandBrakeEncodeWorkerCallback>, IHandBrakeEncodeWorker
	{
		// Some encoders overwrite the CPU affinity of the process when they start encoding. For CPU
		// throttling to work we need to set it back when a new pass starts.
		// This is the pass ID we last set CPU affinity for.
		private int lastSetAffinityPassId = -2;

		private readonly object encodeLock = new object();
		private EncodeState state = EncodeState.NotStarted;

		public HandBrakeEncodeWorker(IPipeInvoker<IHandBrakeEncodeWorkerCallback> callback)
			: base(callback)
		{
			CurrentWorker = this;
		}

		/// <summary>
		/// Starts an encode, given a VidCoder job and some extra data.
		/// </summary>
		/// <param name="job">The VidCoder job to run.</param>
		/// <param name="previewNumber">The preview number to run.</param>
		/// <param name="previewSeconds">The number of seconds the preview should be.</param>
		/// <param name="defaultChapterNameFormat">The default format for chapter names.</param>
		public void StartEncode(
			VCJob job,
			int previewNumber,
			int previewSeconds,
			string defaultChapterNameFormat)
		{
			this.StartEncodeInternal(
				job.SourcePath,
				job.Title,
				scanObject =>
				{
					SourceTitle encodeTitle = scanObject.TitleList.FirstOrDefault(title => title.Index == job.Title);
					if (encodeTitle != null)
					{
						JsonEncodeFactory factory = new JsonEncodeFactory(this.Logger);

						JsonEncodeObject encodeObject = factory.CreateJsonObject(
							job,
							encodeTitle,
							defaultChapterNameFormat,
							previewNumber,
							previewSeconds,
							this.PassedPreviewCount);

						return JsonConvert.SerializeObject(encodeObject, JsonSettings.HandBrakeJsonSerializerSettings);
					}
					else
					{
						return null;
					}
				});
		}

		/// <summary>
		/// Starts an encode with the given encode JSON.
		/// </summary>
		/// <param name="encodeJson">The encode JSON.</param>
		/// <remarks>Used for debug testing of raw encode JSON.</remarks>
		public void StartEncodeFromJson(string encodeJson)
		{
			// Extract the scan title and path from the encode JSON.
			JsonEncodeObject encodeObject = JsonConvert.DeserializeObject<JsonEncodeObject>(encodeJson);

			this.StartEncodeInternal(encodeObject.Source.Path, encodeObject.Source.Title, scanObject => encodeJson);
		}

		/// <summary>
		/// Starts an encode.
		/// </summary>
		/// <param name="scanPath">The path to scan.</param>
		/// <param name="titleNumber">The title number to scan.</param>
		/// <param name="jsonFunc">A function to pick the encode JSON given the scan.</param>
		private void StartEncodeInternal(string scanPath, int titleNumber, Func<JsonScanObject, string> jsonFunc)
		{
			try
			{
				this.Instance = new HandBrakeInstance();
				this.Instance.Initialize(this.PassedVerbosity, noHardware: false);

				this.Instance.ScanCompleted += (o, e) =>
				{
					try
					{
						string encodeJson = jsonFunc(this.Instance.Titles);
						if (encodeJson != null)
						{
							lock (this.encodeLock)
							{
								this.Instance.StartEncode(encodeJson);
								this.MakeOneWayCallback(c => c.OnEncodeStarted());
								this.state = EncodeState.Encoding;
							}
						}
						else
						{
							this.MakeOneWayCallback(c => c.OnEncodeComplete(0));
							this.CleanUpAndSignalCompletion();
						}
					}
					catch (Exception exception)
					{
						this.MakeOneWayCallback(c => c.OnException(exception.ToString()));
						this.CleanUpAndSignalCompletion();
					}
				};

				this.Instance.EncodeProgress += (o, e) =>
				{
					this.StopOnException(() =>
					{
						if (e.PassId > this.lastSetAffinityPassId && e.FractionComplete > 0)
						{
							this.ApplyCpuThrottling();
							this.lastSetAffinityPassId = e.PassId;
						}

						this.MakeOneWayCallback(c => c.OnEncodeProgress(
							(float)e.AverageFrameRate, 
							(float)e.CurrentFrameRate, 
							e.EstimatedTimeLeft, 
							(float)e.FractionComplete,
							e.PassId, 
							e.Pass, 
							e.PassCount,
							e.StateCode));

						return Task.CompletedTask;
					});
				};

				this.Instance.EncodeCompleted += async (o, e) =>
				{
					this.state = EncodeState.Finished;

					try
					{
						await this.SendPendingLogMessagesAsync().ConfigureAwait(false);
						await this.CallbackInvoker.InvokeAsync(c => c.OnEncodeComplete((VCEncodeResultCode)e.Error)).ConfigureAwait(false);
					}
					catch (Exception exception)
					{
						WorkerErrorLogger.LogError("Got exception when reporting completion: " + exception, isError: true);
					}
					finally
					{
						this.CleanUpAndSignalCompletion();
					}
				};


				this.Instance.StartScan(scanPath, this.PassedPreviewCount, TimeSpan.FromSeconds(this.PassedMinTitleDurationSeconds), titleNumber);
				this.state = EncodeState.Scanning;
			}
			catch (Exception exception)
			{
				this.MakeOneWayCallback(c => c.OnException(exception.ToString()));
				throw;
			}
		}

		public static HandBrakeEncodeWorker CurrentWorker { get; private set; }

		public void PauseEncode()
		{
			lock (this.encodeLock)
			{
				if (this.state == EncodeState.Encoding)
				{
					this.Instance.PauseEncode();
				}
			}
		}

		public void ResumeEncode()
		{
			lock (this.encodeLock)
			{
				if (this.state == EncodeState.Encoding)
				{
					this.Instance.ResumeEncode();
				}
			}
		}

		public void StopEncode()
		{
			this.StopEncodeIfPossible();
		}

		// Returns true if we sent a stop signal to the encode.
		public bool StopEncodeIfPossible()
		{
			lock (this.encodeLock)
			{
				if (this.state == EncodeState.Encoding)
				{
					this.Instance.StopEncode();
					this.state = EncodeState.Stopping;
					return true;
				}
			}

			return false;
		}

		// Executes a callback operation and stops the encode when an exception occurs.
		private async void StopOnException(Func<Task> action)
		{
			try
			{
				await action();
			}
			catch (Exception exception)
			{
				WorkerErrorLogger.LogError("Got exception: " + exception, isError: true);

				this.StopEncodeIfPossible();
			}
		}
	}
}
