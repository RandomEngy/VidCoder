﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using HandBrake.Interop.Interop;
using HandBrake.Interop.Interop.HbLib;
using HandBrake.Interop.Interop.Interfaces.Model;
using HandBrake.Interop.Interop.Interfaces.Model.Encoders;
using HandBrake.Interop.Interop.Json.Encode;
using HandBrake.Interop.Interop.Json.Scan;
using HandBrake.Interop.Interop.Json.Shared;
using Microsoft.AnyContainer;
using Omu.ValueInjecter;
using VidCoderCommon.Extensions;
using VidCoderCommon.Services;
using VidCoderCommon.Utilities;
using VidCoderCommon.Utilities.Injection;
using Audio = HandBrake.Interop.Interop.Json.Encode.Audio;
using Metadata = HandBrake.Interop.Interop.Json.Encode.Metadata;
using Source = HandBrake.Interop.Interop.Json.Encode.Source;

namespace VidCoderCommon.Model
{
	public class JsonEncodeFactory
	{
		public const int DefaultMaxWidth = 1920;
		public const int DefaultMaxHeight = 1080;

		private const int ContainerOverheadBytesPerFrame = 6;

		private const int PtsPerSecond = 90000;

		private readonly ILogger logger;

		public JsonEncodeFactory(ILogger logger)
		{
			this.logger = logger;
		}

		/// <summary>
		/// Creates the JSON encode object.
		/// </summary>
		/// <param name="job">The encode job to convert.</param>
		/// <param name="title">The source title.</param>
		/// <param name="defaultChapterNameFormat">The format for a default chapter name.</param>
		/// <param name="previewNumber">The preview number to start at (0-based). Leave off for a normal encode.</param>
		/// <param name="previewSeconds">The number of seconds long to make the preview.</param>
		/// <param name="previewCount">The total number of previews.</param>
		/// <returns>The JSON encode object.</returns>
		public JsonEncodeObject CreateJsonObject(
			VCJob job,
			SourceTitle title,
			string defaultChapterNameFormat,
			int previewNumber = -1,
			int previewSeconds = 0,
			int previewCount = 0)
		{
			if (job == null)
			{
				throw new ArgumentNullException(nameof(job));
			}

			if (title == null)
			{
				throw new ArgumentNullException(nameof(title));
			}

			OutputSizeInfo outputSize = GetOutputSize(job.EncodingProfile, title);
			VCProfile profile = job.EncodingProfile;

			if (profile == null)
			{
				throw new ArgumentException("job must have encoding profile.", nameof(job));
			}
			
			JsonEncodeObject encode = new JsonEncodeObject
			{
				SequenceID = 0,
				Audio = this.CreateAudio(job, title),
				Destination = this.CreateDestination(job, title, defaultChapterNameFormat),
				Filters = this.CreateFilters(profile, title, outputSize),
				Metadata = this.CreateMetadata(job, title),
				PAR = outputSize.Par,
				Source = this.CreateSource(job, title, previewNumber, previewSeconds, previewCount),
				Subtitle = this.CreateSubtitles(job, title),
				Video = this.CreateVideo(job, title, previewSeconds)
			};

			return encode;
		}

		private Audio CreateAudio(VCJob job, SourceTitle title)
		{
			ResolvedAudio resolvedAudio = ResolveAudio(job, title);

			Audio audio = new Audio
			{
				FallbackEncoder = resolvedAudio.FallbackEncoder?.ShortName,
				CopyMask = resolvedAudio.CopyMask,
				AudioList = resolvedAudio.ResolvedTracks.Select(t => t.OutputTrack).ToList()
			};

			return audio;
		}

		/// <summary>
		/// Resolves audio settings to use for the encode.
		/// </summary>
		/// <param name="job">The encode job to convert.</param>
		/// <param name="title">The source title.</param>
		/// <returns>The audio settings to use for the encode.</returns>
		public static ResolvedAudio ResolveAudio(VCJob job, SourceTitle title)
		{
			var resolvedAudio = new ResolvedAudio();

			VCProfile profile = job.EncodingProfile;

			// If using auto-passthrough, set the fallback
			if (profile.AudioEncodings.Any(e => e.Encoder == "copy"))
			{
				resolvedAudio.FallbackEncoder = GetFallbackAudioEncoder(profile);
			}

			if (profile.AudioCopyMask != null && profile.AudioCopyMask.Any())
			{
				resolvedAudio.CopyMask = HandBrakeEncoderHelpers.AudioEncoders
					.Where(e =>
					{
						if (!e.IsPassthrough || !e.ShortName.Contains(":"))
						{
							return false;
						}

						string codecName = e.ShortName.Substring(5);

						CopyMaskChoice profileChoice = profile.AudioCopyMask.FirstOrDefault(choice => choice.Codec == codecName);
						if (profileChoice == null)
						{
							return true;
						}

						return profileChoice.Enabled;
					})
					.Select(e => e.ShortName)
					.ToArray();
			}
			else
			{
				var copyCodecs = new List<string>();
				foreach (var audioEncoder in HandBrakeEncoderHelpers.AudioEncoders)
				{
					if (audioEncoder.IsPassthrough && audioEncoder.ShortName.Contains(":"))
					{
						copyCodecs.Add(audioEncoder.ShortName);
					}
				}

				resolvedAudio.CopyMask = copyCodecs.ToArray();
			}

			List<PairedAudioTrack> pairedTracks = PairAudioTracks(job, title);
			resolvedAudio.ResolvedTracks = ResolveAudioTracks(pairedTracks, resolvedAudio.FallbackEncoder);

			return resolvedAudio;
		}

		public class ResolvedAudio
		{
			public HBAudioEncoder FallbackEncoder { get; set; }

			public string[] CopyMask { get; set; }

			public List<ResolvedAudioTrack> ResolvedTracks { get; set; }
		}

		/// <summary>
		/// Pairs audio encodings with tracks to encode.
		/// </summary>
		/// <param name="job">The encode job</param>
		/// <param name="title">The title the job is meant to encode.</param>
		/// <returns>A list of tracks to encode and what encoding settings to use with them.</returns>
		private static List<PairedAudioTrack> PairAudioTracks(VCJob job, SourceTitle title)
		{
			var list = new List<PairedAudioTrack>();

			foreach (AudioEncoding encoding in job.EncodingProfile.AudioEncodings)
			{
				if (encoding.InputNumber == 0)
				{
					// Add this encoding for all chosen tracks
					foreach (ChosenAudioTrack chosenTrack in job.AudioTracks)
					{
						// In normal cases we'll never have a chosen audio track that doesn't exist but when batch encoding
						// we just choose the first audio track without checking if it exists.
						if (chosenTrack.TrackNumber <= title.AudioList.Count)
						{
							list.Add(new PairedAudioTrack { Encoding = encoding, ChosenTrack = chosenTrack, SourceTrack = title.AudioList[chosenTrack.TrackNumber - 1] });
						}
					}
				}
				else if (encoding.InputNumber <= job.AudioTracks.Count)
				{
					// Add this encoding for the specified track, if it exists
					ChosenAudioTrack chosenTrack = job.AudioTracks[encoding.InputNumber - 1];

					// In normal cases we'll never have a chosen audio track that doesn't exist but when batch encoding
					// we just choose the first audio track without checking if it exists.
					if (chosenTrack.TrackNumber <= title.AudioList.Count)
					{
						list.Add(new PairedAudioTrack { Encoding = encoding, ChosenTrack = chosenTrack, SourceTrack = title.AudioList[chosenTrack.TrackNumber - 1] });
					}
				}
			}

			return list;
		}

		private class PairedAudioTrack
		{
			public AudioEncoding Encoding { get; set; }

			public ChosenAudioTrack ChosenTrack { get; set; }

			public SourceAudioTrack SourceTrack { get; set; }
		}

