using System;
using System.Collections.Generic;
using Omu.ValueInjecter;
using ReactiveUI;
using VidCoderCommon.Utilities.Injection;

namespace VidCoderCommon.Model;

/// <summary>
/// A VidCoder encoding profile.
/// </summary>
public class VCProfile : ReactiveObject
{
	public VCProfile()
	{
		this.Cropping = new VCCropping();
		this.Padding = new VCPadding();
	}

	private string containerName;

	public string ContainerName
	{
		get { return this.containerName; }
		set { this.RaiseAndSetIfChanged(ref this.containerName, value); }
	}

	public VCOutputExtension PreferredExtension { get; set; }

	public bool IncludeChapterMarkers { get; set; }

	public bool Optimize { get; set; }

	public bool AlignAVStart { get; set; }

	public bool IPod5GSupport { get; set; }

	public VCSizingMode SizingMode { get; set; }

	public int Width { get; set; }

	public int Height { get; set; }

	public VCCroppingType CroppingType { get; set; }

	public int CroppingMinimum { get; set; } = 2;

	public bool CroppingConstrainToOneAxis { get; set; }

	public VCCropping Cropping { get; set; }

	private VCPadding padding;
	public VCPadding Padding
	{
		get { return this.padding; }
		set { this.padding = value ?? new VCPadding(); }
	}

	private string padColor;
	public string PadColor
	{
		get => this.padColor;
		set => this.RaiseAndSetIfChanged(ref this.padColor, value);
	}

	public VCPaddingMode PaddingMode { get; set; }

	public bool UseAnamorphic { get; set; }

	public VCScalingMode ScalingMode { get; set; }

	public int PixelAspectX { get; set; }

	public int PixelAspectY { get; set; }

	private VCPictureRotation rotation;
	public VCPictureRotation Rotation
	{
		get { return this.rotation; }
		set { this.RaiseAndSetIfChanged(ref this.rotation, value); }
	}

	private bool flipHorizontal;
	public bool FlipHorizontal
	{
		get { return this.flipHorizontal; }
		set { this.RaiseAndSetIfChanged(ref this.flipHorizontal, value); }
	}

	private bool flipVertical;
	public bool FlipVertical
	{
		get { return this.flipVertical; }
		set { this.RaiseAndSetIfChanged(ref this.flipVertical, value); }
	}


	public string Detelecine { get; set; }

	public string CustomDetelecine { get; set; }


	public VCDeinterlace DeinterlaceType { get; set; }

	public string DeinterlacePreset { get; set; }

	public string CustomDeinterlace { get; set; }


	public string CombDetect { get; set; }

	public string CustomCombDetect { get; set; }


	public VCDenoise DenoiseType { get; set; }

	public string DenoisePreset { get; set; }

	public string DenoiseTune { get; set; }

	public string CustomDenoise { get; set; }


	public string ChromaSmoothPreset { get; set; }

	public string ChromaSmoothTune { get; set; }

	public string CustomChromaSmooth { get; set; }


	public VCSharpen SharpenType { get; set; }

	public string SharpenPreset { get; set; }

	public string SharpenTune { get; set; }

	public string CustomSharpen { get; set; }


	public string DeblockPreset { get; set; }

	public string DeblockTune { get; set; }

	public string CustomDeblock { get; set; }


	public string ColorspacePreset { get; set; }

	public string CustomColorspace { get; set; }


	public bool Grayscale { get; set; }



	private string videoEncoder;
	public string VideoEncoder
	{
		get { return this.videoEncoder; }
		set { this.RaiseAndSetIfChanged(ref this.videoEncoder, value); }
	}

	private string videoOptions;
	public string VideoOptions
	{
		get { return this.videoOptions; }
		set { this.RaiseAndSetIfChanged(ref this.videoOptions, value); }
	}

	public string VideoProfile { get; set; }

	private string videoPreset;
	public string VideoPreset
	{
		get { return this.videoPreset; }
		set { this.RaiseAndSetIfChanged(ref this.videoPreset, value); }
	}

	public string VideoLevel { get; set; }

	private List<string> videoTunes;
	public List<string> VideoTunes
	{
		get { return this.videoTunes; }
		set { this.RaiseAndSetIfChanged(ref this.videoTunes, value); }
	}

	public VCVideoEncodeRateType VideoEncodeRateType { get; set; }

	public decimal Quality { get; set; }

	public double TargetSize { get; set; }

	public int VideoBitrate { get; set; }

	public bool MultiPass { get; set; }

	public bool TurboFirstPass { get; set; }

	private double framerate;
	public double Framerate
	{
		get { return this.framerate; }
		set { this.RaiseAndSetIfChanged(ref this.framerate, value); }
	}

	public bool ConstantFramerate { get; set; }


	private List<AudioEncoding> audioEncodings;
	public List<AudioEncoding> AudioEncodings
	{
		get { return this.audioEncodings; }
		set { this.RaiseAndSetIfChanged(ref this.audioEncodings, value); }
	}

	private List<CopyMaskChoice> audioCopyMask = new List<CopyMaskChoice>();

	/// <summary>
	/// Gets or sets the codecs that can be passed through for auto passthrough.
	/// </summary>
	public List<CopyMaskChoice> AudioCopyMask
	{
		get { return this.audioCopyMask; }
		set { this.RaiseAndSetIfChanged(ref this.audioCopyMask, value); }
	}

	public string AudioEncoderFallback { get; set; }

	// Obsolete. Use MultiPass instead.
	public bool TwoPass { get; set; }

	public VCProfile Clone()
	{
		var profile = new VCProfile();
		profile.InjectFrom<CloneInjection>(this);

		return profile;
	}
}
