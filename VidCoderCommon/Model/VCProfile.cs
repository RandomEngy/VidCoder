using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Omu.ValueInjecter;
using ReactiveUI;
using VidCoderCommon.Utilities.Injection;

namespace VidCoderCommon.Model
{
	/// <summary>
	/// A VidCoder encoding profile.
	/// </summary>
	[JsonObject]
	public class VCProfile : ReactiveObject
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

		private string containerName;

		[JsonProperty]
		public string ContainerName
		{
			get { return this.containerName; }
			set { this.RaiseAndSetIfChanged(ref this.containerName, value); }
		}

		[JsonProperty]
		public VCOutputExtension PreferredExtension { get; set; }

		[JsonProperty]
		public bool IncludeChapterMarkers { get; set; }

		[JsonProperty]
		public bool LargeFile { get; set; }

		[JsonProperty]
		public bool Optimize { get; set; }

		[JsonProperty]
		public bool IPod5GSupport { get; set; }


		[JsonProperty]
		public int Width { get; set; }

		[JsonProperty]
		public int Height { get; set; }

		[JsonProperty]
		public int MaxWidth { get; set; }

		[JsonProperty]
		public int MaxHeight { get; set; }

		public int CappedWidth
		{
			get
			{
				if (this.Width == 0)
				{
					return 0;
				}

				if (this.MaxWidth != 0 && this.Width > this.MaxWidth)
				{
					return this.MaxWidth;
				}

				return this.Width;
			}
		}

		public int CappedHeight
		{
			get
			{
				if (this.Height == 0)
				{
					return 0;
				}

				if (this.MaxHeight != 0 && this.Height > this.MaxHeight)
				{
					return this.MaxHeight;
				}

				return this.Height;
			}
		}

		[JsonProperty]
		public VCScaleMethod ScaleMethod { get; set; }

		[JsonProperty]
		public VCCroppingType CroppingType { get; set; }

		[JsonProperty]
		public VCCropping Cropping { get; set; }

		[JsonProperty]
		public VCAnamorphic Anamorphic { get; set; }

		[JsonProperty]
		public bool UseDisplayWidth { get; set; }

		[JsonProperty]
		public int DisplayWidth { get; set; }

		[JsonProperty]
		public bool KeepDisplayAspect { get; set; }

		[JsonProperty]
		public int PixelAspectX { get; set; }

		[JsonProperty]
		public int PixelAspectY { get; set; }

		[JsonProperty]
		public int Modulus { get; set; }

		private VCPictureRotation rotation;

		[JsonProperty]
		public VCPictureRotation Rotation
		{
			get { return this.rotation; }
			set { this.RaiseAndSetIfChanged(ref this.rotation, value); }
		}

		private bool flipHorizontal;

		[JsonProperty]
		public bool FlipHorizontal
		{
			get { return this.flipHorizontal; }
			set { this.RaiseAndSetIfChanged(ref this.flipHorizontal, value); }
		}

		private bool flipVertical;

		[JsonProperty]
		public bool FlipVertical
		{
			get { return this.flipVertical; }
			set { this.RaiseAndSetIfChanged(ref this.flipVertical, value); }
		}


		[JsonProperty]
		public VCDeinterlace Deinterlace { get; set; }

		[JsonProperty]
		public string CustomDeinterlace { get; set; }

		[JsonProperty]
		public VCDecomb Decomb { get; set; }

		[JsonProperty]
		public string CustomDecomb { get; set; }

		[JsonProperty]
		public VCDetelecine Detelecine { get; set; }

		[JsonProperty]
		public string CustomDetelecine { get; set; }

		[JsonProperty]
		public VCDenoise DenoiseType { get; set; }

		[JsonProperty]
		public string DenoisePreset { get; set; }

		[JsonProperty]
		public string DenoiseTune { get; set; }

		private bool useCustomDenoise;

		[JsonProperty]
		public bool UseCustomDenoise
		{
			get { return this.useCustomDenoise; }
			set { this.RaiseAndSetIfChanged(ref this.useCustomDenoise, value); }
		}

		[JsonProperty]
		public string CustomDenoise { get; set; }

		[JsonProperty]
		public int Deblock { get; set; }

		[JsonProperty]
		public bool Grayscale { get; set; }


		[JsonProperty]
		public bool UseAdvancedTab { get; set; }

		private string videoEncoder;

		[JsonProperty]
		public string VideoEncoder
		{
			get { return this.videoEncoder; }
			set { this.RaiseAndSetIfChanged(ref this.videoEncoder, value); }
		}

		private string videoOptions;

		[JsonProperty]
		public string VideoOptions
		{
			get { return this.videoOptions; }
			set { this.RaiseAndSetIfChanged(ref this.videoOptions, value); }
		}

		[JsonProperty]
		public string VideoProfile { get; set; }

		[JsonProperty]
		public string VideoPreset { get; set; }

		[JsonProperty]
		public string VideoLevel { get; set; }

		[JsonProperty]
		public List<string> VideoTunes { get; set; }

		[JsonProperty]
		public bool QsvDecode { get; set; }

		[JsonProperty]
		public VCVideoEncodeRateType VideoEncodeRateType { get; set; }

		[JsonProperty]
		public double Quality { get; set; }

		[JsonProperty]
		public int TargetSize { get; set; }

		[JsonProperty]
		public int VideoBitrate { get; set; }

		[JsonProperty]
		public bool TwoPass { get; set; }

		[JsonProperty]
		public bool TurboFirstPass { get; set; }

		private double framerate;

		[JsonProperty]
		public double Framerate
		{
			get { return this.framerate; }
			set { this.RaiseAndSetIfChanged(ref this.framerate, value); }
		}

		[JsonProperty]
		public bool ConstantFramerate { get; set; }


		[JsonProperty]
		public List<AudioEncoding> AudioEncodings { get; set; }

		[JsonProperty]
		public string AudioEncoderFallback { get; set; }

		public VCProfile Clone()
		{
			var profile = new VCProfile();
			profile.InjectFrom<FastDeepCloneInjection>(this);

			return profile;
		}
	}
}
