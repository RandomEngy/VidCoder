using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using HandBrake.ApplicationServices.Interop;
using HandBrake.ApplicationServices.Interop.HbLib;
using HandBrake.ApplicationServices.Interop.Json.Anamorphic;
using HandBrake.ApplicationServices.Interop.Json.Encode;
using HandBrake.ApplicationServices.Interop.Json.Scan;
using HandBrake.ApplicationServices.Interop.Json.Shared;
using HandBrake.ApplicationServices.Interop.Model.Encoding;
using VidCoderCommon.Extensions;
using Audio = HandBrake.ApplicationServices.Interop.Json.Encode.Audio;
using Metadata = HandBrake.ApplicationServices.Interop.Json.Encode.Metadata;
using Source = HandBrake.ApplicationServices.Interop.Json.Encode.Source;

namespace VidCoderCommon.Model
{
	public static class JsonEncodeFactory
	{
		private const int ContainerOverheadBytesPerFrame = 6;

		private const int PtsPerSecond = 90000;

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
		/// <returns></returns>
		public static JsonEncodeObject CreateJsonObject(
			VCJob job,
			SourceTitle title, 
			string defaultChapterNameFormat,
			bool dxvaDecoding,
			int previewNumber = -1,
			int previewSeconds = 0,
			int previewCount = 0)
		{
			Geometry anamorphicSize = GetAnamorphicSize(job.EncodingProfile, title);
			VCProfile profile = job.EncodingProfile;

			JsonEncodeObject encode = new JsonEncodeObject
			{
				SequenceID = 0,
				Audio = CreateAudio(job, title),
				Destination = CreateDestination(job, title, defaultChapterNameFormat),
				Filters = CreateFilters(profile, title, anamorphicSize),
				Metadata = CreateMetadata(),
				PAR = anamorphicSize.PAR,
				Source = CreateSource(job, title, previewNumber, previewSeconds, previewCount),
				Subtitle = CreateSubtitles(job),
				Video = CreateVideo(job, title, previewSeconds, dxvaDecoding)
			};

			return encode;
		}

