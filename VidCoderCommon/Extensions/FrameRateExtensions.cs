using HandBrake.ApplicationServices.Interop.Json.Scan;

namespace VidCoderCommon.Extensions
{
	public static class FrameRateExtensions
	{
		public static double ToDouble(this FrameRate frameRate)
		{
			return (double) frameRate.Num / frameRate.Den;
		}
	}
}
