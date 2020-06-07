using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VidCoder.Extensions
{
	using System.Globalization;

	public static class TimeSpanExtensions
	{
		public static string ToFileName(this TimeSpan time)
		{
			return time.ToString("g", CultureInfo.InvariantCulture).Replace(':', '.');
		}

		public static string FormatShort(this TimeSpan time)
		{
			if (time.TotalDays >= 1)
			{
				return time.ToString(@"d\:hh\:mm\:ss");
			}

			if (time.TotalHours >= 1)
			{
				return time.ToString(@"h\:mm\:ss");
			}

			return time.ToString(@"mm\:ss");
		}

		public static string FormatWithHours(this TimeSpan time)
		{
			if (time.TotalDays >= 1)
			{
				return time.ToString(@"d\:hh\:mm\:ss");
			}

			return time.ToString(@"h\:mm\:ss");
		}
	}
}
