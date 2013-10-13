using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VidCoder.Model
{
	using HandBrake.Interop.Model;
	using HandBrake.Interop.Model.Encoding;
	using Omu.ValueInjecter;

	/// <summary>
	/// An analogue for HBInterop's EncodingProfile.
	/// </summary>
	public class VCProfile
	{
		public VCProfile()
		{
			this.Cropping = new Cropping();
		}

		[Obsolete("Use ContainerName instead.")]
		public Container OutputFormat { get; set; }

		public string ContainerName { get; set; }
		public OutputExtension PreferredExtension { get; set; }
		public bool IncludeChapterMarkers { get; set; }
		public bool LargeFile { get; set; }
		public bool Optimize { get; set; }
		public bool IPod5GSupport { get; set; }

		public int Width { get; set; }
		public int Height { get; set; }
		public int MaxWidth { get; set; }
		public int MaxHeight { get; set; }
		public ScaleMethod ScaleMethod { get; set; }
		
		// CustomCropping is obsolete but marking it that way prevents the XML serializer from working. Use CroppingType instead.
		public bool CustomCropping { get; set; }
		public CroppingType CroppingType { get; set; }
		public Cropping Cropping { get; set; }
		public Anamorphic Anamorphic { get; set; }
		public bool UseDisplayWidth { get; set; }
		public int DisplayWidth { get; set; }
		public bool KeepDisplayAspect { get; set; }
		public int PixelAspectX { get; set; }
		public int PixelAspectY { get; set; }
		public int Modulus { get; set; }

		public Deinterlace Deinterlace { get; set; }
		public string CustomDeinterlace { get; set; }
		public Decomb Decomb { get; set; }
		public string CustomDecomb { get; set; }
		public Detelecine Detelecine { get; set; }
		public string CustomDetelecine { get; set; }
		public Denoise Denoise { get; set; }
		public string CustomDenoise { get; set; }
		public int Deblock { get; set; }
		public bool Grayscale { get; set; }

		public bool UseAdvancedTab { get; set; }
		public string VideoEncoder { get; set; }
		public string X264Options { get; set; }
		public string X264Profile { get; set; }
		public string X264Preset { get; set; }

		// X264Tune is obsolete but marking it that way prevents the XML serializer from working. Use X264Tunes instead.
		public string X264Tune { get; set; }
		public List<string> X264Tunes { get; set; }
		public string QsvPreset { get; set; }
		public bool QsvDecode { get; set; }
		public string H264Level { get; set; }
		public VideoEncodeRateType VideoEncodeRateType { get; set; }
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

		public EncodingProfile HbProfile
		{
			get
			{
				var hbProfile = new EncodingProfile();
				hbProfile.InjectFrom(this);

				if (this.UseAdvancedTab)
				{
					hbProfile.X264Preset = null;
					hbProfile.X264Profile = null;
					hbProfile.X264Tunes = null;
					hbProfile.H264Level = null;
				}

				return hbProfile;
			}
		}

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

				Deinterlace = this.Deinterlace,
				CustomDeinterlace = this.CustomDeinterlace,
				Decomb = this.Decomb,
				CustomDecomb = this.CustomDecomb,
				Detelecine = this.Detelecine,
				CustomDetelecine = this.CustomDetelecine,
				Denoise = this.Denoise,
				CustomDenoise = this.CustomDenoise,
				Deblock = this.Deblock,
				Grayscale = this.Grayscale,

				UseAdvancedTab = this.UseAdvancedTab,
				VideoEncoder = this.VideoEncoder,
				X264Options = this.X264Options,
				X264Profile = this.X264Profile,
				X264Preset = this.X264Preset,
				X264Tunes = this.X264Tunes,
				QsvPreset = this.QsvPreset,
				QsvDecode = this.QsvDecode,
				H264Level = this.H264Level,
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
