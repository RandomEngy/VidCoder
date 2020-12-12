using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using VidCoder.Resources;
using VidCoder.Services;
using VidCoderCommon.Model;
using System.Threading;
using System.Threading.Tasks;
using HandBrake.Interop.Interop;
using HandBrake.Interop.Interop.EventArgs;
using HandBrake.Interop.Interop.Json.Encode;
using HandBrake.Interop.Interop.Json.Scan;
using VidCoderCommon.Utilities;

namespace VidCoder
{
	public class LocalEncodeProxy : IEncodeProxy
	{
		private IAppLogger logger;

		// Instance and lock only used when doing in-process encode (for debugging)
		private HandBrakeInstance instance;
		private readonly object encoderLock = new object();

		private ManualResetEventSlim encodeStartEvent;
		private ManualResetEventSlim encodeEndEvent;

		private bool encoding;

		public event EventHandler EncodeStarted;
		public event EventHandler<EncodeProgressEventArgs> EncodeProgress;
		public event EventHandler<VCEncodeCompletedEventArgs> EncodeCompleted;

		public Task StartEncodeAsync(
			VCJob job,
			IAppLogger logger,
			bool preview, 
			int previewNumber, 
			int previewSeconds, 
			double overallSelectedLengthSeconds)
		{
			this.logger = logger;

			this.StartEncodeInternal(
				job.SourcePath,
				job.Title,
				scanObject =>
				{
					SourceTitle encodeTitle = scanObject.TitleList.FirstOrDefault(title => title.Index == job.Title);
					if (encodeTitle != null)
					{
						JsonEncodeFactory factory = new JsonEncodeFactory(logger);

						JsonEncodeObject jsonEncodeObject = factory.CreateJsonObject(
							job,
							encodeTitle,
							EncodingRes.DefaultChapterName,
							preview ? previewNumber : -1,
							previewSeconds,
							Config.PreviewCount);

						return JsonConvert.SerializeObject(jsonEncodeObject, JsonSettings.HandBrakeJsonSerializerSettings);
					}
					else
					{
						return null;
					}
				});

			return Task.CompletedTask;
		}

		public Task StartEncodeAsync(string encodeJson, IAppLogger logger)
		{
			this.logger = logger;

			// Extract the scan title and path from the encode JSON.
			JsonEncodeObject encodeObject = JsonConvert.DeserializeObject<JsonEncodeObject>(encodeJson);

			this.StartEncodeInternal(encodeObject.Source.Path, encodeObject.Source.Title, scanObject => encodeJson);
			return Task.CompletedTask;
		}

		private void StartEncodeInternal(string scanPath, int titleNumber, Func<JsonScanObject, string> jsonFunc)
		{
			this.logger.Log("Starting encode in-process");

			this.encoding = true;

			this.encodeStartEvent = new ManualResetEventSlim(false);
			this.encodeEndEvent = new ManualResetEventSlim(false);

			this.instance = new HandBrakeInstance();
			this.instance.Initialize(Config.LogVerbosity, noHardware: false);

			this.instance.ScanCompleted += (o, e) =>
			{
				try
				{
					string encodeJson = jsonFunc(this.instance.Titles);
					if (encodeJson != null)
					{
						lock (this.encoderLock)
						{
							this.instance.StartEncode(encodeJson);
							this.EncodeStarted?.Invoke(this, EventArgs.Empty);

							this.encodeStartEvent.Set();
						}
					}
					else
					{
						this.EncodeCompleted?.Invoke(this, new VCEncodeCompletedEventArgs(VCEncodeResultCode.ErrorScanFailed));

						this.encodeStartEvent.Set();
						this.encodeEndEvent.Set();
					}
				}
				catch (Exception exception)
				{
					this.logger.LogError("Encoding failed. Please report this error so it can be fixed in the future:" + Environment.NewLine + exception);
				}
			};

			this.instance.EncodeProgress += (o, e) =>
			{
				// Dispatch to avoid deadlocks on callbacks
				DispatchUtilities.BeginInvoke(() =>
				{
					lock (this.encoderLock)
					{
						if (this.encoding)
						{
							this.EncodeProgress?.Invoke(this, e);
						}
					}
				});
			};

			this.instance.EncodeCompleted += (o, e) =>
			{
				if (this.encoding)
				{
					this.EncodeCompleted?.Invoke(this, new VCEncodeCompletedEventArgs((VCEncodeResultCode)e.Error));

					this.encoding = false;
				}

				this.encodeEndEvent.Set();
				this.instance.Dispose();
			};

			this.instance.StartScan(scanPath, Config.PreviewCount, TimeSpan.FromSeconds(Config.MinimumTitleLengthSeconds), titleNumber);

			this.encoding = true;
		}

		public Task PauseEncodeAsync()
		{
			this.encodeStartEvent.Wait();

			lock (this.encoderLock)
			{
				this.instance.PauseEncode();
			}

			return Task.CompletedTask;
		}

		public Task ResumeEncodeAsync()
		{
			lock (this.encoderLock)
			{
				this.instance.ResumeEncode();
			}

			return Task.CompletedTask;
		}

		public Task StopEncodeAsync()
		{
			this.encodeStartEvent.Wait();

			lock (this.encoderLock)
			{
				this.instance.StopEncode();
			}

			return Task.CompletedTask;
		}

		public Task StopAndWaitAsync()
		{
			this.encodeStartEvent.Wait();

			lock (this.encoderLock)
			{
				if (this.encoding)
				{
					this.instance.StopEncode();
				}
			}

			this.encodeEndEvent.Wait();

			return Task.CompletedTask;
		}

		public void Dispose()
		{
		}
	}
}
