using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HandBrake.ApplicationServices.Interop;
using HandBrake.ApplicationServices.Interop.EventArgs;
using HandBrake.ApplicationServices.Interop.Json.Encode;
using HandBrake.ApplicationServices.Interop.Json.Scan;
using Newtonsoft.Json;
using VidCoder.Resources;
using VidCoder.Services;
using VidCoderCommon.Model;
using System.Threading;
using System.Xml.Serialization;
using VidCoderCommon.Utilities;

namespace VidCoder
{
	public class LocalEncodeProxy : IEncodeProxy
	{
		private IAppLogger logger;

		// Instance and lock only used when doing in-process encode (for debugging)
		private HandBrakeInstance instance;
		private object encoderLock = new object();

		private ManualResetEventSlim encodeStartEvent;
		private ManualResetEventSlim encodeEndEvent;

		private bool encoding;

		public event EventHandler EncodeStarted;
		public event EventHandler<EncodeProgressEventArgs> EncodeProgress;
		public event EventHandler<EncodeCompletedEventArgs> EncodeCompleted;

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
							Config.DxvaDecoding,
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
		}

		public void StartEncode(string encodeJson, IAppLogger logger)
		{
			this.logger = logger;

			// Extract the scan title and path from the encode JSON.
			JsonEncodeObject encodeObject = JsonConvert.DeserializeObject<JsonEncodeObject>(encodeJson);

			this.StartEncodeInternal(encodeObject.Source.Path, encodeObject.Source.Title, scanObject => encodeJson);
		}

		private void StartEncodeInternal(string scanPath, int titleNumber, Func<JsonScanObject, string> jsonFunc)
		{
			this.logger.Log("Starting encode in-process");

			this.encoding = true;

			this.encodeStartEvent = new ManualResetEventSlim(false);
			this.encodeEndEvent = new ManualResetEventSlim(false);

			this.instance = new HandBrakeInstance();
			this.instance.Initialize(Config.LogVerbosity);

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
							this.IsEncodeStarted = true;
							this.EncodeStarted?.Invoke(this, EventArgs.Empty);

							this.encodeStartEvent.Set();
						}
					}
					else
					{
						this.EncodeCompleted?.Invoke(this, new EncodeCompletedEventArgs(error: true));

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
					this.EncodeCompleted?.Invoke(this, e);

					this.encoding = false;
				}

				this.encodeEndEvent.Set();
				this.instance.Dispose();
			};

			this.instance.StartScan(scanPath, Config.PreviewCount, TimeSpan.FromSeconds(Config.MinimumTitleLengthSeconds), titleNumber);

			this.encoding = true;
		}

		public void PauseEncode()
		{
			this.encodeStartEvent.Wait();

			lock (this.encoderLock)
			{
				this.instance.PauseEncode();
			}
		}

		public void ResumeEncode()
		{
			this.instance.ResumeEncode();
		}

		public void StopEncode()
		{
			this.encodeStartEvent.Wait();

			lock (this.encoderLock)
			{
				this.instance.StopEncode();
			}
		}

		public void StopAndWait()
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
		}
	}
}
