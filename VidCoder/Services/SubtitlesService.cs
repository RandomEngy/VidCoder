using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AnyContainer;
using UtfUnknown;
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

		public FileSubtitle LoadSubtitleFile(string subtitlePath)
		{
			try
			{
				DetectionResult detectionResult = CharsetDetector.DetectFromFile(subtitlePath);

				string characterCode = null;
				DetectionDetail detail = detectionResult.Detected;
				if (detail != null && detail.EncodingName != null)
				{
					this.logger.Log($"Detected encoding {detail.EncodingName} for {subtitlePath} with confidence {detail.Confidence}.");
					characterCode = CharCode.FromUtfUnknownCode(detail.EncodingName);

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
					StaticResolver.Resolve<IMessageBoxService>().Show(this, SubtitleRes.SubtitleCharsetDetectionFailedMessage);
					characterCode = "UTF-8";
				}

				return new FileSubtitle { FileName = subtitlePath, Default = false, CharacterCode = characterCode, LanguageCode = LanguageUtilities.GetDefaultLanguageCode(), Offset = 0 };
			}
			catch (Exception exception)
			{
				this.logger.LogError("Could not load subtitle file: " + exception);
				return null;
			}
		}
	}
}