		/// <summary>
		/// Resolves the picked audio tracks into final encoding settings, taking into account the source track type and encoding settings.
		/// </summary>
		/// <param name="pairedTracks">The paired tracks.</param>
		/// <returns>The final resolved audio encoding settings.</returns>
		private static List<ResolvedAudioTrack> ResolveAudioTracks(List<PairedAudioTrack> pairedTracks, HBAudioEncoder fallbackEncoder)
		{
			var resolvedTracks = new List<ResolvedAudioTrack>();

			foreach (PairedAudioTrack pickedTrack in pairedTracks)
			{
				AudioEncoding encoding = pickedTrack.Encoding;
				ChosenAudioTrack chosenTrack = pickedTrack.ChosenTrack;

				HBAudioEncoder encoder = HandBrakeEncoderHelpers.GetAudioEncoder(encoding.Encoder);
				if (encoder == null)
				{
					throw new InvalidOperationException("Could not find audio encoder " + encoding.Name);
				}

				SourceAudioTrack sourceTrack = pickedTrack.SourceTrack;
				HBAudioEncoder inputCodec = HandBrakeEncoderHelpers.GetAudioEncoder(sourceTrack.Codec);

				uint outputCodec = (uint)encoder.Id;
				bool isPassthrough = encoder.IsPassthrough;

				int passMask = 0;
				foreach (var audioEncoder in HandBrakeEncoderHelpers.AudioEncoders)
				{
					if (audioEncoder.IsPassthrough && audioEncoder.ShortName.Contains(":"))
					{
						passMask |= audioEncoder.Id;
					}
				}

				if (encoding.PassthroughIfPossible &&
					(encoder.Id == sourceTrack.Codec ||
						inputCodec != null && (inputCodec.ShortName.ToLowerInvariant().Contains("aac") && encoder.ShortName.ToLowerInvariant().Contains("aac") ||
						inputCodec.ShortName.ToLowerInvariant().Contains("mp3") && encoder.ShortName.ToLowerInvariant().Contains("mp3"))) &&
					(inputCodec.Id & passMask) > 0)
				{
					outputCodec = (uint)sourceTrack.Codec | HandBrakeNativeConstants.HB_ACODEC_PASS_FLAG;
					isPassthrough = true;
				}

				var outputTrack = new AudioTrack
				{
					Track = chosenTrack.TrackNumber - 1,
					Encoder = HandBrakeEncoderHelpers.GetAudioEncoder((int)outputCodec).ShortName,
				};

				if (!string.IsNullOrEmpty(chosenTrack.Name) && chosenTrack.Name != sourceTrack.Name)
				{
					// If we have picked a name different from the source, use it.
					outputTrack.Name = chosenTrack.Name;
				}
				else if (!string.IsNullOrEmpty(encoding.Name))
				{
					// If the encoding has a name associated with it, use it.
					outputTrack.Name = encoding.Name;
				}
				else if (!string.IsNullOrEmpty(sourceTrack.Name))
				{
					// Else fall back to the name from the source.
					outputTrack.Name = sourceTrack.Name;
				}

				if (isPassthrough && fallbackEncoder != null)
				{
					// If it's passthrough, find the settings for the fallback encoder and apply those, since they will be picked up if the passthrough doesn't work
					OutputAudioTrackInfo fallbackSettings = AudioUtilities.GetDefaultSettings(sourceTrack, fallbackEncoder);

					outputTrack.Samplerate = fallbackSettings.SampleRate;
					outputTrack.Mixdown = fallbackSettings.Mixdown.Id;
					outputTrack.CompressionLevel = fallbackSettings.CompressionLevel;

					if (fallbackSettings.EncodeRateType == AudioEncodeRateType.Bitrate)
					{
						outputTrack.Bitrate = fallbackSettings.Bitrate;
					}
					else
					{
						outputTrack.Quality = fallbackSettings.Quality;
					}

					outputTrack.Gain = fallbackSettings.Gain;
					outputTrack.DRC = fallbackSettings.Drc;
				}
				else if (!isPassthrough)
				{
					outputTrack.Samplerate = encoding.SampleRateRaw;
					outputTrack.Mixdown = HandBrakeEncoderHelpers.GetMixdown(encoding.Mixdown).Id;
					outputTrack.Gain = encoding.Gain;
					outputTrack.DRC = encoding.Drc;

					if (encoder.SupportsCompression)
					{
						outputTrack.CompressionLevel = encoding.Compression;
					}

					switch (encoding.EncodeRateType)
					{
						case AudioEncodeRateType.Bitrate:
							outputTrack.Bitrate = encoding.Bitrate;
							break;
						case AudioEncodeRateType.Quality:
							outputTrack.Quality = encoding.Quality;
							break;
						default:
							throw new ArgumentOutOfRangeException();
					}
				}

				resolvedTracks.Add(new ResolvedAudioTrack
				{
					SourceTrack = sourceTrack,
					OutputTrack = outputTrack
				});
			}

			return resolvedTracks;
		}

		public class ResolvedAudioTrack
		{
			public SourceAudioTrack SourceTrack { get; set; }

			public AudioTrack OutputTrack { get; set; }
		}

		private Destination CreateDestination(VCJob job, SourceTitle title, string defaultChapterNameFormat)
		{
			VCProfile profile = job.EncodingProfile;

			var chapterList = new List<Chapter>();
			if (profile.IncludeChapterMarkers) // TODO: suppress if preview
			{
				int numChapters = title.ChapterList.Count;
				if (job.UseDefaultChapterNames)
				{
					for (int i = 0; i < title.ChapterList.Count; i++)
					{
						SourceChapter sourceChapter = title.ChapterList[i];

						string chapterName;
						if (string.IsNullOrWhiteSpace(sourceChapter.Name))
						{
							chapterName = this.CreateDefaultChapterName(defaultChapterNameFormat, i + 1);
						}
						else
						{
							chapterName = sourceChapter.Name;
						}

						chapterList.Add(new Chapter { Name = chapterName });
					}
				}
				else
				{
					for (int i = 0; i < numChapters; i++)
					{
						if (i < numChapters && i < job.CustomChapterNames.Count)
						{
							string chapterName;
							if (string.IsNullOrWhiteSpace(job.CustomChapterNames[i]))
							{
								chapterName = this.CreateDefaultChapterName(defaultChapterNameFormat, i + 1);
							}
							else
							{
								chapterName = job.CustomChapterNames[i];
							}

							chapterList.Add(new Chapter { Name = chapterName });
						}
					}
				}
			}

			var destination = new Destination
			{
				File = job.PartOutputPath ?? job.FinalOutputPath,
				AlignAVStart = profile.AlignAVStart,
				Mp4Options = new Mp4Options
				{
					IpodAtom = profile.IPod5GSupport,
					Mp4Optimize = profile.Optimize
				},
				Mux = profile.ContainerName,
				ChapterMarkers = profile.IncludeChapterMarkers,
				ChapterList = chapterList
			};

			return destination;
		}


		private string CreateDefaultChapterName(string defaultChapterNameFormat, int chapterNumber)
		{
			return string.Format(
				CultureInfo.CurrentCulture,
				defaultChapterNameFormat,
				chapterNumber);
		}

