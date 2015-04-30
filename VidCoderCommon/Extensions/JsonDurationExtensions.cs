using System;
using HandBrake.ApplicationServices.Interop.Json.Scan;

namespace VidCoderCommon.Extensions
{
	public static class JsonDurationExtensions
	{
		public static TimeSpan ToSpan(this Duration duration)
		{
			return TimeSpan.FromSeconds((double) duration.Ticks / 90000);
		}
	}
}
