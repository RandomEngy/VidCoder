using System;
using System.Diagnostics;
using System.Linq;
using System.ServiceModel;
using HandBrake.ApplicationServices.Interop;
using HandBrake.ApplicationServices.Interop.Json.Encode;
using HandBrake.ApplicationServices.Interop.Json.Scan;
using Newtonsoft.Json;
using VidCoderCommon;
using VidCoderCommon.Model;

namespace VidCoderWorker
{
	public class HandBrakeEncoder : IHandBrakeEncoder
	{
		private IHandBrakeEncoderCallback callback;
		private HandBrakeInstance instance;
		private object encodeLock = new object();

		// True if we are encoding (not scanning)
		private EncodeState state = EncodeState.NotStarted;

		public void StartEncode(
			VCJob job,
			int previewNumber,
			int previewSeconds,
			int verbosity,
			int previewCount,
			bool useDvdNav,
			bool dxvaDecoding,
			double minTitleDurationSeconds,
			string defaultChapterNameFormat,
			double cpuThrottlingFraction,
			string tempFolder)
		{
#if DEBUG_REMOTE
			Debugger.Launch();
#endif

			if (!string.IsNullOrEmpty(tempFolder))
			{
				Environment.SetEnvironmentVariable("TMP", tempFolder, EnvironmentVariableTarget.Process);
			}

			CurrentEncoder = this;
			this.callback = OperationContext.Current.GetCallbackChannel<IHandBrakeEncoderCallback>();

			try
			{
				if (this.callback == null)
				{
					throw new ArgumentException("Could not get callback channel.");
				}

				if (cpuThrottlingFraction < 1.0)
				{
					int coresToUse = (int)Math.Round(Environment.ProcessorCount * cpuThrottlingFraction);
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

					process.ProcessorAffinity = (IntPtr) affinityMask;
				}

				HandBrakeUtils.MessageLogged += (o, e) =>
					{
						this.StopOnException(() =>
							{
								this.callback.OnMessageLogged(e.Message);
							});
					};

				HandBrakeUtils.ErrorLogged += (o, e) =>
					{
						this.StopOnException(() =>
							{
								this.callback.OnErrorLogged(e.Message);
							});
					};

				HandBrakeUtils.SetDvdNav(useDvdNav);

				this.instance = new HandBrakeInstance();
				this.instance.Initialize(verbosity);

				this.instance.ScanCompleted += (o, e) =>
				{
					try
					{
						SourceTitle encodeTitle = this.instance.Titles.TitleList.FirstOrDefault(title => title.Index == job.Title);
						JsonEncodeObject encodeObject = JsonEncodeFactory.CreateJsonObject(
							job,
							encodeTitle,
							defaultChapterNameFormat,
							dxvaDecoding,
							previewNumber,
							previewSeconds,
							previewCount);

						this.callback.OnVidCoderMessageLogged("Encode JSON:" + Environment.NewLine + JsonConvert.SerializeObject(encodeObject, Formatting.Indented));

						if (encodeTitle != null)
						{
							lock (this.encodeLock)
							{
								this.instance.StartEncode(encodeObject);
								this.callback.OnEncodeStarted();
								this.state = EncodeState.Encoding;
							}
						}
						else
						{
							this.callback.OnEncodeComplete(true);
							this.CleanUpAndSignalCompletion();
						}
					}
					catch (Exception exception)
					{
						this.callback.OnException(exception.ToString());
						this.CleanUpAndSignalCompletion();
					}
				};

				this.instance.EncodeProgress += (o, e) =>
				{
					this.StopOnException(() =>
					{
						this.callback.OnEncodeProgress((float)e.AverageFrameRate, (float)e.CurrentFrameRate, e.EstimatedTimeLeft, (float)e.FractionComplete, e.PassId, e.Pass, e.PassCount);
					});
				};

				this.instance.EncodeCompleted += (o, e) =>
				{
					this.state = EncodeState.Finished;

					try
					{
						this.callback.OnEncodeComplete(e.Error);
					}
					catch (CommunicationException exception)
					{
						WorkerLogger.Log("Got exception when reporting completion: " + exception, isError: true);
					}
					finally
					{
						this.CleanUpAndSignalCompletion();
					}
				};


				this.instance.StartScan(job.SourcePath, previewCount, TimeSpan.FromSeconds(minTitleDurationSeconds), job.Title);
				this.state = EncodeState.Scanning;
			}
			catch (Exception exception)
			{
				this.callback.OnException(exception.ToString());
				throw;
			}
		}

		public static HandBrakeEncoder CurrentEncoder { get; private set; }

		public void PauseEncode()
		{
			lock (this.encodeLock)
			{
				if (this.state == EncodeState.Encoding)
				{
					this.instance.PauseEncode();
				}
			}
		}

		public void ResumeEncode()
		{
			lock (this.encodeLock)
			{
				if (this.state == EncodeState.Encoding)
				{
					this.instance.ResumeEncode();
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
					this.instance.StopEncode();
					this.state = EncodeState.Stopping;
					return true;
				}
			}

			return false;
		}

		public string Ping()
		{
			return "OK";
		}

		public void CleanUp()
		{
			if (this.instance != null)
			{
				this.instance.Dispose();
			}

			HandBrakeUtils.DisposeGlobal();
		}

		private void CleanUpAndSignalCompletion()
		{
			this.CleanUp();
			Program.SignalEncodeComplete();
		}

		// Executes a callback operation and stops the encode when a communication exception occurs.
		private void StopOnException(Action action)
		{
			try
			{
				action();
			}
			catch (CommunicationException exception)
			{
				WorkerLogger.Log("Got exception: " + exception, isError: true);

				this.StopEncodeIfPossible();
			}
		}
	}
}