		private Filters CreateFilters(VCProfile profile, SourceTitle title, OutputSizeInfo outputSizeInfo)
		{
			Filters filters = new Filters
			{
				FilterList = new List<Filter>(),
			};

			// Detelecine
			if (!string.IsNullOrEmpty(profile.Detelecine) && profile.Detelecine != "off")
			{
				string settingsString = profile.Detelecine == "custom" ? profile.CustomDetelecine : null;
				string presetString = profile.Detelecine == "custom" ? "custom" : "default";

				Filter filterItem = new Filter
				{
					ID = (int)hb_filter_ids.HB_FILTER_DETELECINE,
					Settings = this.GetFilterSettingsPresetOnly(hb_filter_ids.HB_FILTER_DETELECINE, presetString, settingsString)
				};
				filters.FilterList.Add(filterItem);
			}

			// Deinterlace
			if (profile.DeinterlaceType != VCDeinterlace.Off)
			{
				hb_filter_ids filterId = profile.DeinterlaceType == VCDeinterlace.Yadif
					? hb_filter_ids.HB_FILTER_DEINTERLACE
					: hb_filter_ids.HB_FILTER_DECOMB;

				JsonDocument settings;
				if (profile.DeinterlacePreset == "custom")
				{
					settings = this.GetFilterSettingsCustom(filterId, profile.CustomDeinterlace);
				}
				else
				{
					settings = this.GetFilterSettingsPresetOnly(filterId, profile.DeinterlacePreset, null);
				}

				if (settings != null)
				{
					Filter filterItem = new Filter { ID = (int)filterId, Settings = settings };
					filters.FilterList.Add(filterItem); 
				}
			}

			// Comb detect
			if (profile.DeinterlaceType != VCDeinterlace.Off && !string.IsNullOrEmpty(profile.CombDetect) && profile.CombDetect != "off")
			{
				JsonDocument settings;
				if (profile.CombDetect == "custom")
				{
					settings = this.GetFilterSettingsCustom(hb_filter_ids.HB_FILTER_COMB_DETECT, profile.CustomCombDetect);
				}
				else
				{
					settings = this.GetFilterSettingsPresetOnly(hb_filter_ids.HB_FILTER_COMB_DETECT, profile.CombDetect, null);
				}

				if (settings != null)
				{
					Filter filterItem = new Filter { ID = (int)hb_filter_ids.HB_FILTER_COMB_DETECT, Settings = settings };
					filters.FilterList.Add(filterItem);
				}
			}

			// VFR / CFR

			// 0 - True VFR, 1 - Constant framerate, 2 - VFR with peak framerate
			if (profile.Framerate > 0 || profile.ConstantFramerate)
			{
				JsonDocument framerateSettings;
				if (profile.Framerate == 0)
				{
					// CFR with "Same as Source". Use the title rate
					framerateSettings = this.GetFramerateSettings(1);
				}
				else
				{
					int framerateMode;

					// Specified framerate
					if (profile.ConstantFramerate)
					{
						// Mark as pure CFR
						framerateMode = 1;
					}
					else
					{
						// Mark as peak framerate
						framerateMode = 2;
					}

					framerateSettings = this.GetFramerateSettings(
						framerateMode,
						27000000,
						HandBrakeUnitConversionHelpers.FramerateToVrate(profile.Framerate));
				}

				Filter framerateShaper = new Filter { ID = (int)hb_filter_ids.HB_FILTER_VFR, Settings = framerateSettings };
				filters.FilterList.Add(framerateShaper);
			}

			// Denoise
			if (profile.DenoiseType != VCDenoise.Off)
			{
				hb_filter_ids filterId = ModelConverters.VCDenoiseToHbDenoise(profile.DenoiseType);

				JsonDocument settings;
				if (profile.DenoisePreset == "custom")
				{
					settings = this.GetFilterSettingsCustom(filterId, profile.CustomDenoise);
				}
				else
				{
					settings = this.GetFilterSettingsPresetAndTune(filterId, profile.DenoisePreset, profile.DenoiseTune, null);
				}

				if (settings != null)
				{
					Filter filterItem = new Filter { ID = (int)filterId, Settings = settings };
					filters.FilterList.Add(filterItem);
				}
			}

			// Chroma smooth
			if (profile.ChromaSmoothPreset != null && profile.ChromaSmoothPreset != "off")
			{
				JsonDocument settings;
				if (profile.ChromaSmoothPreset == "custom")
				{
					settings = this.GetFilterSettingsCustom(hb_filter_ids.HB_FILTER_CHROMA_SMOOTH, profile.CustomChromaSmooth);
				}
				else
				{
					settings = this.GetFilterSettingsPresetAndTune(hb_filter_ids.HB_FILTER_CHROMA_SMOOTH, profile.ChromaSmoothPreset, profile.ChromaSmoothTune, null);
				}

				if (settings != null)
				{
					Filter filterItem = new Filter { ID = (int)hb_filter_ids.HB_FILTER_CHROMA_SMOOTH, Settings = settings };
					filters.FilterList.Add(filterItem);
				}
			}

			// Sharpen
			if (profile.SharpenType != VCSharpen.Off)
			{
				hb_filter_ids filterId = profile.SharpenType == VCSharpen.LapSharp
					? hb_filter_ids.HB_FILTER_LAPSHARP
					: hb_filter_ids.HB_FILTER_UNSHARP;

				JsonDocument settings;
				if (profile.SharpenPreset == "custom")
				{
					settings = this.GetFilterSettingsCustom(filterId, profile.CustomSharpen);
				}
				else
				{
					settings = this.GetFilterSettingsPresetAndTune(filterId, profile.SharpenPreset, profile.SharpenTune, null);
				}

				if (settings != null)
				{
					Filter filterItem = new Filter { ID = (int)filterId, Settings = settings };
					filters.FilterList.Add(filterItem);
				}
			}

			// Deblock
			if (profile.DeblockPreset != null && profile.DeblockPreset != "off")
			{
				JsonDocument settings;
				if (profile.DeblockPreset == "custom")
				{
					settings = this.GetFilterSettingsCustom(hb_filter_ids.HB_FILTER_DEBLOCK, profile.CustomDeblock);
				}
				else
				{
					settings = this.GetFilterSettingsPresetAndTune(hb_filter_ids.HB_FILTER_DEBLOCK, profile.DeblockPreset, profile.DeblockTune, null);
				}

				if (settings != null)
				{
					Filter filterItem = new Filter { ID = (int)hb_filter_ids.HB_FILTER_DEBLOCK, Settings = settings };
					filters.FilterList.Add(filterItem);
				}
			}

			// Colorspace
			if (profile.ColorspacePreset != null && profile.ColorspacePreset != "off")
			{
				JsonDocument settings;
				if (profile.ColorspacePreset == "custom")
				{
					settings = this.GetFilterSettingsCustom(hb_filter_ids.HB_FILTER_COLORSPACE, profile.CustomColorspace);
				}
				else
				{
					settings = this.GetFilterSettingsPresetOnly(hb_filter_ids.HB_FILTER_COLORSPACE, profile.ColorspacePreset, null);
				}

				if (settings != null)
				{
					Filter filterItem = new Filter { ID = (int)hb_filter_ids.HB_FILTER_COLORSPACE, Settings = settings };
					filters.FilterList.Add(filterItem);
				}
			}

			// CropScale Filter
			VCCropping cropping = GetCropping(profile, title);
			JsonDocument cropFilterSettings = this.GetFilterSettingsCustom(
				hb_filter_ids.HB_FILTER_CROP_SCALE,
				string.Format(
					CultureInfo.InvariantCulture,
					"width={0}:height={1}:crop-top={2}:crop-bottom={3}:crop-left={4}:crop-right={5}",
					outputSizeInfo.ScaleWidth,
					outputSizeInfo.ScaleHeight,
					cropping.Top,
					cropping.Bottom,
					cropping.Left,
					cropping.Right));

			Filter cropScale = new Filter
			{
				ID = (int)hb_filter_ids.HB_FILTER_CROP_SCALE,
				Settings = cropFilterSettings
			};
			filters.FilterList.Add(cropScale);

			// Rotate
			if (profile.FlipHorizontal || profile.FlipVertical || profile.Rotation != VCPictureRotation.None)
			{
				int rotateDegrees = (int)profile.Rotation;
				bool flipHorizontal = false;

				if (profile.FlipHorizontal && profile.FlipVertical)
				{
					rotateDegrees = (rotateDegrees + 180) % 360;
				}
				else if (profile.FlipHorizontal)
				{
					flipHorizontal = true;
				}
				else if (profile.FlipVertical)
				{
					rotateDegrees = (rotateDegrees + 180) % 360;
					flipHorizontal = true;
				}

				JsonDocument rotateSettings = this.GetFilterSettingsCustom(
					hb_filter_ids.HB_FILTER_ROTATE,
					string.Format(
						CultureInfo.InvariantCulture,
						"angle={0}:hflip={1}",
						rotateDegrees,
						flipHorizontal ? 1 : 0));

				Filter rotateFilter = new Filter
				{
					ID = (int)hb_filter_ids.HB_FILTER_ROTATE,
					Settings = rotateSettings
				};
				filters.FilterList.Add(rotateFilter);
			}

			// Grayscale
			if (profile.Grayscale)
			{
				Filter filterItem = new Filter { ID = (int)hb_filter_ids.HB_FILTER_GRAYSCALE, Settings = null };
				filters.FilterList.Add(filterItem);
			}

			// Padding
			if (outputSizeInfo.Padding != null && !outputSizeInfo.Padding.IsZero)
			{
				var padColor = !string.IsNullOrEmpty(profile.PadColor) ? profile.PadColor : "000000";
				if (padColor.StartsWith("#"))
				{
					padColor = padColor.Substring(1);
				}

				if (padColor.Length == 3)
				{
					StringBuilder builder = new StringBuilder();
					foreach (char c in padColor)
					{
						builder.Append(c);
						builder.Append(c);
					}

					padColor = builder.ToString();
				}

				JsonDocument padSettings = this.GetFilterSettingsCustom(
					hb_filter_ids.HB_FILTER_PAD,
					FormattableString.Invariant($"width={outputSizeInfo.OutputWidth}:height={outputSizeInfo.OutputHeight}:x={outputSizeInfo.Padding.Left}:y={outputSizeInfo.Padding.Top}:color=0x{padColor}"));
				Filter filterItem = new Filter { ID = (int)hb_filter_ids.HB_FILTER_PAD, Settings = padSettings };
				filters.FilterList.Add(filterItem);
			}

			return filters;
		}

