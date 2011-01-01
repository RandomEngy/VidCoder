using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HandBrake.Interop
{
	public static class Converters
	{
		private static Dictionary<double, int> vrates = new Dictionary<double, int>
		{
			{5, 5400000},
			{10, 2700000},
			{12, 2250000},
			{15, 1800000},
			{23.976, 1126125},
			{24, 1125000},
			{25, 1080000},
			{29.97, 900900}
		};

		public static int FramerateToVrate(double framerate)
		{
			if (!vrates.ContainsKey(framerate))
			{
				throw new ArgumentException("Framerate not recognized.", "framerate");
			}

			return vrates[framerate];
		}

		public static int MixdownToNative(Mixdown mixdown)
		{
			if (mixdown == Mixdown.Auto)
			{
				throw new ArgumentException("Cannot convert Auto to native.");
			}

			switch (mixdown)
			{
				case Mixdown.DolbyProLogicII:
					return NativeConstants.HB_AMIXDOWN_DOLBYPLII;
				case Mixdown.DolbySurround:
					return NativeConstants.HB_AMIXDOWN_DOLBY;
				case Mixdown.Mono:
					return NativeConstants.HB_AMIXDOWN_MONO;
				case Mixdown.SixChannelDiscrete:
					return NativeConstants.HB_AMIXDOWN_6CH;
				case Mixdown.Stereo:
					return NativeConstants.HB_AMIXDOWN_STEREO;
			}

			return 0;
		}

		public static uint AudioCodecToNative(AudioEncoder codec)
		{
			switch (codec)
			{
				case AudioEncoder.Ac3Passthrough:
					return NativeConstants.HB_ACODEC_AC3_PASS;
				case AudioEncoder.DtsPassthrough:
					return NativeConstants.HB_ACODEC_DCA_PASS;
				case AudioEncoder.Faac:
					return NativeConstants.HB_ACODEC_FAAC;
				case AudioEncoder.Lame:
					return NativeConstants.HB_ACODEC_LAME;
				case AudioEncoder.Ac3:
					return NativeConstants.HB_ACODEC_AC3;
				case AudioEncoder.Vorbis:
					return NativeConstants.HB_ACODEC_VORBIS;
			}

			return 0;
		}
	}
}