		private static Audio CreateAudio(VCJob job, SourceTitle title)
		{
			Audio audio = new Audio();

			VCProfile profile = job.EncodingProfile;
			List<Tuple<AudioEncoding, int>> outputTrackList = GetOutputTracks(job, title);

            if (!string.IsNullOrEmpty(profile.AudioEncoderFallback))
            {
                HBAudioEncoder audioEncoder = HandBrakeEncoderHelpers.GetAudioEncoder(profile.AudioEncoderFallback);
                if (audioEncoder == null)
                {
                    throw new ArgumentException("Unrecognized fallback audio encoder: " + profile.AudioEncoderFallback);
                }

				audio.FallbackEncoder = HandBrakeEncoderHelpers.GetAudioEncoder(profile.AudioEncoderFallback).Id;
            }

			audio.CopyMask = new uint[] { NativeConstants.HB_ACODEC_ANY };
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

				if (encoding.PassthroughIfPossible && 
					(encoder.Id == scanAudioTrack.Codec || 
						inputCodec != null && (inputCodec.ShortName.ToLowerInvariant().Contains("aac") && encoder.ShortName.ToLowerInvariant().Contains("aac") ||
						inputCodec.ShortName.ToLowerInvariant().Contains("mp3") && encoder.ShortName.ToLowerInvariant().Contains("mp3"))) &&
					(inputCodec.Id & NativeConstants.HB_ACODEC_PASS_MASK) > 0)
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
		private static List<Tuple<AudioEncoding, int>> GetOutputTracks(VCJob job, SourceTitle title)
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

		private static Destination CreateDestination(VCJob job, SourceTitle title, string defaultChapterNameFormat)
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
						chapterList.Add(new Chapter { Name = CreateDefaultChapterName(defaultChapterNameFormat, i + 1) });
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
								chapterName = CreateDefaultChapterName(defaultChapterNameFormat, i + 1);
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

		
		private static string CreateDefaultChapterName(string defaultChapterNameFormat, int chapterNumber)
		{
			return string.Format(
				CultureInfo.CurrentCulture,
				defaultChapterNameFormat,
				chapterNumber);
		}

		private static Filters CreateFilters(VCProfile profile, SourceTitle title, Geometry anamorphicSize)
		{
			Filters filters = new Filters
			{
				FilterList = new List<Filter>(),
				Grayscale = profile.Grayscale
			};

			// Detelecine
			if (profile.Detelecine != VCDetelecine.Off)
			{
				string settings = profile.Detelecine == VCDetelecine.Custom ? profile.CustomDetelecine : null;

				Filter filterItem = new Filter { ID = (int)hb_filter_ids.HB_FILTER_DETELECINE, Settings = settings };
				filters.FilterList.Add(filterItem);
			}

			// Decomb
			if (profile.Decomb != VCDecomb.Off)
			{
				string options;
				if (profile.Decomb == VCDecomb.Fast)
				{
					options = "7:2:6:9:1:80";
				}
				else if (profile.Decomb == VCDecomb.Bob)
				{
					options = "455";
				}
				else
				{
					options = profile.CustomDecomb;
				}

				Filter filterItem = new Filter { ID = (int)hb_filter_ids.HB_FILTER_DECOMB, Settings = options };
				filters.FilterList.Add(filterItem);
			}

			// Deinterlace
			if (profile.Deinterlace != VCDeinterlace.Off)
			{
				string options;
				if (profile.Deinterlace == VCDeinterlace.Fast)
				{
					options = "0";
				}
				else if (profile.Deinterlace == VCDeinterlace.Slow)
				{
					options = "1";
				}
				else if (profile.Deinterlace == VCDeinterlace.Slower)
				{
					options = "3";
				}
				else if (profile.Deinterlace == VCDeinterlace.Bob)
				{
					options = "15";
				}
				else
				{
					options = profile.CustomDeinterlace;
				}

				Filter filterItem = new Filter { ID = (int)hb_filter_ids.HB_FILTER_DEINTERLACE, Settings = options };
				filters.FilterList.Add(filterItem);
			}

			// VFR / CFR

			// 0 - True VFR, 1 - Constant framerate, 2 - VFR with peak framerate
			string framerateSettings;
			if (profile.Framerate == 0)
			{
				if (profile.ConstantFramerate)
				{
					// CFR with "Same as Source". Use the title rate
					framerateSettings = GetFramerateSettings(1, title.FrameRate.Num, title.FrameRate.Den);
				}
				else
				{
					// Pure VFR "Same as Source"
					framerateSettings = "0";
				}
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

				framerateSettings = GetFramerateSettings(
					framerateMode,
					27000000,
					HandBrakeUnitConversionHelpers.FramerateToVrate(profile.Framerate));
			}

			Filter framerateShaper = new Filter { ID = (int)hb_filter_ids.HB_FILTER_VFR, Settings = framerateSettings };
			filters.FilterList.Add(framerateShaper);

			// Deblock
			if (profile.Deblock >= 5)
			{
				Filter filterItem = new Filter { ID = (int)hb_filter_ids.HB_FILTER_DEBLOCK, Settings = profile.Deblock.ToString(CultureInfo.InvariantCulture) };
				filters.FilterList.Add(filterItem);
			}

			// Denoise
			if (profile.DenoiseType != VCDenoise.Off)
			{
				hb_filter_ids id = profile.DenoiseType == VCDenoise.hqdn3d
					? hb_filter_ids.HB_FILTER_HQDN3D
					: hb_filter_ids.HB_FILTER_NLMEANS;

				string settings;
				if (profile.UseCustomDenoise)
				{
					settings = profile.CustomDenoise;
				}
				else
				{
					IntPtr settingsPtr = HBFunctions.hb_generate_filter_settings(
					(int)id,
					profile.DenoisePreset,
					profile.DenoiseTune);
					settings = Marshal.PtrToStringAnsi(settingsPtr);
				}

				Filter filterItem = new Filter { ID = (int)id, Settings = settings };
				filters.FilterList.Add(filterItem);
			}

			// CropScale Filter
			VCCropping cropping = GetCropping(profile, title);
			Filter cropScale = new Filter
			{
				ID = (int)hb_filter_ids.HB_FILTER_CROP_SCALE,
				Settings =
					string.Format(
						"{0}:{1}:{2}:{3}:{4}:{5}",
						anamorphicSize.Width,
						anamorphicSize.Height,
						cropping.Top,
						cropping.Bottom,
						cropping.Left,
						cropping.Right)
			};
			filters.FilterList.Add(cropScale);

			// Rotate
			if (profile.FlipHorizontal || profile.FlipVertical || profile.Rotation != VCPictureRotation.None)
			{
				bool rotate90 = false;
				bool flipHorizontal = profile.FlipHorizontal;
				bool flipVertical = profile.FlipVertical;

				switch (profile.Rotation)
				{
					case VCPictureRotation.Clockwise90:
						rotate90 = true;
						break;
					case VCPictureRotation.Clockwise180:
						flipHorizontal = !flipHorizontal;
						flipVertical = !flipVertical;
						break;
					case VCPictureRotation.Clockwise270:
						rotate90 = true;
						flipHorizontal = !flipHorizontal;
						flipVertical = !flipVertical;
						break;
				}

				int rotateSetting = 0;
				if (flipVertical)
				{
					rotateSetting |= 1;
				}

				if (flipHorizontal)
				{
					rotateSetting |= 2;
				}

				if (rotate90)
				{
					rotateSetting |= 4;
				}

				Filter rotateFilter = new Filter
				{
					ID = (int)hb_filter_ids.HB_FILTER_ROTATE,
					Settings = rotateSetting.ToString(CultureInfo.InvariantCulture)
				};
				filters.FilterList.Add(rotateFilter);
			}

			return filters;
		}

		private static string GetFramerateSettings(int framerateMode, int framerateNumerator, int framerateDenominator)
		{
			return string.Format(
				CultureInfo.InvariantCulture,
				"{0}:{1}:{2}",
				framerateMode,
				framerateNumerator,
				framerateDenominator);
		}

		private static Metadata CreateMetadata()
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
		private static Source CreateSource(VCJob job, SourceTitle title, int previewNumber, int previewSeconds, int previewCount)
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

		private static Subtitles CreateSubtitles(VCJob job)
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
					subtitles.Search.Forced = sourceSubtitle.Forced;
				}
				else
				{
					SubtitleTrack track = new SubtitleTrack
					{
						Burn = sourceSubtitle.BurnedIn,
						Default = sourceSubtitle.Default,
						Forced = sourceSubtitle.Forced,
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

		private static Video CreateVideo(VCJob job, SourceTitle title, int previewLengthSeconds, bool dxvaDecoding)
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
					video.Bitrate = CalculateBitrate(job, title, profile.TargetSize, previewLengthSeconds);
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
		/// <param name="sizeMB">The target size in MB.</param>
		/// <param name="overallSelectedLengthSeconds">The currently selected encode length. Used in preview
		/// for calculating bitrate when the target size would be wrong.</param>
		/// <returns>The video bitrate in kbps.</returns>
		public static int CalculateBitrate(VCJob job, SourceTitle title, int sizeMB, double overallSelectedLengthSeconds = 0)
		{
			long availableBytes = ((long)sizeMB) * 1024 * 1024;

			VCProfile profile = job.EncodingProfile;

			double lengthSeconds = overallSelectedLengthSeconds > 0 ? overallSelectedLengthSeconds : GetJobLength(job, title).TotalSeconds;
			lengthSeconds += 1.5;

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

			long frames = (long)(lengthSeconds * outputFramerate);

			availableBytes -= frames * ContainerOverheadBytesPerFrame;

			List<Tuple<AudioEncoding, int>> outputTrackList = GetOutputTracks(job, title);
			availableBytes -= GetAudioSize(job, lengthSeconds, title, outputTrackList);

			if (availableBytes < 0)
			{
				return 0;
			}

			// Video bitrate is in kilobits per second, or where 1 kbps is 1000 bits per second.
			// So 1 kbps is 125 bytes per second.
			return (int)(availableBytes / (125 * lengthSeconds));
		}

		/// <summary>
		/// Gives estimated file size (in MB) of the given job and video bitrate.
		/// </summary>
		/// <param name="job">The encode job.</param>
		/// <param name="title">The title being encoded.</param>
		/// <param name="videoBitrate">The video bitrate to be used (kbps).</param>
		/// <returns>The estimated file size (in MB) of the given job and video bitrate.</returns>
		public static double CalculateFileSize(VCJob job, SourceTitle title, int videoBitrate)
		{
			long totalBytes = 0;

			VCProfile profile = job.EncodingProfile;

			double lengthSeconds = GetJobLength(job, title).TotalSeconds;
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

			List<Tuple<AudioEncoding, int>> outputTrackList = GetOutputTracks(job, title);
			totalBytes += GetAudioSize(job, lengthSeconds, title, outputTrackList);

			return (double)totalBytes / 1024 / 1024;
		}

		/// <summary>
		/// Gets the total number of seconds on the given encode job.
		/// </summary>
		/// <param name="job">The encode job to query.</param>
		/// <param name="title">The title being encoded.</param>
		/// <returns>The total number of seconds of video to encode.</returns>
		private static TimeSpan GetJobLength(VCJob job, SourceTitle title)
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
		/// <param name="job">The encode job.</param>
		/// <param name="lengthSeconds">The length of the encode in seconds.</param>
		/// <param name="title">The title to encode.</param>
		/// <param name="outputTrackList">The list of tracks to encode.</param>
		/// <returns>The size in bytes for the audio with the given parameters.</returns>
		private static long GetAudioSize(VCJob job, double lengthSeconds, SourceTitle title, List<Tuple<AudioEncoding, int>> outputTrackList)
		{
			long audioBytes = 0;

			foreach (Tuple<AudioEncoding, int> outputTrack in outputTrackList)
			{
				AudioEncoding encoding = outputTrack.Item1;
				SourceAudioTrack track = title.AudioList[outputTrack.Item2 - 1];

				int samplesPerFrame = GetAudioSamplesPerFrame(encoding.Encoder);
				int audioBitrate;

				HBAudioEncoder audioEncoder = HandBrakeEncoderHelpers.GetAudioEncoder(encoding.Encoder);

				if (audioEncoder.IsPassthrough)
				{
					// Input bitrate is in bits/second.
					audioBitrate = track.BitRate / 8;
				}
				else if (encoding.EncodeRateType == AudioEncodeRateType.Quality)
				{
					// Can't predict size of quality targeted audio encoding.
					audioBitrate = 0;
				}
				else
				{
					int outputBitrate;
					if (encoding.Bitrate > 0)
					{
						outputBitrate = encoding.Bitrate;
					}
					else
					{
						outputBitrate = HandBrakeEncoderHelpers.GetDefaultBitrate(
							audioEncoder,
							encoding.SampleRateRaw == 0 ? track.SampleRate : encoding.SampleRateRaw,
							HandBrakeEncoderHelpers.SanitizeMixdown(HandBrakeEncoderHelpers.GetMixdown(encoding.Mixdown), audioEncoder, (ulong)track.ChannelLayout));
					}

					// Output bitrate is in kbps.
					audioBitrate = outputBitrate * 1000 / 8;
				}

				audioBytes += (long)(lengthSeconds * audioBitrate);

				// Audio overhead
				audioBytes += encoding.SampleRateRaw * ContainerOverheadBytesPerFrame / samplesPerFrame;
			}

			return audioBytes;
		}

		/// <summary>
		/// Gets the number of audio samples used per frame for the given audio encoder.
		/// </summary>
		/// <param name="encoderName">The encoder to query.</param>
		/// <returns>The number of audio samples used per frame for the given
		/// audio encoder.</returns>
		private static int GetAudioSamplesPerFrame(string encoderName)
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
			int modulus;
			if (profile.Anamorphic == VCAnamorphic.Loose || profile.Anamorphic == VCAnamorphic.Custom)
			{
				modulus = profile.Modulus;
			}
			else
			{
				modulus = 2;
			}

			int keepValue = profile.KeepDisplayAspect ? 0x04 : 0;
			VCCropping cropping = GetCropping(profile, title);
			List<int> croppingList = new List<int> { cropping.Top, cropping.Bottom, cropping.Left, cropping.Right };

			// HB doesn't expect us to give it a 0 width and height so we need to guard against that
			int sanitizedWidth = profile.Width > 0 ? profile.Width : title.Geometry.Width - cropping.Left - cropping.Right;
			int sanitizedHeight = profile.Height > 0 ? profile.Height : title.Geometry.Height - cropping.Top - cropping.Bottom;

			AnamorphicGeometry anamorphicGeometry = new AnamorphicGeometry
			{
				SourceGeometry = title.Geometry,
				DestSettings = new DestSettings
				{
					AnamorphicMode = (int)profile.Anamorphic,
					Geometry = new Geometry
					{
						Width = sanitizedWidth,
						Height = sanitizedHeight,
						PAR = new PAR
						{
							Num = profile.Anamorphic == VCAnamorphic.Custom ? profile.PixelAspectX : title.Geometry.PAR.Num,
							Den = profile.Anamorphic == VCAnamorphic.Custom ? profile.PixelAspectY : title.Geometry.PAR.Den
						},
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