		private JsonDocument GetFilterSettingsCustom(hb_filter_ids filter, string custom)
		{
			return this.GetFilterSettingsPresetAndTune(filter, "custom", null, custom);
		}

		private JsonDocument GetFilterSettingsPresetOnly(hb_filter_ids filter, string preset, string custom)
		{
			return this.GetFilterSettingsPresetAndTune(filter, preset, null, custom);
		}

		private JsonDocument GetFilterSettingsPresetAndTune(hb_filter_ids filter, string preset, string tune, string custom)
		{
			custom = custom?.Trim();
			string unparsedJson = HandBrakeFilterHelpers.GenerateFilterSettingJson((int)filter, preset, tune, custom);

			if (unparsedJson == null)
			{
				this.logger.LogError($"Could not recognize filter settings for '{filter}' with preset '{preset}', tune '{tune}' and custom settings '{custom}'");
				return null;
			}

			return JsonDocument.Parse(unparsedJson);
		}

		private JsonDocument GetFramerateSettings(int framerateMode)
		{
			return this.GetFilterSettingsCustom(
				hb_filter_ids.HB_FILTER_VFR,
				string.Format(
					CultureInfo.InvariantCulture,
					"mode={0}",
					framerateMode));
		}

		private JsonDocument GetFramerateSettings(int framerateMode, int framerateNumerator, int framerateDenominator)
		{
			return this.GetFilterSettingsCustom(
				hb_filter_ids.HB_FILTER_VFR,
				string.Format(
					CultureInfo.InvariantCulture,
					"mode={0}:rate={1}/{2}",
					framerateMode,
					framerateNumerator,
					framerateDenominator));
		}

		private Metadata CreateMetadata(VCJob job, SourceTitle title)
		{
			var metadata = new Metadata();
			if (job.PassThroughMetadata && title.MetaData != null)
			{
				metadata.InjectFrom<CloneInjection>(title.MetaData);
			}

			return metadata;
		}

		/// <summary>
		/// Creates the source sub-object.
		/// </summary>
		/// <param name="job">The encoding job.</param>
		/// <param name="title">The title being encoded.</param>
		/// <param name="previewNumber">The preview number (0-based) to use. -1 for a normal encode.</param>
		/// <param name="previewSeconds">The number of seconds long to make the preview.</param>
		/// <param name="previewCount">The total number of previews.</param>
		/// <returns>The source sub-object</returns>
		private Source CreateSource(VCJob job, SourceTitle title, int previewNumber, int previewSeconds, int previewCount)
		{
			var range = new HandBrake.Interop.Interop.Json.Encode.Range();

			if (previewNumber >= 0)
			{
				range.Type = "preview";
				range.Start = previewNumber + 1;
				range.End = previewSeconds * 90000;
				range.SeekPoints = previewCount;
			}
			else
			{
				switch (job.RangeType)
				{
					case VideoRangeType.All:
						range.Type = "chapter";
						range.Start = 1;
						range.End = title.ChapterList.Count;
						break;
					case VideoRangeType.Chapters:
						range.Type = "chapter";
						range.Start = job.ChapterStart;
						range.End = job.ChapterEnd;
						break;
					case VideoRangeType.Seconds:
						range.Type = "time";
						range.Start = (int)(job.SecondsStart * PtsPerSecond);
						range.End = (int)(job.SecondsEnd * PtsPerSecond);
						break;
					case VideoRangeType.Frames:
						range.Type = "frame";
						range.Start = job.FramesStart + 1; // VidCoder uses 0-based frame numbering, but HB expects 1-based frame numbering
						range.End = job.FramesEnd + 1;
						break;
					default:
						throw new ArgumentOutOfRangeException();
				}
			}

			var source = new Source
			{
				Title = job.Title,
				Angle = job.Angle,
				Path = job.SourcePath,
				Range = range
			};

			return source;
		}

		private Subtitles CreateSubtitles(VCJob job, SourceTitle title)
		{
			Subtitles subtitles = new Subtitles
			{
				Search =
					new SubtitleSearch
					{
						Enable = false,
						Default = false,
						Burn = false,
						Forced = false
					},
				SubtitleList = new List<SubtitleTrack>()
			};

			foreach (ChosenSourceSubtitle sourceSubtitle in job.Subtitles.SourceSubtitles)
			{
				// Handle Foreign Audio Search
				if (sourceSubtitle.TrackNumber == 0)
				{
					subtitles.Search.Enable = true;
					subtitles.Search.Burn = sourceSubtitle.BurnedIn;
					subtitles.Search.Default = sourceSubtitle.Default;
					subtitles.Search.Forced = sourceSubtitle.ForcedOnly;
				}
				else
				{
					SourceSubtitleTrack sourceSubtitleTrack = title.SubtitleList[sourceSubtitle.TrackNumber - 1];
					bool forcedOnly = HandBrakeEncoderHelpers.SubtitleCanSetForcedOnly(sourceSubtitleTrack.Source) && sourceSubtitle.ForcedOnly;
					string name;
					if (string.IsNullOrEmpty(sourceSubtitle.Name))
					{
						name = sourceSubtitleTrack.Name;
					}
					else
					{
						name = sourceSubtitle.Name;
					}

					SubtitleTrack track = new SubtitleTrack
					{
						Burn = sourceSubtitle.BurnedIn,
						Default = sourceSubtitle.Default,
						Forced = forcedOnly,
						ID = sourceSubtitle.TrackNumber,
						Track = sourceSubtitle.TrackNumber - 1,
						Name = name
					};
					subtitles.SubtitleList.Add(track);
				}
			}

			foreach (FileSubtitle fileSubtitle in job.Subtitles.FileSubtitles)
			{
				SubtitleTrack track = new SubtitleTrack
				{
					Track = -1, // Indicates SRT
					Name = fileSubtitle.Name,
					Default = fileSubtitle.Default,
					Offset = fileSubtitle.Offset,
					Burn = fileSubtitle.BurnedIn,
					Import =
						new SubImport
						{
							Format = fileSubtitle.FileName.EndsWith(".srt", StringComparison.OrdinalIgnoreCase) ? "SRT" : "SSA",
							Filename = fileSubtitle.FileName,
							Codeset = fileSubtitle.CharacterCode,
							Language = fileSubtitle.LanguageCode
						}
				};

				subtitles.SubtitleList.Add(track);
			}

			return subtitles;
		}

