using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VidCoderWorker
{
	using System.ServiceModel;
	using HandBrake.Interop;
	using HandBrake.Interop.Model;
	using HandBrake.Interop.SourceData;

	public class HandBrakeEncoder : IHandBrakeEncoder
	{
		private IHandBrakeEncoderCallback callback;
		private HandBrakeInstance instance;
		private object encodeLock = new object();

		// True if we are encoding (not scanning)
		private EncodeState state = EncodeState.NotStarted;

		public void StartEncode(EncodeJob job, bool preview, int previewNumber, int previewSeconds, double overallSelectedLengthSeconds, int verbosity, int previewCount)
		{
			Program.Logger.Log("StartEncode called.");

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

				this.instance = new HandBrakeInstance();
				this.instance.Initialize(verbosity);

				this.instance.ScanCompleted += (o, e) =>
					{
						try
						{
							Title encodeTitle = this.instance.Titles.FirstOrDefault(title => title.TitleNumber == job.Title);

							if (encodeTitle != null)
							{
								lock (this.encodeLock)
								{
									this.instance.StartEncode(job, preview, previewNumber, previewSeconds, overallSelectedLengthSeconds);
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
								this.callback.OnEncodeProgress(e.AverageFrameRate, e.CurrentFrameRate, e.EstimatedTimeLeft, e.FractionComplete, e.Pass);
							});
					};

				this.instance.EncodeCompleted += (o, e) =>
					{
						this.state = EncodeState.Finished;

						try
						{
							this.callback.OnEncodeComplete(e.Error);
						}
						catch (CommunicationException)
						{
							// If reporting completion fails, there's nothing we can do but clean up.
						}
						finally
						{
							this.CleanUpAndSignalCompletion();
						}
					};

				this.instance.StartScan(job.SourcePath, previewCount, job.Title);
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
			Program.Logger.Log("Got Ping.");

			return "OK";
		}

		public void CleanUp()
		{
			Program.Logger.Log("Cleaning up handbrake instance.");

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
				Program.Logger.Log("Got exception: " + exception.ToString());

				this.StopEncodeIfPossible();
			}
		}
	}
}
