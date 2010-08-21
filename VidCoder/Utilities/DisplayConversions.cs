using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VidCoder.Model;
using HandBrake.Interop;

namespace VidCoder
{
	public static class DisplayConversions
	{
		private static EnumStringConverter<AudioEncoder> audioConverter = new EnumStringConverter<AudioEncoder>();
		private static EnumStringConverter<Mixdown> mixdownConverter = new EnumStringConverter<Mixdown>();

		public static string DisplayAudioEncoder(AudioEncoder audioEncoder)
		{
			return audioConverter.Convert(audioEncoder);
		}

		public static string DisplayMixdown(Mixdown mixdown)
		{
			return mixdownConverter.Convert(mixdown);
		}
	}
}