		private Video CreateVideo(VCJob job, SourceTitle title, int previewLengthSeconds)
		{
			Video video = new Video();
			VCProfile profile = job.EncodingProfile;

			HBVideoEncoder videoEncoder = HandBrakeEncoderHelpers.GetVideoEncoder(profile.VideoEncoder);
			if (videoEncoder == null)
			{
				throw new ArgumentException("Video encoder " + profile.VideoEncoder + " not recognized.");
			}

			video.Encoder = videoEncoder.ShortName;
			video.QSV = new QSV
			{
				Decode = profile.QsvDecode
			};

			video.Level = profile.VideoLevel ?? "auto";
			video.Options = profile.VideoOptions?.Trim();
			video.Preset = profile.VideoPreset;
			video.Profile = profile.VideoProfile ?? "auto";

			switch (profile.VideoEncodeRateType)
			{
				case VCVideoEncodeRateType.TargetSize:
					video.Bitrate = this.CalculateBitrate(job, title, profile.TargetSize, previewLengthSeconds);
					video.TwoPass = profile.TwoPass;
					video.Turbo = profile.TurboFirstPass;
					break;
				case VCVideoEncodeRateType.AverageBitrate:
					video.Bitrate = profile.VideoBitrate;
					video.TwoPass = profile.TwoPass;
					video.Turbo = profile.TurboFirstPass;
					break;
				case VCVideoEncodeRateType.ConstantQuality:
					video.Quality = profile.Quality;
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}

			video.Tune = profile.VideoTunes == null || profile.VideoTunes.Count == 0 ? null : string.Join(",", profile.VideoTunes);

			return video;
		}

		/// <summary>
		/// Calculates the video bitrate for the given job and target size.
		/// </summary>
		/// <param name="job">The encode job.</param>
		/// <param name="title">The title being encoded.</param>
		/// <param name="sizeMb">The target size in MB.</param>
		/// <param name="overallSelectedLengthSeconds">The currently selected encode length. Used in preview
		/// for calculating bitrate when the target size would be wrong.</param>
		/// <param name="logger">The logger to use.</param>
		/// <returns>The video bitrate in kbps.</returns>
		public int CalculateBitrate(VCJob job, SourceTitle title, double sizeMb, double overallSelectedLengthSeconds = 0)
		{
			bool isPreview = overallSelectedLengthSeconds > 0;

			double titleLengthSeconds = this.GetJobLength(job, title).TotalSeconds;

			this.logger.Log($"Calculating bitrate - Title length: {titleLengthSeconds} seconds");

			if (overallSelectedLengthSeconds > 0)
			{
				this.logger.Log($"Calculating bitrate - Selected value length: {overallSelectedLengthSeconds} seconds");
			}

			long availableBytes = (long)(sizeMb * 1024 * 1024);
			if (isPreview)
			{
				availableBytes = (long)(availableBytes * (overallSelectedLengthSeconds / titleLengthSeconds));
			}

			this.logger.Log($"Calculating bitrate - Available bytes: {availableBytes}");

			VCProfile profile = job.EncodingProfile;

			double lengthSeconds = isPreview ? overallSelectedLengthSeconds : titleLengthSeconds;

			double outputFramerate;
			if (profile.Framerate == 0)
			{
				outputFramerate = (double)title.FrameRate.Num / title.FrameRate.Den;
			}
			else
			{
				// Not sure what to do for VFR here hb_calc_bitrate never handled it...
				//   just use the peak for now.
				outputFramerate = profile.Framerate;
			}

			this.logger.Log($"Calculating bitrate - Output framerate: {outputFramerate}");

			long frames = (long)(lengthSeconds * outputFramerate);

			long containerOverheadBytes = frames * ContainerOverheadBytesPerFrame;
			this.logger.Log($"Calculating bitrate - Container overhead: {containerOverheadBytes} bytes");

			availableBytes -= containerOverheadBytes;

			ResolvedAudio resolvedAudio = ResolveAudio(job, title);
			long audioSizeBytes = this.GetAudioSize(lengthSeconds, title, resolvedAudio, job.EncodingProfile.AudioCopyMask);
			this.logger.Log($"Calculating bitrate - Audio size: {audioSizeBytes} bytes");
			availableBytes -= audioSizeBytes;

			this.logger.Log($"Calculating bitrate - Available bytes minus container and audio: {availableBytes}");

			if (availableBytes < 0)
			{
				return 0;
			}

			// Video bitrate is in kilobits per second, or where 1 kbps is 1000 bits per second.
			// So 1 kbps is 125 bytes per second.
			int resultBitrateKilobitsPerSecond = (int)(availableBytes / (125 * lengthSeconds));

			this.logger.Log($"Calculating bitrate - Bitrate result (kbps): {resultBitrateKilobitsPerSecond}");
			return resultBitrateKilobitsPerSecond;
		}

		/// <summary>
		/// Gives estimated file size (in MB) of the given job and video bitrate.
		/// </summary>
		/// <param name="job">The encode job.</param>
		/// <param name="title">The title being encoded.</param>
		/// <param name="videoBitrate">The video bitrate to be used (kbps).</param>
		/// <returns>The estimated file size (in MB) of the given job and video bitrate.</returns>
		public double CalculateFileSize(VCJob job, SourceTitle title, int videoBitrate)
		{
			long totalBytes = 0;

			VCProfile profile = job.EncodingProfile;

			double lengthSeconds = this.GetJobLength(job, title).TotalSeconds;
			lengthSeconds += 1.5;

			double outputFramerate;
			if (profile.Framerate == 0)
			{
				outputFramerate = title.FrameRate.ToDouble();
			}
			else
			{
				// Not sure what to do for VFR here hb_calc_bitrate never handled it...
				//   just use the peak for now.
				outputFramerate = profile.Framerate;
			}

			long frames = (long)(lengthSeconds * outputFramerate);

			totalBytes += (long)(lengthSeconds * videoBitrate * 125);
			totalBytes += frames * ContainerOverheadBytesPerFrame;

			ResolvedAudio resolvedAudio = ResolveAudio(job, title);
			totalBytes += this.GetAudioSize(lengthSeconds, title, resolvedAudio, job.EncodingProfile.AudioCopyMask);

			return (double)totalBytes / 1024 / 1024;
		}

		/// <summary>
		/// Gets the total number of seconds on the given encode job.
		/// </summary>
		/// <param name="job">The encode job to query.</param>
		/// <param name="title">The title being encoded.</param>
		/// <returns>The total number of seconds of video to encode.</returns>
		private TimeSpan GetJobLength(VCJob job, SourceTitle title)
		{
			switch (job.RangeType)
			{
				case VideoRangeType.All:
					return title.Duration.ToSpan();
				case VideoRangeType.Chapters:
					TimeSpan duration = TimeSpan.Zero;
					for (int i = job.ChapterStart; i <= job.ChapterEnd; i++)
					{
						duration += title.ChapterList[i - 1].Duration.ToSpan();
					}

					return duration;
				case VideoRangeType.Seconds:
					return TimeSpan.FromSeconds(job.SecondsEnd - job.SecondsStart);
				case VideoRangeType.Frames:
					return TimeSpan.FromSeconds((job.FramesEnd - job.FramesStart) / ((double)title.FrameRate.Num / title.FrameRate.Den));
			}

			return TimeSpan.Zero;
		}

