using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VidCoder
{
	using HandBrake.Interop.Model;
	using HandBrake.Interop.Model.Encoding;

	public class CodecUtilities
	{
		/// <summary>
		/// Gets overall bitrate limits for the given audio encoder.
		/// </summary>
		/// <param name="encoder">The encoder to check.</param>
		/// <returns>The bitrate limits for the given encoder.</returns>
		public static BitrateLimits GetAudioEncoderLimits(HBAudioEncoder encoder)
		{
			int low = 32;
			int high = 1536;

			switch (encoder.ShortName)
			{
				case "faac":
					low = 32;
					high = 1536;
					break;
				case "ffaac":
					low = 32;
					high = 512;
					break;
				case "vorbis":
					low = 32;
					high = 1536;
					break;
				case "lame":
					low = 32;
					high = 320;
					break;
				case "ffac3":
					low = 48;
					high = 640;
					break;
			}

			return new BitrateLimits { Low = low, High = high };
		}
	}
}
