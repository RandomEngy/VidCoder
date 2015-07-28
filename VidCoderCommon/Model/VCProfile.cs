using System;
using System.Collections.Generic;
using Omu.ValueInjecter;
using VidCoderCommon.Utilities.Injection;

namespace VidCoderCommon.Model
{
	/// <summary>
	/// A VidCoder encoding profile.
	/// </summary>
	public class VCProfile
	{
		public VCProfile()
		{
			this.Cropping = new VCCropping();
		}

		#region Obsolete fields
		[Obsolete("Use ContainerName instead.")]
		public VCContainer OutputFormat { get; set; }
		public string X264Options { get; set; }

		// X264Tune is obsolete but marking it that way prevents the XML serializer from working. Use VideoTunes instead.
		public string X264Tune { get; set; }
		public List<string> X264Tunes { get; set; }
		public string X264Profile { get; set; }
		public string X264Preset { get; set; }
		public string QsvPreset { get; set; }
		public string H264Level { get; set; }

		// CustomCropping is obsolete but marking it that way prevents the XML serializer from working. Use CroppingType instead.
		public bool CustomCropping { get; set; }

		[Obsolete("This setting is obsolete. Use Framerate and ConstantFramerate instead.")]
		public bool PeakFramerate { get; set; }

		[Obsolete("Use DenoiseType instead.")]
		public string Denoise { get; set; }
		#endregion

		public string ContainerName { get; set; }
		public VCOutputExtension PreferredExtension { get; set; }
		public bool IncludeChapterMarkers { get; set; }
		public bool LargeFile { get; set; }
		public bool Optimize { get; set; }
		public bool IPod5GSupport { get; set; }

		public int Width { get; set; }
		public int Height { get; set; }
		public int MaxWidth { get; set; }
		public int MaxHeight { get; set; }
		public VCScaleMethod ScaleMethod { get; set; }
		public VCCroppingType CroppingType { get; set; }
		public VCCropping Cropping { get; set; }
		public VCAnamorphic Anamorphic { get; set; }
		public bool UseDisplayWidth { get; set; }
		public int DisplayWidth { get; set; }
		public bool KeepDisplayAspect { get; set; }
		public int PixelAspectX { get; set; }
		public int PixelAspectY { get; set; }
		public int Modulus { get; set; }
		public VCPictureRotation Rotation { get; set; }
		public bool FlipHorizontal { get; set; }
		public bool FlipVertical { get; set; }

		public VCDeinterlace Deinterlace { get; set; }
		public string CustomDeinterlace { get; set; }
		public VCDecomb Decomb { get; set; }
		public string CustomDecomb { get; set; }
		public VCDetelecine Detelecine { get; set; }
		public string CustomDetelecine { get; set; }
		public VCDenoise DenoiseType { get; set; }
		public string DenoisePreset { get; set; }
		public string DenoiseTune { get; set; }
		public bool UseCustomDenoise { get; set; }
		public string CustomDenoise { get; set; }
		public int Deblock { get; set; }
		public bool Grayscale { get; set; }

		public bool UseAdvancedTab { get; set; }
		public string VideoEncoder { get; set; }
		public string VideoOptions { get; set; }
		public string VideoProfile { get; set; }
		public string VideoPreset { get; set; }
		public string VideoLevel { get; set; }
		public List<string> VideoTunes { get; set; }
		public bool QsvDecode { get; set; }
		public VCVideoEncodeRateType VideoEncodeRateType { get; set; }
		public double Quality { get; set; }
		public int TargetSize { get; set; }
		public int VideoBitrate { get; set; }
		public bool TwoPass { get; set; }
		public bool TurboFirstPass { get; set; }
		public double Framerate { get; set; }
		public bool ConstantFramerate { get; set; }

		public List<AudioEncoding> AudioEncodings { get; set; }
		public string AudioEncoderFallback { get; set; }

		public VCProfile Clone()
		{
			var profile = new VCProfile();
			profile.InjectFrom<FastDeepCloneInjection>(this);

			return profile;
		}
	}
}
