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
	}
}