		/// <summary>
		/// Gets the size in bytes for the audio with the given parameters.
		/// </summary>
		/// <param name="lengthSeconds">The length of the encode in seconds.</param>
		/// <param name="title">The title to encode.</param>
		/// <param name="resolvedAudio">The resolved audio setttings.</param>
		/// <param name="copyMask">The audio copy mask for the job.</param>
		/// <returns>The size in bytes for the audio with the given parameters.</returns>
		private long GetAudioSize(double lengthSeconds, SourceTitle title, ResolvedAudio resolvedAudio, List<CopyMaskChoice> copyMask)
		{
			long audioBytes = 0;
			int outputTrackNumber = 1;

			foreach (ResolvedAudioTrack resolvedTrack in resolvedAudio.ResolvedTracks)
			{
				SourceAudioTrack sourceTrack = resolvedTrack.SourceTrack;
				AudioTrack outputTrack = resolvedTrack.OutputTrack;
				string encoderString = outputTrack.Encoder;

				int samplesPerFrame = this.GetAudioSamplesPerFrame(encoderString);
				int audioBitrate;

				HBAudioEncoder encoder = HandBrakeEncoderHelpers.GetAudioEncoder(encoderString);

				this.logger.Log($"Calculating bitrate - Audio track {outputTrackNumber} - Encoder: {encoderString}");
				int sampleRate = outputTrack.Samplerate == 0 ? sourceTrack.SampleRate : outputTrack.Samplerate;

				if (encoder.IsPassthrough)
				{
				    if (encoder.ShortName == "copy")
				    {
                        // Auto-passthrough

				        if (TrackIsEligibleForPassthrough(sourceTrack, copyMask))
				        {
				            // Input bitrate is in bits/second.
				            audioBitrate = sourceTrack.BitRate / 8;

				            this.logger.Log($"Calculating bitrate - Audio track {outputTrackNumber} - Track is auto passthrough. {audioBitrate} bytes/second");
				        }
				        else
				        {
				            OutputAudioTrackInfo outputTrackInfo = AudioUtilities.GetDefaultSettings(sourceTrack, resolvedAudio.FallbackEncoder);
				            if (outputTrackInfo.EncodeRateType == AudioEncodeRateType.Quality)
				            {
				                audioBitrate = 0;

						        this.logger.Log($"Calculating bitrate - Audio track {outputTrackNumber} - Fallback track for auto-passthrough is quality targeted. Assuming 0 byte size.");
				            }
                            else
				            {
				                audioBitrate = outputTrackInfo.Bitrate;

						        this.logger.Log($"Calculating bitrate - Audio track {outputTrackNumber} - Fallback track for auto-passthrough has {audioBitrate} bytes/second");
				            }
                        }
				    }
				    else
				    {
                        // Passthrough for specific codec

				        if (HandBrakeEncoderHelpers.AudioEncoderIsCompatible(sourceTrack.Codec, encoder))
				        {
				            // Input bitrate is in bits/second.
				            audioBitrate = sourceTrack.BitRate / 8;

				            this.logger.Log($"Calculating bitrate - Audio track {outputTrackNumber} - Track is passthrough via {encoder.ShortName}. {audioBitrate} bytes/second");
				        }
				        else
				        {
				            // Track will be dropped
				            audioBitrate = 0;

						    this.logger.Log($"Calculating bitrate - Audio track {outputTrackNumber} - Track will be dropped due to non compatible passthrough.");
                        }
                    }
				}
				else if (outputTrack.Quality != null)
				{
					// Can't predict size of quality targeted audio encoding.
					audioBitrate = 0;

					this.logger.Log($"Calculating bitrate - Audio track {outputTrackNumber} - Track is quality targeted. Assuming 0 byte size.");
				}
				else
				{
					int outputBitrate;
					if (outputTrack.Bitrate != null && outputTrack.Bitrate > 0)
					{
						outputBitrate = outputTrack.Bitrate.Value;

						this.logger.Log($"Calculating bitrate - Audio track {outputTrackNumber} - Output bitrate set at {outputBitrate} kbps");
					}
					else
					{
						outputBitrate = HandBrakeEncoderHelpers.GetDefaultBitrate(
							encoder,
							sampleRate,
							HandBrakeEncoderHelpers.SanitizeMixdown(HandBrakeEncoderHelpers.GetMixdown(outputTrack.Mixdown), encoder, (ulong)sourceTrack.ChannelLayout));

						this.logger.Log($"Calculating bitrate - Audio track {outputTrackNumber} - Output bitrate is Auto, will be {outputBitrate} kbps");
					}

					// Output bitrate is in kbps.
					audioBitrate = outputBitrate * 1000 / 8;

					this.logger.Log($"Calculating bitrate - Audio track {outputTrackNumber} - {audioBitrate} bytes/second");
				}

				long audioTrackBytes = (long)(lengthSeconds * audioBitrate);
				this.logger.Log($"Calculating bitrate - Audio track {outputTrackNumber} - Audio data is {audioTrackBytes} bytes");
				audioBytes += audioTrackBytes;

				// Audio overhead
				long audioTrackOverheadBytes = sampleRate * ContainerOverheadBytesPerFrame / samplesPerFrame;
				this.logger.Log($"Calculating bitrate - Audio track {outputTrackNumber} - Overhead is {audioTrackOverheadBytes} bytes");
				audioBytes += audioTrackOverheadBytes;

				outputTrackNumber++;
			}

			return audioBytes;
		}

	    private static HBAudioEncoder GetFallbackAudioEncoder(VCProfile profile)
	    {
	        if (!string.IsNullOrEmpty(profile.AudioEncoderFallback))
	        {
	            HBAudioEncoder audioEncoder = HandBrakeEncoderHelpers.GetAudioEncoder(profile.AudioEncoderFallback);
	            if (audioEncoder == null)
	            {
	                throw new ArgumentException("Unrecognized fallback audio encoder: " + profile.AudioEncoderFallback);
	            }

	            return audioEncoder;
	        }

            HBContainer container = HandBrakeEncoderHelpers.GetContainer(profile.ContainerName);
	        foreach (HBAudioEncoder encoder in HandBrakeEncoderHelpers.AudioEncoders)
	        {
	            if ((encoder.CompatibleContainers & container.Id) > 0 && !encoder.IsPassthrough)
	            {
	                return encoder;
	            }
	        }

            throw new ArgumentException("Cannot find fallback audio encoder for profile.");
        }

	    private static bool TrackIsEligibleForPassthrough(SourceAudioTrack track, List<CopyMaskChoice> copyMask)
	    {
		    if (copyMask == null || !copyMask.Any())
		    {
			    return true;
		    }

	        foreach (CopyMaskChoice maskChoice in copyMask)
	        {
	            if (maskChoice.Enabled)
	            {
	                if ((HandBrakeEncoderHelpers.GetAudioEncoder("copy:" + maskChoice.Codec).Id & track.Codec) > 0)
	                {
	                    return true;
	                }
                }
	        }

	        return false;
	    }

		/// <summary>
		/// Gets the number of audio samples used per frame for the given audio encoder.
		/// </summary>
		/// <param name="encoderName">The encoder to query.</param>
		/// <returns>The number of audio samples used per frame for the given
		/// audio encoder.</returns>
		private int GetAudioSamplesPerFrame(string encoderName)
		{
			switch (encoderName)
			{
				case "faac":
				case "ffaac":
				case "copy:aac":
				case "vorbis":
					return 1024;
				case "lame":
				case "copy:mp3":
					return 1152;
				case "ffac3":
				case "copy":
				case "copy:ac3":
				case "copy:dts":
				case "copy:dtshd":
					return 1536;
			}

			// Unknown encoder; make a guess.
			return 1536;
		}

