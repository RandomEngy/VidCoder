using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using HandBrake.ApplicationServices.Interop;
using HandBrake.ApplicationServices.Interop.HbLib;
using HandBrake.ApplicationServices.Interop.Json.Anamorphic;
using HandBrake.ApplicationServices.Interop.Json.Encode;
using HandBrake.ApplicationServices.Interop.Json.Scan;
using HandBrake.ApplicationServices.Interop.Json.Shared;
using HandBrake.ApplicationServices.Interop.Model.Encoding;
using Microsoft.Practices.ServiceLocation;
using Newtonsoft.Json.Linq;
using VidCoderCommon.Extensions;
using VidCoderCommon.Services;
using Audio = HandBrake.ApplicationServices.Interop.Json.Encode.Audio;
using Metadata = HandBrake.ApplicationServices.Interop.Json.Encode.Metadata;
using Source = HandBrake.ApplicationServices.Interop.Json.Encode.Source;

namespace VidCoderCommon.Model
{
	public class JsonEncodeFactory
	{
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
		/// <param name="dxvaDecoding">True to enable DXVA decoding.</param>
		/// <param name="previewNumber">The preview number to start at (0-based). Leave off for a normal encode.</param>
		/// <param name="previewSeconds">The number of seconds long to make the preview.</param>
		/// <param name="previewCount">The total number of previews.</param>
		/// <param name="logger">The logger to use.</param>
		/// <returns></returns>
		public JsonEncodeObject CreateJsonObject(
			VCJob job,
			SourceTitle title,
			string defaultChapterNameFormat,
			bool dxvaDecoding,
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

			Geometry anamorphicSize = GetAnamorphicSize(job.EncodingProfile, title);
			VCProfile profile = job.EncodingProfile;

			if (profile == null)
			{
				throw new ArgumentException("job must have enoding profile.", nameof(job));
			}

			JsonEncodeObject encode = new JsonEncodeObject
			{
				SequenceID = 0,
				Audio = this.CreateAudio(job, title),
				Destination = this.CreateDestination(job, title, defaultChapterNameFormat),
				Filters = this.CreateFilters(profile, title, anamorphicSize),
				Metadata = this.CreateMetadata(),
				PAR = anamorphicSize.PAR,
				Source = this.CreateSource(job, title, previewNumber, previewSeconds, previewCount),
				Subtitle = this.CreateSubtitles(job),
				Video = this.CreateVideo(job, title, previewSeconds, dxvaDecoding)
			};

			return encode;
		}

		private Audio CreateAudio(VCJob job, SourceTitle title)
		{
			Audio audio = new Audio();

			VCProfile profile = job.EncodingProfile;
			List<Tuple<AudioEncoding, int>> outputTrackList = this.GetOutputTracks(job, title);

			if (!string.IsNullOrEmpty(profile.AudioEncoderFallback))
			{
				HBAudioEncoder audioEncoder = HandBrakeEncoderHelpers.GetAudioEncoder(profile.AudioEncoderFallback);
				if (audioEncoder == null)
				{
					throw new ArgumentException("Unrecognized fallback audio encoder: " + profile.AudioEncoderFallback);
				}

				audio.FallbackEncoder = audioEncoder.Id;
			}

			if (profile.AudioCopyMask != null)
			{
				audio.CopyMask = HandBrakeEncoderHelpers.AudioEncoders
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
					.Select(e => (uint)e.Id)
					.ToArray();
			}
			else
			{
				int anyCodec = 0;
				foreach (var audioEncoder in HandBrakeEncoderHelpers.AudioEncoders)
				{
					if (audioEncoder.IsPassthrough && audioEncoder.ShortName.Contains(":"))
					{
						anyCodec |= audioEncoder.Id;
					}
				}

				audio.CopyMask = new uint[] { (uint)anyCodec };
			}

			audio.AudioList = new List<AudioTrack>();

			foreach (Tuple<AudioEncoding, int> outputTrack in outputTrackList)
			{
				AudioEncoding encoding = outputTrack.Item1;
				int trackNumber = outputTrack.Item2;

				HBAudioEncoder encoder = HandBrakeEncoderHelpers.GetAudioEncoder(encoding.Encoder);
				if (encoder == null)
				{
					throw new InvalidOperationException("Could not find audio encoder " + encoding.Name);
				}

				SourceAudioTrack scanAudioTrack = title.AudioList[trackNumber - 1];
				HBAudioEncoder inputCodec = HandBrakeEncoderHelpers.GetAudioEncoder(scanAudioTrack.Codec);

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
					(encoder.Id == scanAudioTrack.Codec ||
						inputCodec != null && (inputCodec.ShortName.ToLowerInvariant().Contains("aac") && encoder.ShortName.ToLowerInvariant().Contains("aac") ||
						inputCodec.ShortName.ToLowerInvariant().Contains("mp3") && encoder.ShortName.ToLowerInvariant().Contains("mp3"))) &&
					(inputCodec.Id & passMask) > 0)
				{
					outputCodec = (uint)scanAudioTrack.Codec | NativeConstants.HB_ACODEC_PASS_FLAG;
					isPassthrough = true;
				}

				var audioTrack = new AudioTrack
				{
					Track = trackNumber - 1,
					Encoder = (int)outputCodec,
				};

				if (!isPassthrough)
				{
					audioTrack.Samplerate = encoding.SampleRateRaw;
					audioTrack.Mixdown = HandBrakeEncoderHelpers.GetMixdown(encoding.Mixdown).Id;
					audioTrack.Gain = encoding.Gain;
					audioTrack.DRC = encoding.Drc;

					if (encoder.SupportsCompression)
					{
						audioTrack.CompressionLevel = encoding.Compression;
					}

					switch (encoding.EncodeRateType)
					{
						case AudioEncodeRateType.Bitrate:
							audioTrack.Bitrate = encoding.Bitrate;
							break;
						case AudioEncodeRateType.Quality:
							audioTrack.Quality = encoding.Quality;
							break;
						default:
							throw new ArgumentOutOfRangeException();
					}
				}

				audio.AudioList.Add(audioTrack);
			}

