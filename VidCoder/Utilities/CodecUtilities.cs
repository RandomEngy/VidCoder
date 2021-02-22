using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HandBrake.Interop.Interop.Interfaces.Model.Encoders;

namespace VidCoder
{
	public class CodecUtilities
	{
		/// <summary>
		/// Gets overall bitrate limits for the given audio encoder.
		/// </summary>
		/// <param name="encoder">The encoder to check.</param>
		/// <returns>The bitrate limits for the given encoder.</returns>
		/// <remarks>HandBrake does not supply an "overall" set of limits, only when you give it a specific
		/// mixdown and sample rate.</remarks>
		public static BitrateLimits GetAudioEncoderLimits(HBAudioEncoder encoder)
		{
			int low = 8;
			int high = 1536;

			switch (encoder.ShortName)
			{
				case "fdk_aac":
					low = 48;
					high = 1440;
					break;
				case "fdk_haac":
					low = 8;
					high = 256;
					break;
				case "av_aac":
					low = 32;
					high = 512;
					break;
				case "vorbis":
					low = 32;
					high = 1536;
					break;
				case "mp3":
					low = 32;
					high = 320;
					break;
				case "ac3":
					low = 48;
					high = 640;
					break;
			}

			return new BitrateLimits(low, high);
		}
	}
}
