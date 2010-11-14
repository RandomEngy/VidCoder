using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VidCoder.Model;
using HandBrake.Interop;
using HandBrake.SourceData;

namespace VidCoder
{
	public static class DisplayConversions
	{
		private static EnumStringConverter<AudioEncoder> audioConverter = new EnumStringConverter<AudioEncoder>();
		private static EnumStringConverter<Mixdown> mixdownConverter = new EnumStringConverter<Mixdown>();
		private static EnumStringConverter<InputType> inputTypeConverter = new EnumStringConverter<InputType>();

		public static string DisplayAudioEncoder(AudioEncoder audioEncoder)
		{
			return audioConverter.Convert(audioEncoder);
		}

		public static string DisplayMixdown(Mixdown mixdown)
		{
			return mixdownConverter.Convert(mixdown);
		}

		public static string DisplayInputType(InputType inputType)
		{
			return inputTypeConverter.Convert(inputType);
		}

		public static string DisplayVideoCodecName(string videoCodecName)
		{
			switch (videoCodecName)
			{
				case "h264":
					return "H.264";
				case "mpeg2":
					return "MPEG-2";
			}

			return videoCodecName;
		}
	}
}
