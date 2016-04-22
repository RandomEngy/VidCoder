using System.Globalization;
using HandBrake.ApplicationServices.Interop.Model.Encoding;
using VidCoder.Model;

namespace VidCoder
{
	public static class DisplayConversions
	{
		private static EnumStringConverter<TitleType> titleTypeConverter = new EnumStringConverter<TitleType>();

		public static string DisplayTitleType(TitleType titleType)
		{
			return titleTypeConverter.Convert(titleType);
		}

		public static string DisplayVideoCodecName(string videoCodecName)
		{
			switch (videoCodecName)
			{
				case "h264":
					return "H.264";
				case "mpeg2":
				case "mpeg2video":
					return "MPEG-2";
			}

			return videoCodecName;
		}

		public static string DisplaySampleRate(int sampleRate)
		{
			switch (sampleRate)
			{
				case 48000:
					return "48 kHz";
				case 44100:
					return "44.1 kHz";
				case 32000:
					return "32 kHz";
				case 24000:
					return "24 kHz";
				case 22050:
					return "22.05 kHz";
				case 16000:
					return "16 kHz";
				case 12000:
					return "12 kHz";
				case 11025:
					return "11.025 kHz";
				case 8000:
					return "8 kHz";
			}

			return sampleRate.ToString(CultureInfo.InvariantCulture);
		}
	}
}
