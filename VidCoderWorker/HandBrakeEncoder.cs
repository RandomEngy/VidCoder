using System;
using System.Linq;
using System.ServiceModel;
using HandBrake.ApplicationServices.Interop;
using HandBrake.ApplicationServices.Interop.Json.Encode;
using HandBrake.ApplicationServices.Interop.Json.Scan;
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
			SourceTitle encodeTitle,
			int previewNumber,
			int previewSeconds,
			int verbosity,
			int previewCount,
			bool useDvdNav,
			bool dxvaDecoding,
			double minTitleDurationSeconds,
			string defaultChapterNameFormat)
		{
			CurrentEncoder = this;
			this.callback = OperationContext.Current.GetCallbackChannel<IHandBrakeEncoderCallback>();

			try
			{
				if (this.callback == null)
				{
					throw new ArgumentException("Could not get callback channel.");
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

				if (encodeTitle == null)
				{
					this.instance.ScanCompleted += (o, e) =>
					{
						encodeTitle = this.instance.Titles.TitleList.FirstOrDefault(title => title.Index == job.Title);
						this.StartEncode(job, encodeTitle, previewNumber, previewSeconds, previewCount, dxvaDecoding, defaultChapterNameFormat);
					};

					this.instance.StartScan(job.SourcePath, previewCount, TimeSpan.FromSeconds(minTitleDurationSeconds), job.Title);
					this.state = EncodeState.Scanning;
				}
				else
				{
					this.StartEncode(job, encodeTitle, previewNumber, previewSeconds, previewCount, dxvaDecoding, defaultChapterNameFormat);
				}

				//this.instance.ScanCompleted += (o, e) =>
				//	{
				//		this.StartEncode(job, encodeTitle, previewNumber, previewSeconds, previewCount, dxvaDecoding, defaultChapterNameFormat);
				//	};

				//this.instance.StartScan(job.SourcePath, previewCount, TimeSpan.FromSeconds(minTitleDurationSeconds), job.Title);
				//this.state = EncodeState.Scanning;
			}
			catch (Exception exception)
			{
				this.callback.OnException(exception.ToString());
				throw;
			}
		}

		private void StartEncode(VCJob job, SourceTitle encodeTitle, int previewNumber, int previewSeconds, int previewCount, bool dxvaDecoding, string defaultChapterNameFormat)
		{
			try
			{
				JsonEncodeObject encodeObject = JsonEncodeFactory.CreateJsonObject(
					job,
					encodeTitle,
					defaultChapterNameFormat,
					dxvaDecoding,
					previewNumber,
					previewSeconds,
					previewCount);

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