		public static OutputSizeInfo GetOutputSize(VCProfile profile, SourceTitle title)
		{
			if (profile.SizingMode == VCSizingMode.Manual)
			{
				int manualOutputWidth = profile.Width;
				int manualOutputHeight = profile.Height;

				manualOutputWidth += profile.Padding.Left + profile.Padding.Right;
				manualOutputHeight += profile.Padding.Top + profile.Padding.Bottom;

				return new OutputSizeInfo
				{
					ScaleWidth = profile.Width,
					ScaleHeight = profile.Height,
					OutputWidth = manualOutputWidth,
					OutputHeight = manualOutputHeight,
					Par = new PAR { Num = profile.PixelAspectX, Den = profile.PixelAspectY },
					Padding = new VCPadding(profile.Padding)
				};
			}

			// Someday this may be exposed in the UI
			const int modulus = 2;

			int sourceWidth = title.Geometry.Width;
			int sourceHeight = title.Geometry.Height;

			int sourceParWidth = title.Geometry.PAR.Num;
			int sourceParHeight = title.Geometry.PAR.Den;

			// If we are rotating 90/270, swap the source picture width/height and PAR
			if (profile.Rotation == VCPictureRotation.Clockwise90 || profile.Rotation == VCPictureRotation.Clockwise270)
			{
				int temp = sourceWidth;
				sourceWidth = sourceHeight;
				sourceHeight = temp;

				temp = sourceParWidth;
				sourceParWidth = sourceParHeight;
				sourceParHeight = temp;
			}

			VCCropping cropping = GetCropping(profile, title);
			int croppedSourceWidth = sourceWidth - cropping.Left - cropping.Right;
			int croppedSourceHeight = sourceHeight - cropping.Top - cropping.Bottom;

			croppedSourceWidth = Math.Max(croppedSourceWidth, 2);
			croppedSourceHeight = Math.Max(croppedSourceHeight, 2);

			double sourceAspect = (double)croppedSourceWidth / croppedSourceHeight;
			double adjustedSourceAspect; // Source aspect, adjusted for PAR

			int adjustedSourceWidth, adjustedSourceHeight;
			if (profile.UseAnamorphic)
			{
				adjustedSourceWidth = croppedSourceWidth;
				adjustedSourceHeight = croppedSourceHeight;

				adjustedSourceAspect = sourceAspect;
			}
			else
			{
				if (sourceParWidth > sourceParHeight)
				{
					adjustedSourceWidth = (int)Math.Round(croppedSourceWidth * ((double)sourceParWidth / sourceParHeight));
					adjustedSourceHeight = croppedSourceHeight;
				}
				else
				{
					adjustedSourceWidth = croppedSourceWidth;
					adjustedSourceHeight = (int)Math.Round(croppedSourceHeight * ((double)sourceParHeight / sourceParWidth));
				}

				adjustedSourceAspect = sourceAspect * sourceParWidth / sourceParHeight;
			}

			// When using padding fill/width/height, the dimensions we are specifying are post-rotation. We need to adjust here to give the right values to the scale filter.
			int? maxPictureWidth = null, maxPictureHeight = null;
			switch (profile.PaddingMode)
			{
				case VCPaddingMode.Fill:
				case VCPaddingMode.Width:
				case VCPaddingMode.Height:
					maxPictureWidth = profile.Width;
					maxPictureHeight = profile.Height;
					break;
				case VCPaddingMode.Custom:
				case VCPaddingMode.None:
				default:
					if (profile.Width > 0)
					{
						maxPictureWidth = profile.Width;
					}

					if (profile.Height > 0)
					{
						maxPictureHeight = profile.Height;
					}

					break;
			}

			int scalingMultipleCap = profile.ScalingMode.UpscalingMultipleCap();
			if (scalingMultipleCap > 0)
			{
				int maxWidthFromScalingXCap = adjustedSourceWidth * scalingMultipleCap;
				maxPictureWidth = maxPictureWidth.HasValue ? Math.Min(maxWidthFromScalingXCap, maxPictureWidth.Value) : maxWidthFromScalingXCap;

				int maxHeightFromScalingXCap = adjustedSourceHeight * scalingMultipleCap;
				maxPictureHeight = maxPictureHeight.HasValue ? Math.Min(maxHeightFromScalingXCap, maxPictureHeight.Value) : maxHeightFromScalingXCap;
			}

			// How big the picture will be, before padding
			int pictureOutputWidth, pictureOutputHeight;
			if (maxPictureWidth == null && maxPictureHeight == null)
			{
				// This is not technically valid for a preset to leave both off but it can happen in an intermediate stage.
				// Cover so we don't crash.
				maxPictureWidth = DefaultMaxWidth;
				maxPictureHeight = DefaultMaxHeight;
			}

			double scaleFactor;
			if (maxPictureHeight == null || adjustedSourceAspect > (double)maxPictureWidth.Value / maxPictureHeight.Value)
			{
				scaleFactor = (double)maxPictureWidth.Value / adjustedSourceWidth;
			}
			else
			{
				scaleFactor = (double)maxPictureHeight.Value / adjustedSourceHeight;
			}

			if (scalingMultipleCap > 0 && scaleFactor > scalingMultipleCap)
			{
				scaleFactor = scalingMultipleCap;
			}

			pictureOutputWidth = (int)Math.Round(adjustedSourceWidth * scaleFactor);
			pictureOutputHeight = (int)Math.Round(adjustedSourceHeight * scaleFactor);

			int scaleWidth = pictureOutputWidth;
			int scaleHeight = pictureOutputHeight;

			int outputWidth, outputHeight;
			VCPadding padding;
			switch (profile.PaddingMode)
			{
				case VCPaddingMode.None:
					outputWidth = pictureOutputWidth;
					outputHeight = pictureOutputHeight;

					padding = new VCPadding();

					break;
				case VCPaddingMode.Custom:
					outputWidth = pictureOutputWidth + profile.Padding.Left + profile.Padding.Right;
					outputHeight = pictureOutputHeight + profile.Padding.Top + profile.Padding.Bottom;

					padding = new VCPadding(profile.Padding);

					break;
				case VCPaddingMode.Fill:
					outputWidth = profile.Width;
					outputHeight = profile.Height;

					int horizontalPaddingPerSide = (profile.Width - pictureOutputWidth) / 2;
					int verticalPaddingPerSide = (profile.Height - pictureOutputHeight) / 2;

					padding = new VCPadding(verticalPaddingPerSide, verticalPaddingPerSide, horizontalPaddingPerSide, horizontalPaddingPerSide);

					break;
				case VCPaddingMode.Width:
					outputWidth = profile.Width;
					outputHeight = pictureOutputHeight;

					horizontalPaddingPerSide = (profile.Width - pictureOutputWidth) / 2;

					padding = new VCPadding(0, 0, horizontalPaddingPerSide, horizontalPaddingPerSide);

					break;
				case VCPaddingMode.Height:
					outputWidth = pictureOutputWidth;
					outputHeight = profile.Height;

					verticalPaddingPerSide = (profile.Height - pictureOutputHeight) / 2;

					padding = new VCPadding(verticalPaddingPerSide, verticalPaddingPerSide, 0, 0);

					break;
				default:
					throw new ArgumentOutOfRangeException();
			}

			int roundedOutputWidth = MathUtilities.RoundToModulus(outputWidth, modulus);
			int roundedOutputHeight = MathUtilities.RoundToModulus(outputHeight, modulus);

			// Adjust padding to preserve picture size from rounded output
			if ((padding.Left > 0 || padding.Right > 0) && roundedOutputWidth != outputWidth)
			{
				int horizontalPaddingDelta = roundedOutputWidth - outputWidth;
				if (horizontalPaddingDelta > 0)
				{
					int leftPaddingDelta = horizontalPaddingDelta / 2;
					int rightPaddingDelta = horizontalPaddingDelta - leftPaddingDelta;

					padding.Left += leftPaddingDelta;
					padding.Right += rightPaddingDelta;
				}
				else
				{
					int paddingDecrease = -horizontalPaddingDelta;

					// Remove half the difference from the left side
					int leftPaddingDecrease = Math.Min(padding.Left, paddingDecrease / 2);
					int remainingPaddingDecrease = paddingDecrease - leftPaddingDecrease;

					// Try to remove the rest from the right
					int rightPaddingDecrease = Math.Min(padding.Right, remainingPaddingDecrease);
					remainingPaddingDecrease -= rightPaddingDecrease;

					// Try to remove anything remaining from the left
					if (remainingPaddingDecrease > 0 && leftPaddingDecrease < padding.Left)
					{
						leftPaddingDecrease = Math.Min(padding.Left, leftPaddingDecrease + remainingPaddingDecrease);
					}

					padding.Left -= leftPaddingDecrease;
					padding.Right -= rightPaddingDecrease;
				}
			}

			if ((padding.Top > 0 || padding.Bottom > 0) && roundedOutputHeight != outputHeight)
			{
				int verticalPaddingDelta = roundedOutputHeight - outputHeight;
				if (verticalPaddingDelta > 0)
				{
					int topPaddingDelta = verticalPaddingDelta / 2;
					int bottomPaddingDelta = verticalPaddingDelta - topPaddingDelta;

					padding.Top += topPaddingDelta;
					padding.Bottom += bottomPaddingDelta;
				}
				else
				{
					int paddingDecrease = -verticalPaddingDelta;

					// Remove half the difference from the top
					int topPaddingDecrease = Math.Min(padding.Top, paddingDecrease / 2);
					int remainingPaddingDecrease = paddingDecrease - topPaddingDecrease;

					// Try to remove the rest from the bottom
					int bottomPaddingDecrease = Math.Min(padding.Bottom, remainingPaddingDecrease);
					remainingPaddingDecrease -= bottomPaddingDecrease;

					// Try to remove anything remaining from the bottom
					if (remainingPaddingDecrease > 0 && topPaddingDecrease < padding.Top)
					{
						topPaddingDecrease = Math.Min(padding.Top, topPaddingDecrease + remainingPaddingDecrease);
					}

					padding.Top -= topPaddingDecrease;
					padding.Bottom -= bottomPaddingDecrease;
				}
			}

			// Picture size may have changed due to modulus rounding
			int totalHorizontalPadding = padding.Left + padding.Right;
			int totalVerticalPadding = padding.Top + padding.Bottom;
			bool hasHorizontalPadding = totalHorizontalPadding > 0;
			bool hasVerticalPadding = totalVerticalPadding > 0;
			if (!hasHorizontalPadding)
			{
				// The output dimensions at this point are after rotation, so we need to swap to get the pre-rotation sizing dimensions.
				scaleWidth = roundedOutputWidth;
			}

			if (!hasVerticalPadding)
			{
				// The output dimensions at this point are after rotation, so we need to swap to get the pre-rotation sizing dimensions.
				scaleHeight = roundedOutputHeight;
			}

			// Refresh the picture output size after adjustment
			pictureOutputWidth = roundedOutputWidth - totalHorizontalPadding;
			pictureOutputHeight = roundedOutputHeight - totalVerticalPadding;

			PAR outputPar;
			if (profile.UseAnamorphic)
			{
				// Calculate PAR from final picture size
				outputPar = MathUtilities.CreatePar(
					(long)croppedSourceWidth * sourceParWidth * pictureOutputHeight,
					(long)croppedSourceHeight * sourceParHeight * pictureOutputWidth);
			}
			else
			{
				outputPar = new PAR { Num = 1, Den = 1 };
			}

			// Error check the padding
			bool paddingNegative = false;
			if (padding.Left < 0)
			{
				padding.Left = 0;
				paddingNegative = true;
			}

			if (padding.Right < 0)
			{
				padding.Right = 0;
				paddingNegative = true;
			}

			if (padding.Top < 0)
			{
				padding.Top = 0;
				paddingNegative = true;
			}

			if (padding.Bottom < 0)
			{
				padding.Bottom = 0;
				paddingNegative = true;
			}

			if (paddingNegative)
			{
				StaticResolver.Resolve<ILogger>().LogError(
					"Output padding cannot be negative: " + JsonSerializer.Serialize(padding) + Environment.NewLine
					+ $"Calculated from profile: Width {profile.Width}, Height {profile.Height}, "
					+ $"Padding {JsonSerializer.Serialize(profile.Padding)}, PaddingMode {profile.PaddingMode}, "
					+ $"SizingMode {profile.SizingMode}, ScalingMode {profile.ScalingMode}, Rotation {profile.Rotation}, "
					+ $"PixelAspectX {profile.PixelAspectX}, PixelAspectY {profile.PixelAspectY}, UseAnamorphic {profile.UseAnamorphic}" + Environment.NewLine
					+ "And source title geometry: " + JsonSerializer.Serialize(title.Geometry));
			}

			return new OutputSizeInfo
			{
				ScaleWidth = scaleWidth,
				ScaleHeight = scaleHeight,
				OutputWidth = roundedOutputWidth,
				OutputHeight = roundedOutputHeight,
				Padding = padding,
				Par = outputPar
			};
		}

