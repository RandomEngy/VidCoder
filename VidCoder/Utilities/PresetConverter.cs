using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HandBrake.Interop.Interop.Json.Presets;
using VidCoderCommon.Model;

namespace VidCoder
{
	public static class PresetConverter
	{
		public static Preset ConvertHandBrakePresetToVC(HBPreset hbPreset)
		{
			var profile = new VCProfile();

			// Container
			profile.ContainerName = hbPreset.FileFormat;
			profile.PreferredExtension = VCOutputExtension.Mp4;
			profile.IncludeChapterMarkers = hbPreset.ChapterMarkers;
			profile.AlignAVStart = hbPreset.AlignAVStart;
			profile.Optimize = hbPreset.Mp4HttpOptimize;
			profile.IPod5GSupport = hbPreset.Mp4iPodCompatible;

			// Sizing
			profile.Cropping = new VCCropping(hbPreset.PictureTopCrop, hbPreset.PictureBottomCrop, hbPreset.PictureLeftCrop, hbPreset.PictureRightCrop);
			profile.CroppingType = hbPreset.PictureAutoCrop ? VCCroppingType.Automatic : VCCroppingType.Custom;
			profile.SizingMode = VCSizingMode.Automatic;
			profile.ScalingMode = VCScalingMode.DownscaleOnly;
			profile.UseAnamorphic = hbPreset.PicturePAR != "off";
			profile.PixelAspectX = 1;
			profile.PixelAspectY = 1;
			profile.Width = hbPreset.PictureWidth ?? 0;
			profile.Height = hbPreset.PictureHeight ?? 0;
			profile.PaddingMode = VCPaddingMode.None;
			profile.Modulus = hbPreset.PictureModulus;
			profile.Rotation = VCPictureRotation.None;
			profile.FlipHorizontal = false;
			profile.FlipVertical = false;

			// Video filters
			profile.Grayscale = hbPreset.VideoGrayScale;
			profile.Detelecine = hbPreset.PictureDetelecine;
			profile.CustomDetelecine = hbPreset.PictureDetelecineCustom;

			switch (hbPreset.PictureDeinterlaceFilter)
			{
				case "yadif":
					profile.DeinterlaceType = VCDeinterlace.Yadif;
					break;
				case "decomb":
					profile.DeinterlaceType = VCDeinterlace.Decomb;
					break;
				case "off":
				default:
					profile.DeinterlaceType = VCDeinterlace.Off;
					break;
			}

			profile.DeinterlacePreset = hbPreset.PictureDeinterlacePreset;
			profile.CustomDeinterlace = hbPreset.PictureDeinterlaceCustom;
			profile.CombDetect = hbPreset.PictureCombDetectPreset;
			profile.CustomCombDetect = hbPreset.PictureCombDetectCustom;

			// Video encoding
			profile.VideoEncoder = hbPreset.VideoEncoder;
			profile.VideoProfile = hbPreset.VideoProfile;
			profile.VideoPreset = hbPreset.VideoPreset;
			profile.VideoLevel = hbPreset.VideoLevel;
			profile.VideoTunes = new List<string>();
			if (!string.IsNullOrEmpty(hbPreset.VideoTune))
			{
				profile.VideoTunes.Add(hbPreset.VideoTune);
			}

			profile.VideoOptions = hbPreset.VideoOptionExtra;
			profile.TwoPass = hbPreset.VideoTwoPass;
			profile.TurboFirstPass = hbPreset.VideoTurboTwoPass;
			profile.Quality = (decimal)hbPreset.VideoQualitySlider;
			profile.VideoBitrate = hbPreset.VideoAvgBitrate ?? 0;
			profile.QsvDecode = false;
			switch (hbPreset.VideoQualityType)
			{
				case 0:
					profile.VideoEncodeRateType = VCVideoEncodeRateType.TargetSize;
					break;
				case 1:
					profile.VideoEncodeRateType = VCVideoEncodeRateType.AverageBitrate;
					break;
				case 2:
				default:
					profile.VideoEncodeRateType = VCVideoEncodeRateType.ConstantQuality;
					break;
			}

			double parsedFramerate;
			double.TryParse(hbPreset.VideoFramerate, out parsedFramerate);
			switch (hbPreset.VideoFramerateMode)
			{
				case "cfr":
					profile.ConstantFramerate = true;
					profile.Framerate = parsedFramerate;
					break;
				case "pfr":
					profile.ConstantFramerate = false;
					profile.Framerate = parsedFramerate;
					break;
				case "vfr":
				default:
					profile.ConstantFramerate = false;
					profile.Framerate = 0;
					break;
			}

			profile.TargetSize = 0;

			// Audio
			profile.AudioCopyMask = new List<CopyMaskChoice>();
			foreach (string audioCodec in hbPreset.AudioCopyMask)
			{
				string codec;

				if (audioCodec.StartsWith("copy:", StringComparison.Ordinal))
				{
					codec = audioCodec.Substring(5);
				}
				else
				{
					codec = audioCodec;
				}

				profile.AudioCopyMask.Add(new CopyMaskChoice { Codec = codec, Enabled = true });
			}

			profile.AudioEncoderFallback = hbPreset.AudioEncoderFallback;

			profile.AudioEncodings = new List<AudioEncoding>();
			foreach (var hbAudio in hbPreset.AudioList)
			{
				var audioEncoding = new AudioEncoding();

				audioEncoding.InputNumber = 0;
				audioEncoding.Encoder = hbAudio.AudioEncoder;
				audioEncoding.Bitrate = hbAudio.AudioBitrate;
				audioEncoding.PassthroughIfPossible = false;
				audioEncoding.Mixdown = hbAudio.AudioMixdown;
				audioEncoding.Quality = (float)hbAudio.AudioTrackQuality;
				audioEncoding.EncodeRateType = hbAudio.AudioTrackQualityEnable ? AudioEncodeRateType.Quality : AudioEncodeRateType.Bitrate;
				audioEncoding.Compression = (float)hbAudio.AudioCompressionLevel;
				if (hbAudio.AudioSamplerate == "auto")
				{
					audioEncoding.SampleRateRaw = 0;
				}
				else
				{
					double parsedSampleRate;
					if (double.TryParse(hbAudio.AudioSamplerate, out parsedSampleRate))
					{
						audioEncoding.SampleRateRaw = (int)(parsedSampleRate * 1000);
					}
					else
					{
						audioEncoding.SampleRateRaw = 0;
					}
				}

				audioEncoding.Gain = (int)hbAudio.AudioTrackGainSlider;
				audioEncoding.Drc = hbAudio.AudioTrackDRCSlider;

				profile.AudioEncodings.Add(audioEncoding);
			}

			return new Preset
			{
				Name = hbPreset.PresetName,
				EncodingProfile = profile,
				IsBuiltIn = true,
			};
		}
	}
}
