using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HandBrake.ApplicationServices.Interop.Json.Scan;

namespace VidCoder.Extensions
{
	public static class JsonDurationExtensions
	{
		public static TimeSpan ToSpan(this Duration duration)
		{
			return TimeSpan.FromSeconds((double) duration.Ticks / 90000);
		}
	}
}