			return audio;
		}

		/// <summary>
		/// Gets a list of encodings and target track indices (1-based).
		/// </summary>
		/// <param name="job">The encode job</param>
		/// <param name="title">The title the job is meant to encode.</param>
		/// <returns>A list of encodings and target track indices (1-based).</returns>
		private List<Tuple<AudioEncoding, int>> GetOutputTracks(VCJob job, SourceTitle title)
		{
			var list = new List<Tuple<AudioEncoding, int>>();

			foreach (AudioEncoding encoding in job.EncodingProfile.AudioEncodings)
			{
				if (encoding.InputNumber == 0)
				{
					// Add this encoding for all chosen tracks
					foreach (int chosenTrack in job.ChosenAudioTracks)
					{
						// In normal cases we'll never have a chosen audio track that doesn't exist but when batch encoding
						// we just choose the first audio track without checking if it exists.
						if (chosenTrack <= title.AudioList.Count)
						{
							list.Add(new Tuple<AudioEncoding, int>(encoding, chosenTrack));
						}
					}
				}
				else if (encoding.InputNumber <= job.ChosenAudioTracks.Count)
				{
					// Add this encoding for the specified track, if it exists
					int trackNumber = job.ChosenAudioTracks[encoding.InputNumber - 1];

					// In normal cases we'll never have a chosen audio track that doesn't exist but when batch encoding
					// we just choose the first audio track without checking if it exists.
					if (trackNumber <= title.AudioList.Count)
					{
						list.Add(new Tuple<AudioEncoding, int>(encoding, trackNumber));
					}
				}
			}

			return list;
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
				File = job.OutputPath,
				Mp4Options = new Mp4Options
				{
					IpodAtom = profile.IPod5GSupport,
					Mp4Optimize = profile.Optimize
				},
				Mux = HBFunctions.hb_container_get_from_name(profile.ContainerName),
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

		private Filters CreateFilters(VCProfile profile, SourceTitle title, Geometry anamorphicSize)
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
					Settings = this.GetFilterSettings(hb_filter_ids.HB_FILTER_DETELECINE, presetString, settingsString)
				};
				filters.FilterList.Add(filterItem);
			}

			// Deinterlace
			if (profile.DeinterlaceType != VCDeinterlace.Off)
			{
				hb_filter_ids filterId = profile.DeinterlaceType == VCDeinterlace.Yadif
					? hb_filter_ids.HB_FILTER_DEINTERLACE
					: hb_filter_ids.HB_FILTER_DECOMB;

				JToken settings;
				if (profile.DeinterlacePreset == "custom")
				{
					settings = this.GetFilterSettings(filterId, "custom", profile.CustomDeinterlace);
				}
				else
				{
					settings = this.GetFilterSettings(filterId, profile.DeinterlacePreset, null);
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
				JToken settings;
				if (profile.CombDetect == "custom")
				{
					settings = this.GetFilterSettings(hb_filter_ids.HB_FILTER_COMB_DETECT, "custom", profile.CustomCombDetect);
				}
				else
				{
					settings = this.GetFilterSettings(hb_filter_ids.HB_FILTER_COMB_DETECT, profile.CombDetect, null);
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
				JToken framerateSettings;
				if (profile.Framerate == 0)
				{
					// CFR with "Same as Source". Use the title rate
					framerateSettings = this.GetFramerateSettings(1, title.FrameRate.Num, title.FrameRate.Den);
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

			// Deblock
			if (profile.Deblock >= 5)
			{
				JToken settings = this.GetFilterSettings(
					hb_filter_ids.HB_FILTER_DEBLOCK,
					string.Format(CultureInfo.InvariantCulture, "qp={0}", profile.Deblock));

				Filter filterItem = new Filter { ID = (int)hb_filter_ids.HB_FILTER_DEBLOCK, Settings = settings };
				filters.FilterList.Add(filterItem);
			}

			// Denoise
			if (profile.DenoiseType != VCDenoise.Off)
			{
				hb_filter_ids filterId = profile.DenoiseType == VCDenoise.hqdn3d
					? hb_filter_ids.HB_FILTER_HQDN3D
					: hb_filter_ids.HB_FILTER_NLMEANS;

				JToken settings;
				if (profile.DenoisePreset == "custom")
				{
					settings = this.GetFilterSettings(filterId, "custom", profile.CustomDenoise);
				}
				else
				{
					settings = this.GetFilterSettings(filterId, profile.DenoisePreset, profile.DenoiseTune);
				}

				if (settings != null)
				{
					Filter filterItem = new Filter { ID = (int)filterId, Settings = settings };
					filters.FilterList.Add(filterItem);
				}
			}

			// Grayscale
			if (profile.Grayscale)
			{
				Filter filterItem = new Filter { ID = (int)hb_filter_ids.HB_FILTER_GRAYSCALE, Settings = null };
				filters.FilterList.Add(filterItem);
			}

			// CropScale Filter
			VCCropping cropping = GetCropping(profile, title);
			JToken cropFilterSettings = this.GetFilterSettings(
				hb_filter_ids.HB_FILTER_CROP_SCALE,
				string.Format(
					CultureInfo.InvariantCulture,
					"width={0}:height={1}:crop-top={2}:crop-bottom={3}:crop-left={4}:crop-right={5}",
					anamorphicSize.Width,
					anamorphicSize.Height,
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

				JToken rotateSettings = this.GetFilterSettings(
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

			return filters;
		}

		private JToken GetFilterSettings(hb_filter_ids filter, string custom)
		{
			return this.GetFilterSettings(filter, null, null, custom);
		}

		private JToken GetFilterSettings(hb_filter_ids filter, string preset, string custom)
		{
			return this.GetFilterSettings(filter, preset, null, custom);
		}

		private JToken GetFilterSettings(hb_filter_ids filter, string preset, string tune, string custom)
		{
			IntPtr settingsPtr = HBFunctions.hb_generate_filter_settings_json((int)filter, preset, tune, custom);
			string unparsedJson = Marshal.PtrToStringAnsi(settingsPtr);

			if (unparsedJson == null)
			{
				this.logger.LogError($"Could not recognize filter settings for '{filter}' with preset '{preset}', tune '{tune}' and custom settings '{custom}'");
				return null;
			}

			return JObject.Parse(unparsedJson);
		}

		private JToken GetFramerateSettings(int framerateMode, int framerateNumerator, int framerateDenominator)
		{
			return this.GetFilterSettings(
				hb_filter_ids.HB_FILTER_VFR,
				string.Format(
					CultureInfo.InvariantCulture,
					"mode={0}:rate={1}/{2}",
					framerateMode,
					framerateNumerator,
					framerateDenominator));
		}

		private Metadata CreateMetadata()
		{
			return new Metadata();
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
			var range = new Range();

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
						range.End = (int)((job.SecondsEnd - job.SecondsStart) * PtsPerSecond);
						break;
					case VideoRangeType.Frames:
						range.Type = "frame";
						range.Start = job.FramesStart;
						range.End = job.FramesEnd;
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

		private Subtitles CreateSubtitles(VCJob job)
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

			foreach (SourceSubtitle sourceSubtitle in job.Subtitles.SourceSubtitles)
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
					SubtitleTrack track = new SubtitleTrack
					{
						Burn = sourceSubtitle.BurnedIn,
						Default = sourceSubtitle.Default,
						Forced = sourceSubtitle.ForcedOnly,
						ID = sourceSubtitle.TrackNumber,
						Track = (sourceSubtitle.TrackNumber - 1)
					};
					subtitles.SubtitleList.Add(track);
				}
			}

			foreach (SrtSubtitle srtSubtitle in job.Subtitles.SrtSubtitles)
			{
				SubtitleTrack track = new SubtitleTrack
				{
					Track = -1, // Indicates SRT
					Default = srtSubtitle.Default,
					Offset = srtSubtitle.Offset,
					Burn = srtSubtitle.BurnedIn,
					SRT =
						new SRT
						{
							Filename = srtSubtitle.FileName,
							Codeset = srtSubtitle.CharacterCode,
							Language = srtSubtitle.LanguageCode
						}
				};

				subtitles.SubtitleList.Add(track);
			}

			return subtitles;
		}

		private Video CreateVideo(VCJob job, SourceTitle title, int previewLengthSeconds, bool dxvaDecoding)
		{
			Video video = new Video();
			VCProfile profile = job.EncodingProfile;

			HBVideoEncoder videoEncoder = HandBrakeEncoderHelpers.GetVideoEncoder(profile.VideoEncoder);
			if (videoEncoder == null)
			{
				throw new ArgumentException("Video encoder " + profile.VideoEncoder + " not recognized.");
			}

			video.Encoder = videoEncoder.Id;
			video.HWDecode = dxvaDecoding;
			video.QSV = new QSV
			{
				Decode = profile.QsvDecode
			};

			if (profile.UseAdvancedTab)
			{
				video.Options = profile.VideoOptions;
			}
			else
			{
				video.Level = profile.VideoLevel;
				video.Options = profile.VideoOptions;
				video.Preset = profile.VideoPreset;
				video.Profile = profile.VideoProfile;
			}

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
		public int CalculateBitrate(VCJob job, SourceTitle title, int sizeMb, double overallSelectedLengthSeconds = 0)
		{
			bool isPreview = overallSelectedLengthSeconds > 0;

			double titleLengthSeconds = this.GetJobLength(job, title).TotalSeconds;

			this.logger.Log($"Calculating bitrate - Title length: {titleLengthSeconds} seconds");

			if (overallSelectedLengthSeconds > 0)
			{
				this.logger.Log($"Calculating bitrate - Selected value length: {overallSelectedLengthSeconds} seconds");
			}

			long availableBytes = ((long)sizeMb) * 1024 * 1024;
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

			List<Tuple<AudioEncoding, int>> outputTrackList = this.GetOutputTracks(job, title);
			long audioSizeBytes = this.GetAudioSize(lengthSeconds, title, outputTrackList);
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

			List<Tuple<AudioEncoding, int>> outputTrackList = this.GetOutputTracks(job, title);
			totalBytes += this.GetAudioSize(lengthSeconds, title, outputTrackList);

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
		/// <param name="outputTrackList">The list of tracks to encode.</param>
		/// <returns>The size in bytes for the audio with the given parameters.</returns>
		private long GetAudioSize(double lengthSeconds, SourceTitle title, List<Tuple<AudioEncoding, int>> outputTrackList)
		{
			long audioBytes = 0;
			int outputTrackNumber = 1;

			foreach (Tuple<AudioEncoding, int> outputTrack in outputTrackList)
			{
				AudioEncoding encoding = outputTrack.Item1;
				SourceAudioTrack track = title.AudioList[outputTrack.Item2 - 1];

				int samplesPerFrame = this.GetAudioSamplesPerFrame(encoding.Encoder);
				int audioBitrate;

				HBAudioEncoder audioEncoder = HandBrakeEncoderHelpers.GetAudioEncoder(encoding.Encoder);

				if (audioEncoder.IsPassthrough)
				{
					// Input bitrate is in bits/second.
					audioBitrate = track.BitRate / 8;

					this.logger.Log($"Calculating bitrate - Audio track {outputTrackNumber} - Track is passthrough. {audioBitrate} bytes/second");
				}
				else if (encoding.EncodeRateType == AudioEncodeRateType.Quality)
				{
					// Can't predict size of quality targeted audio encoding.
					audioBitrate = 0;

					this.logger.Log($"Calculating bitrate - Audio track {outputTrackNumber} - Track is quality targeted. Assuming 0 byte overhead.");
				}
				else
				{
					int outputBitrate;
					if (encoding.Bitrate > 0)
					{
						outputBitrate = encoding.Bitrate;

						this.logger.Log($"Calculating bitrate - Audio track {outputTrackNumber} - Output bitrate set at {outputBitrate} kbps");
					}
					else
					{
						outputBitrate = HandBrakeEncoderHelpers.GetDefaultBitrate(
							audioEncoder,
							encoding.SampleRateRaw == 0 ? track.SampleRate : encoding.SampleRateRaw,
							HandBrakeEncoderHelpers.SanitizeMixdown(HandBrakeEncoderHelpers.GetMixdown(encoding.Mixdown), audioEncoder, (ulong)track.ChannelLayout));

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
				long audioTrackOverheadBytes = encoding.SampleRateRaw * ContainerOverheadBytesPerFrame / samplesPerFrame;
				this.logger.Log($"Calculating bitrate - Audio track {outputTrackNumber} - Overhead is {audioTrackOverheadBytes} bytes");
				audioBytes += audioTrackOverheadBytes;

				outputTrackNumber++;
			}

			return audioBytes;
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

		public static Geometry GetAnamorphicSize(VCProfile profile, SourceTitle title)
		{
			if (profile == null)
			{
				throw new ArgumentNullException(nameof(profile));
			}

			if (title == null)
			{
				throw new ArgumentNullException(nameof(title));
			}

			int modulus;
			if (profile.Anamorphic == VCAnamorphic.Loose || profile.Anamorphic == VCAnamorphic.Custom)
			{
				modulus = profile.Modulus;
			}
			else
			{
				modulus = 2;
			}

			VCCropping cropping = GetCropping(profile, title);
			List<int> croppingList = new List<int> { cropping.Top, cropping.Bottom, cropping.Left, cropping.Right };

			int sourceCroppedWidth = title.Geometry.Width - cropping.Left - cropping.Right;
			int sourceCroppedHeight = title.Geometry.Height - cropping.Top - cropping.Bottom;

			PAR par;
			if (profile.Anamorphic == VCAnamorphic.Custom)
			{
				if (profile.UseDisplayWidth)
				{
					// Figure PAR to hit the desired display width.
					if (profile.Width > 0)
					{
						par = new PAR { Num = profile.DisplayWidth, Den = profile.CappedWidth };
					}
					else
					{
						int cappedWidth;

						if (profile.MaxWidth != 0 && sourceCroppedWidth > profile.MaxWidth)
						{
							cappedWidth = profile.MaxWidth;
						}
						else
						{
							cappedWidth = sourceCroppedWidth;
						}

						par = new PAR { Num = profile.DisplayWidth, Den = cappedWidth };
					}
				}
				else
				{
					// User has manually specified PAR.
					par = new PAR { Num = profile.PixelAspectX, Den = profile.PixelAspectY };
				}
			}
			else
			{
				par = new PAR { Num = title.Geometry.PAR.Num, Den = title.Geometry.PAR.Den };
			}

			par.Simplify();

			// HB doesn't expect us to give it a 0 width and height so we need to guard against that
			int outputStorageWidth = profile.Width;
			int outputStorageHeight = profile.Height;
			if (outputStorageWidth == 0)
			{
				outputStorageWidth = sourceCroppedWidth;
			}

			if (outputStorageHeight == 0)
			{
				outputStorageHeight = sourceCroppedHeight;
			}

			int keepValue = 0;
			if (profile.KeepDisplayAspect && profile.Anamorphic != VCAnamorphic.Custom)
			{
				keepValue = keepValue | NativeConstants.HB_KEEP_DISPLAY_ASPECT;

				if (profile.Width > 0 && profile.Height == 0)
				{
					keepValue = keepValue | NativeConstants.HB_KEEP_WIDTH;
				}

				if (profile.Height > 0 && profile.Width == 0)
				{
					keepValue = keepValue | NativeConstants.HB_KEEP_HEIGHT;
				}
			}

			AnamorphicGeometry anamorphicGeometry = new AnamorphicGeometry
			{
				SourceGeometry = title.Geometry,
				DestSettings = new DestSettings
				{
					AnamorphicMode = (int)profile.Anamorphic,
					Geometry = new Geometry
					{
						Width = outputStorageWidth,
						Height = outputStorageHeight,
						PAR = par,
					},
					Keep = keepValue,
					Crop = croppingList,
					Modulus = modulus,
					MaxWidth = profile.MaxWidth,
					MaxHeight = profile.MaxHeight,
					ItuPAR = false
				}
			};

			return HandBrakeUtils.GetAnamorphicSize(anamorphicGeometry);
		}

		public static VCCropping GetCropping(VCProfile profile, SourceTitle title)
		{
			switch (profile.CroppingType)
			{
				case VCCroppingType.Automatic:
					var autoCrop = title.Crop;
					return new VCCropping(autoCrop[0], autoCrop[1], autoCrop[2], autoCrop[3]);
				case VCCroppingType.None:
					return new VCCropping(0, 0, 0, 0);
				case VCCroppingType.Custom:
					return profile.Cropping;
				default:
					throw new ArgumentOutOfRangeException();
			}
		}
	}
}
