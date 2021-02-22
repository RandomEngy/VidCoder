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
			this.Padding = new VCPadding();
		}

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
		public bool Optimize { get; set; }

		[JsonProperty]
		public bool AlignAVStart { get; set; }

		[JsonProperty]
		public bool IPod5GSupport { get; set; }

		[JsonProperty]
		public VCSizingMode SizingMode { get; set; }

		[JsonProperty]
		public int Width { get; set; }

		[JsonProperty]
		public int Height { get; set; }

		[JsonProperty]
		public VCCroppingType CroppingType { get; set; }

		[JsonProperty]
		public VCCropping Cropping { get; set; }

		private VCPadding padding;
		[JsonProperty]
		public VCPadding Padding
		{
			get { return this.padding; }
			set { this.padding = value ?? new VCPadding(); }
		}

		private string padColor;
		[JsonProperty]
		public string PadColor
		{
			get => this.padColor;
			set => this.RaiseAndSetIfChanged(ref this.padColor, value);
		}

		[JsonProperty]
		public VCPaddingMode PaddingMode { get; set; }

		[JsonProperty]
		public bool UseAnamorphic { get; set; }

		[JsonProperty]
		public VCScalingMode ScalingMode { get; set; }

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
		public string Detelecine { get; set; }

		[JsonProperty]
		public string CustomDetelecine { get; set; }


		[JsonProperty]
		public VCDeinterlace DeinterlaceType { get; set; }

		[JsonProperty]
		public string DeinterlacePreset { get; set; }

		[JsonProperty]
		public string CustomDeinterlace { get; set; }


		[JsonProperty]
		public string CombDetect { get; set; }

		[JsonProperty]
		public string CustomCombDetect { get; set; }


		[JsonProperty]
		public VCDenoise DenoiseType { get; set; }

		[JsonProperty]
		public string DenoisePreset { get; set; }

		[JsonProperty]
		public string DenoiseTune { get; set; }

		[JsonProperty]
		public string CustomDenoise { get; set; }


		[JsonProperty]
		public VCSharpen SharpenType { get; set; }

		[JsonProperty]
		public string SharpenPreset { get; set; }

		[JsonProperty]
		public string SharpenTune { get; set; }

		[JsonProperty]
		public string CustomSharpen { get; set; }


		[JsonProperty]
		public int Deblock { get; set; }


		[JsonProperty]
		public bool Grayscale { get; set; }



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

		private string videoPreset;

		[JsonProperty]
		public string VideoPreset
		{
			get { return this.videoPreset; }
			set { this.RaiseAndSetIfChanged(ref this.videoPreset, value); }
		}

		[JsonProperty]
		public string VideoLevel { get; set; }

		private List<string> videoTunes;

		[JsonProperty]
		public List<string> VideoTunes
		{
			get { return this.videoTunes; }
			set { this.RaiseAndSetIfChanged(ref this.videoTunes, value); }
		}

		[JsonProperty]
		public bool QsvDecode { get; set; }

		[JsonProperty]
		public VCVideoEncodeRateType VideoEncodeRateType { get; set; }

		[JsonProperty]
		public decimal Quality { get; set; }

		[JsonProperty]
		public double TargetSize { get; set; }

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


		private List<AudioEncoding> audioEncodings;

		[JsonProperty]
		public List<AudioEncoding> AudioEncodings
		{
			get { return this.audioEncodings; }
			set { this.RaiseAndSetIfChanged(ref this.audioEncodings, value); }
		}

		private List<CopyMaskChoice> audioCopyMask = new List<CopyMaskChoice>();

		/// <summary>
		/// Gets or sets the codecs that can be passed through for auto passthrough.
		/// </summary>
		[JsonProperty]
		public List<CopyMaskChoice> AudioCopyMask
		{
			get { return this.audioCopyMask; }
			set { this.RaiseAndSetIfChanged(ref this.audioCopyMask, value); }
		}

		[JsonProperty]
		public string AudioEncoderFallback { get; set; }

		public VCProfile Clone()
		{
			var profile = new VCProfile();
			profile.InjectFrom<CloneInjection>(this);

			return profile;
		}
	}
}
