using HandBrake.Interop.Interop.Model;
using VidCoderCommon.Model;

namespace VidCoder.Extensions
{
	public static class VCCroppingExtensions
	{
		public static Cropping GetHbCropping(this VCCropping cropping)
		{
			return new Cropping(cropping.Top, cropping.Bottom, cropping.Left, cropping.Right);
		}
	}
}
