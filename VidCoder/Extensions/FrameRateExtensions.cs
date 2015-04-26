using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HandBrake.ApplicationServices.Interop.Json.Scan;

namespace VidCoder.Extensions
{
	public static class FrameRateExtensions
	{
		public static double ToDouble(this FrameRate frameRate)
		{
			return (double) frameRate.Num / frameRate.Den;
		}
	}
}
