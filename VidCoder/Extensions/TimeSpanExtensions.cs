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

		public static string FormatFriendly(this TimeSpan time)
		{
			if (time == TimeSpan.MaxValue)
			{
				return "--";
			}

			if (time.TotalDays >= 1.0)
			{
				return string.Format("{0}d {1}h", Math.Floor(time.TotalDays), time.Hours);
			}

			if (time.TotalHours >= 1.0)
			{
				return string.Format("{0}h {1:d2}m", time.Hours, time.Minutes);
			}

			if (time.TotalMinutes >= 1.0)
			{
				return string.Format("{0}m {1:d2}s", time.Minutes, time.Seconds);
			}

			return string.Format("{0}s", time.Seconds);
		}
	}
}