		public static VCCropping GetCropping(VCProfile profile, SourceTitle title)
		{
			switch (profile.CroppingType)
			{
				case VCCroppingType.Automatic:
					return GetAutomaticCropping(profile.Rotation, profile.FlipHorizontal, profile.FlipVertical, profile.CroppingMinimum, profile.CroppingConstrainToOneAxis, title);
				case VCCroppingType.None:
					return new VCCropping(0, 0, 0, 0);
				case VCCroppingType.Custom:
					return profile.Cropping;
				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		public static VCCropping GetAutomaticCropping(VCPictureRotation rotation, bool flipHorizontal, bool flipVertical, int croppingMinimum, bool constrainToOneAxis, SourceTitle title)
		{
			var autoCrop = title.Crop;
			int top = autoCrop[0];
			int bottom = autoCrop[1];
			int left = autoCrop[2];
			int right = autoCrop[3];

			int temp;

			if (flipHorizontal)
			{
				temp = left;
				left = right;
				right = temp;
			}

			if (flipVertical)
			{
				temp = top;
				top = bottom;
				bottom = temp;
			}

			switch (rotation)
			{
				case VCPictureRotation.Clockwise90:
					temp = top;
					top = left;
					left = bottom;
					bottom = right;
					right = temp;
					break;
				case VCPictureRotation.Clockwise180:
					temp = top;
					top = bottom;
					bottom = temp;
					temp = left;
					left = right;
					right = temp;
					break;
				case VCPictureRotation.Clockwise270:
					temp = top;
					top = right;
					right = bottom;
					bottom = left;
					left = temp;
					break;
				default:
					break;
			}

			if (top < croppingMinimum)
			{
				top = 0;
			}

			if (bottom < croppingMinimum)
			{
				bottom = 0;
			}

			if (left < croppingMinimum)
			{
				left = 0;
			}

			if (right < croppingMinimum)
			{
				right = 0;
			}

			if (constrainToOneAxis)
			{
				int vertical = top + bottom;
				int horizontal = left + right;
				if (vertical > horizontal)
				{
					left = right = 0;
				} else
				{
					top = bottom = 0;
				}
			}

			return new VCCropping(top, bottom, left, right);
		}
	}
}
