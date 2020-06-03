using HandBrake.Interop.Interop.Json.Scan;
using HandBrake.Interop.Interop.Model.Encoding;
using HandBrake.Interop.Interop.Model.Preview;
using VidCoderCommon.Model;

namespace VidCoder.Extensions
{
	public static class VCProfileExtensions
	{
		public static PreviewSettings CreatePreviewSettings(this VCProfile profile, SourceTitle title)
		{
			VCCropping cropping = JsonEncodeFactory.GetCropping(profile, title);

			var outputSize = JsonEncodeFactory.GetOutputSize(profile, title);
			int width = outputSize.ScaleWidth;
			int height = outputSize.ScaleHeight;

			return new PreviewSettings
			{
				Anamorphic = Anamorphic.None,
				Cropping = cropping.GetHbCropping(),
				Width = width,
				Height = height,
				MaxWidth = width,
				MaxHeight = height,
				KeepDisplayAspect = true,
				Modulus = profile.Modulus,
				PixelAspectX = 1,
				PixelAspectY = 1,
				TitleNumber = title.Index
			};
		}
	}
}
