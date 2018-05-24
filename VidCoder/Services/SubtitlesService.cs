using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VidCoder.Model;
using VidCoder.Resources;
using VidCoderCommon.Model;

namespace VidCoder.Services
{
	public class SubtitlesService
	{
		private readonly IAppLogger logger;

		public SubtitlesService(IAppLogger logger)
		{
			this.logger = logger;
		}

		public SrtSubtitle LoadSrtSubtitle(string srtPath)
		{
			try
			{
				string characterCode = null;
				using (FileStream srtFileStream = File.OpenRead(srtPath))
				{
					Ude.CharsetDetector detector = new Ude.CharsetDetector();
					detector.Feed(srtFileStream);
					detector.DataEnd();
					if (detector.Charset != null)
					{
						this.logger.Log($"Detected encoding {detector.Charset} for {srtPath} with confidence {detector.Confidence}.");
						characterCode = CharCode.FromUdeCode(detector.Charset);

						if (characterCode == null)
						{
							this.logger.Log("Detected encoding does not match with any available encoding.");
						}
						else
						{
							this.logger.Log("Picked encoding " + characterCode);
						}
					}

					if (characterCode == null)
					{
						Ioc.Get<IMessageBoxService>().Show(this, SubtitleRes.SubtitleCharsetDetectionFailedMessage);
						characterCode = "UTF-8";
					}
				}

				return new SrtSubtitle { FileName = srtPath, Default = false, CharacterCode = characterCode, LanguageCode = LanguageUtilities.GetDefaultLanguageCode(), Offset = 0 };
			}
			catch (Exception exception)
			{
				this.logger.LogError("Could not load SRT file: " + exception);
				return null;
			}
		}
	}
}
