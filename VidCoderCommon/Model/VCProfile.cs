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

		[JsonProperty]
		public string PadColor { get; set; }

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

		#region Obsolete fields
		[DeserializeOnly]
		public int MaxWidth { get; set; }

		[DeserializeOnly]
		public int MaxHeight { get; set; }

		[DeserializeOnly]
		public VCAnamorphic Anamorphic { get; set; }

		[DeserializeOnly]
		public bool UseDisplayWidth { get; set; }

		[DeserializeOnly]
		public int DisplayWidth { get; set; }

		[DeserializeOnly]
		public bool KeepDisplayAspect { get; set; }

		[DeserializeOnly]
		public VCContainer OutputFormat { get; set; }

		[DeserializeOnly]
		public string X264Options { get; set; }

		// X264Tune is obsolete but marking it that way prevents the XML serializer from working. Use VideoTunes instead.
		[DeserializeOnly]
		public string X264Tune { get; set; }

		[DeserializeOnly]
		public List<string> X264Tunes { get; set; }

		[DeserializeOnly]
		public string X264Profile { get; set; }

		[DeserializeOnly]
		public string X264Preset { get; set; }

		[DeserializeOnly]
		public string QsvPreset { get; set; }

		[DeserializeOnly]
		public string H264Level { get; set; }

		// CustomCropping is obsolete but marking it that way prevents the XML serializer from working. Use CroppingType instead.
		[DeserializeOnly]
		public bool CustomCropping { get; set; }

		[Obsolete("This setting is obsolete. Use Framerate and ConstantFramerate instead.")]
		[DeserializeOnly]
		public bool PeakFramerate { get; set; }

		// Obsolete. Use DeinterlaceType instead.
		[JsonProperty]
		[DeserializeOnly]
		public string Deinterlace { get; set; }

		// Obsolete. DenoisePreset should be "custom" when this is true.
		[JsonProperty]
		[DeserializeOnly]
		public bool UseCustomDenoise { get; set; }

		// Obsolete. Use DeinterlacePreset instead.
		[JsonProperty]
		[DeserializeOnly]
		public string Decomb { get; set; }

		// Obsolete. Use CustomDeinterlace instead.
		[JsonProperty]
		[DeserializeOnly]
		public string CustomDecomb { get; set; }

		[Obsolete("Use DenoiseType instead.")]
		[DeserializeOnly]
		public string Denoise { get; set; }
		#endregion

		public VCProfile Clone()
		{
			var profile = new VCProfile();
			profile.InjectFrom<FastDeepCloneInjection>(this);

			return profile;
		}
	}
}
