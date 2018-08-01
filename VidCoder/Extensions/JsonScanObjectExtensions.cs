using HandBrake.Interop.Interop.Json.Scan;
using VidCoder.Model;

namespace VidCoder.Extensions
{
	public static class JsonScanObjectExtensions
	{
		public static VideoSource ToVideoSource(this JsonScanObject scanObject)
		{
			return new VideoSource { FeatureTitle = scanObject.MainFeature, Titles = scanObject.TitleList };
		}
	}
}
