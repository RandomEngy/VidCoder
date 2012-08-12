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
		private bool encoding;

		public void StartEncode(EncodeJob job, bool preview, int previewNumber, int previewSeconds, double overallSelectedLengthSeconds, int verbosity)
		{
			this.callback = OperationContext.Current.GetCallbackChannel<IHandBrakeEncoderCallback>();

			try
			{
				if (this.callback == null)
				{
					throw new ArgumentException("Could not get callback channel.");
				}

				HandBrakeUtils.MessageLogged += (o, e) =>
					{
						this.callback.OnMessageLogged(e.Message);
					};

				HandBrakeUtils.ErrorLogged += (o, e) =>
					{
						this.callback.OnErrorLogged(e.Message);
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
									this.encoding = true;
								}
							}
							else
							{
								this.callback.OnEncodeComplete(true);
								Program.SignalEncodeComplete();
							}
						}
						catch (Exception exception)
						{
							this.callback.OnException(exception.ToString());
							Environment.Exit(1);
							throw;
						}
					};

				this.instance.EncodeProgress += (o, e) =>
					{
						this.callback.OnEncodeProgress(e.AverageFrameRate, e.CurrentFrameRate, e.EstimatedTimeLeft, e.FractionComplete, e.Pass);
					};

				this.instance.EncodeCompleted += (o, e) =>
					{
						this.callback.OnEncodeComplete(e.Error);
						Program.SignalEncodeComplete();
					};

				this.instance.StartScan(job.SourcePath, 10, job.Title);
			}
			catch (Exception exception)
			{
				this.callback.OnException(exception.ToString());
				throw;
			}
		}

		public void PauseEncode()
		{
			lock (this.encodeLock)
			{
				if (this.encoding)
				{
					this.instance.PauseEncode();
				}
			}
		}

		public void ResumeEncode()
		{
			lock (this.encodeLock)
			{
				if (this.encoding)
				{
					this.instance.ResumeEncode();
				}
			}
		}

		public void StopEncode()
		{
			lock (this.encodeLock)
			{
				if (this.encoding)
				{
					this.instance.StopEncode();
					this.encoding = false;
					Program.SignalEncodeComplete();
				}
			}
		}

		public string Ping()
		{
			return "OK";
		}
	}
}
