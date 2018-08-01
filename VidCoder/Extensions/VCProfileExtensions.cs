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

			// HB doesn't expect us to give it a 0 width and height so we need to guard against that
			int sanitizedWidth = profile.Width > 0 ? profile.Width : title.Geometry.Width - cropping.Left - cropping.Right;
			int sanitizedHeight = profile.Height > 0 ? profile.Height : title.Geometry.Height - cropping.Top - cropping.Bottom;

			return new PreviewSettings
			{
				Anamorphic = EnumConverter.Convert<VCAnamorphic, Anamorphic>(profile.Anamorphic),
				Cropping = JsonEncodeFactory.GetCropping(profile, title).GetHbCropping(),
				Width = sanitizedWidth,
				Height = sanitizedHeight,
				MaxWidth = profile.MaxWidth,
				MaxHeight = profile.MaxHeight,
				KeepDisplayAspect = profile.KeepDisplayAspect,
				Modulus = profile.Modulus,
				PixelAspectX = 1,
				PixelAspectY = 1,
				TitleNumber = title.Index
			};
		}
	}
}
