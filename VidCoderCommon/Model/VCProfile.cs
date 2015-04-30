using System;
using System.Collections.Generic;

namespace VidCoderCommon.Model
{
	/// <summary>
	/// An analogue for HBInterop's EncodingProfile.
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
		
		// CustomCropping is obsolete but marking it that way prevents the XML serializer from working. Use CroppingType instead.
		public bool CustomCropping { get; set; }
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

		[Obsolete("Use DenoiseType instead.")]
		public string Denoise { get; set; }
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

		[Obsolete("This setting is obsolete. Use Framerate and ConstantFramerate instead.")]
		public bool PeakFramerate { get; set; }

		public List<AudioEncoding> AudioEncodings { get; set; }
		public string AudioEncoderFallback { get; set; }

		public VCProfile Clone()
		{
			var profile = new VCProfile
			{
#pragma warning disable 612, 618
				OutputFormat = this.OutputFormat,
#pragma warning restore 612, 618
				ContainerName = this.ContainerName,
				PreferredExtension = this.PreferredExtension,
				IncludeChapterMarkers = this.IncludeChapterMarkers,
				LargeFile = this.LargeFile,
				Optimize = this.Optimize,
				IPod5GSupport = this.IPod5GSupport,

				Width = this.Width,
				Height = this.Height,
				MaxWidth = this.MaxWidth,
				MaxHeight = this.MaxHeight,
				ScaleMethod = this.ScaleMethod,
				CroppingType = this.CroppingType,
				Cropping = this.Cropping.Clone(),
				Anamorphic = this.Anamorphic,
				UseDisplayWidth = this.UseDisplayWidth,
				DisplayWidth = this.DisplayWidth,
				KeepDisplayAspect = this.KeepDisplayAspect,
				PixelAspectX = this.PixelAspectX,
				PixelAspectY = this.PixelAspectY,
				Modulus = this.Modulus,
				Rotation = this.Rotation,
				FlipHorizontal = this.FlipHorizontal,
				FlipVertical = this.FlipVertical,

				Deinterlace = this.Deinterlace,
				CustomDeinterlace = this.CustomDeinterlace,
				Decomb = this.Decomb,
				CustomDecomb = this.CustomDecomb,
				Detelecine = this.Detelecine,
				CustomDetelecine = this.CustomDetelecine,
				DenoiseType = this.DenoiseType,
				DenoisePreset = this.DenoisePreset,
				DenoiseTune = this.DenoiseTune,
				UseCustomDenoise = this.UseCustomDenoise,
				CustomDenoise = this.CustomDenoise,
				Deblock = this.Deblock,
				Grayscale = this.Grayscale,

				UseAdvancedTab = this.UseAdvancedTab,
				X264Options = this.X264Options,
				X264Profile = this.X264Profile,
				X264Preset = this.X264Preset,
				QsvPreset = this.QsvPreset,
				X264Tunes = this.X264Tunes,
				H264Level = this.H264Level,
				VideoEncoder = this.VideoEncoder,
				VideoOptions = this.VideoOptions,
				VideoProfile = this.VideoProfile,
				VideoPreset = this.VideoPreset,
				VideoTunes = this.VideoTunes,
				VideoLevel = this.VideoLevel,
				QsvDecode = this.QsvDecode,
				VideoEncodeRateType = this.VideoEncodeRateType,
				Quality = this.Quality,
				TargetSize = this.TargetSize,
				VideoBitrate = this.VideoBitrate,
				TwoPass = this.TwoPass,
				TurboFirstPass = this.TurboFirstPass,
				Framerate = this.Framerate,
				ConstantFramerate = this.ConstantFramerate,

				AudioEncodings = new List<AudioEncoding>(this.AudioEncodings),
				AudioEncoderFallback = this.AudioEncoderFallback
			};

			return profile;
		}
	}
}
