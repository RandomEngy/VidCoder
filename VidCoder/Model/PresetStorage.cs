using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Markup.Localizer;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using HandBrake.ApplicationServices.Interop;
using HandBrake.ApplicationServices.Interop.HbLib;
using Newtonsoft.Json;
using Omu.ValueInjecter;
using VidCoder.Extensions;
using VidCoder.Resources;
using VidCoder.Services;
using VidCoderCommon.Model;
using VidCoderCommon.Utilities.Injection;

namespace VidCoder.Model
{
	public static class PresetStorage
	{
		private const int CurrentPresetVersion = 20;

		private static readonly string UserPresetsFolder = Path.Combine(Utilities.AppFolder, "UserPresets");
		private static readonly string BuiltInPresetsPath = "BuiltInPresets.json";
		private static object userPresetSync = new object();

		// This field holds the copy of the preset list that has been most recently queued for saving.
		private static volatile List<Preset> pendingSavePresetList;

		public static IList<Preset> BuiltInPresets
		{
			get
			{
				string builtInPresetsPath = Path.Combine(Utilities.ProgramFolder, BuiltInPresetsPath);
				return JsonConvert.DeserializeObject<IList<Preset>>(File.ReadAllText(builtInPresetsPath));
			}
		}

		public static List<Preset> UserPresets
		{
			get
			{
				lock (userPresetSync)
				{
					var result = GetJsonPresetListFromDb();

					ErrorCheckPresets(result);

					return result;
				}
			}

			set
			{
				var clonedList = value.Select(p =>
				{
					Preset newPreset = new Preset();
					newPreset.InjectFrom<FastDeepCloneInjection>(p);

					return newPreset;
				}).ToList();

				pendingSavePresetList = clonedList;

				// Do the actual save asynchronously.
				ThreadPool.QueueUserWorkItem(SaveUserPresetsBackground, clonedList);
			}
		}

		/// <summary>
		/// Serializes a preset to JSON.
		/// </summary>
		/// <param name="preset">The preset to serialize.</param>
		/// <returns>The serialized JSON of the preset.</returns>
		public static string SerializePreset(Preset preset)
		{
			return JsonConvert.SerializeObject(preset);
		}

		/// <summary>
		/// Saves a user preset to a file.
		/// </summary>
		/// <param name="preset">The preset to save.</param>
		/// <param name="filePath">The path to save the preset to.</param>
		/// <returns>True if the save succeeded.</returns>
		public static bool SavePresetToFile(Preset preset, string filePath)
		{
			try
			{
				var presetWrapper = new PresetWrapper { Version = CurrentPresetVersion, Preset = preset };

				File.WriteAllText(filePath, JsonConvert.SerializeObject(presetWrapper));

				return true;
			}
			catch (Exception exception)
			{
				System.Windows.MessageBox.Show(string.Format(MainRes.CouldNotSavePresetMessage, preset.Name, Environment.NewLine, exception));
			}

			return false;
		}

		/// <summary>
		/// Loads in a preset from a file.
		/// </summary>
		/// <param name="presetFile">The file to load the preset from.</param>
		/// <returns>The parsed preset from the file, or null if the preset is invalid.</returns>
		public static Preset LoadPresetFile(string presetFile)
		{
			string extension = Path.GetExtension(presetFile);
			if (extension != null)
			{
				extension = extension.ToLowerInvariant();
			}

			if (extension != ".xml" && extension != ".vjpreset")
			{
				throw new ArgumentException("File extension " + extension + " is not recognized.");
			}

			if (!File.Exists(presetFile))
			{
				throw new ArgumentException("Preset file could not be found.");
			}

			if (extension == ".xml")
			{
				XDocument doc = XDocument.Load(presetFile);
				if (doc.Element("UserPreset") == null)
				{
					throw new ArgumentException("Preset file is malformed.");
				}

				XElement presetElement = doc.Element("UserPreset").Element("Preset");
				int version = int.Parse(doc.Element("UserPreset").Attribute("Version").Value, CultureInfo.InvariantCulture);

				using (XmlReader reader = presetElement.CreateReader())
				{
					XmlSerializer presetSerializer = new XmlSerializer(typeof(Preset));
					var preset = presetSerializer.Deserialize(reader) as Preset;
					UpgradeEncodingProfile(preset.EncodingProfile, PresetToDbVersion(version));

					return preset;
				}
			}
			else
			{
				PresetWrapper presetWrapper = JsonConvert.DeserializeObject<PresetWrapper>(File.ReadAllText(presetFile));
				UpgradeEncodingProfile(presetWrapper.Preset.EncodingProfile, PresetToDbVersion(presetWrapper.Version));

				return presetWrapper.Preset;
			}
		}

		/// <summary>
		/// Load in a preset from an XML string.
		/// </summary>
		/// <param name="presetXml">The XML of the preset to load in (without wrapper XML).</param>
		/// <param name="serializer">The XML serializer to use.</param>
		/// <returns>The loaded Preset.</returns>
		public static Preset ParsePresetXml(string presetXml, XmlSerializer serializer)
		{
			try
			{
				using (var stringReader = new StringReader(presetXml))
				{
					using (var xmlReader = new XmlTextReader(stringReader))
					{
						var preset = serializer.Deserialize(xmlReader) as Preset;

						return preset;
					}
				}
			}
			catch (XmlException exception)
			{
				System.Windows.MessageBox.Show(
					MainRes.CouldNotLoadPresetMessage +
					exception +
					Environment.NewLine +
					Environment.NewLine +
					presetXml);
			}

			return null;
		}

		/// <summary>
		/// Parse a preset from an JSON string.
		/// </summary>
		/// <param name="presetJson">The JSON of the preset to load in.</param>
		/// <returns>The parsed Preset.</returns>
		public static Preset ParsePresetJson(string presetJson)
		{
			try
			{
				return JsonConvert.DeserializeObject<Preset>(presetJson);
			}
			catch (Exception exception)
			{
				System.Windows.MessageBox.Show(
					MainRes.CouldNotLoadPresetMessage +
					exception +
					Environment.NewLine +
					Environment.NewLine +
					presetJson);
			}

			return null;
		}

		public static void UpgradeEncodingProfile(VCProfile profile, int oldDatabaseVersion, int? targetDatabaseVersion = null)
		{
			if (oldDatabaseVersion >= Utilities.CurrentDatabaseVersion)
			{
				return;
			}

			List<EncodingProfileUpgrade> upgrades = new List<EncodingProfileUpgrade>
			{
				new EncodingProfileUpgrade(13, UpgradeEncodingProfileTo13),
				new EncodingProfileUpgrade(14, UpgradeEncodingProfileTo14),
				new EncodingProfileUpgrade(15, UpgradeEncodingProfileTo15),
				new EncodingProfileUpgrade(16, UpgradeEncodingProfileTo16),
				new EncodingProfileUpgrade(17, UpgradeEncodingProfileTo17),
				new EncodingProfileUpgrade(19, UpgradeEncodingProfileTo19),
				new EncodingProfileUpgrade(20, UpgradeEncodingProfileTo20),
				new EncodingProfileUpgrade(21, UpgradeEncodingProfileTo21),
				new EncodingProfileUpgrade(22, UpgradeEncodingProfileTo22),
				new EncodingProfileUpgrade(23, UpgradeEncodingProfileTo23),
				new EncodingProfileUpgrade(24, UpgradeEncodingProfileTo24),
				new EncodingProfileUpgrade(25, UpgradeEncodingProfileTo25),
				new EncodingProfileUpgrade(26, UpgradeEncodingProfileTo26),
				new EncodingProfileUpgrade(29, UpgradeEncodingProfileTo29),
				new EncodingProfileUpgrade(31, UpgradeEncodingProfileTo31),
				new EncodingProfileUpgrade(32, UpgradeEncodingProfileTo32),
				new EncodingProfileUpgrade(33, UpgradeEncodingProfileTo33),
			};

			foreach (EncodingProfileUpgrade upgrade in upgrades)
			{
				if (targetDatabaseVersion.HasValue && upgrade.TargetDatabaseVersion > targetDatabaseVersion.Value)
				{
					break;
				}

				if (oldDatabaseVersion < upgrade.TargetDatabaseVersion)
				{
					upgrade.UpgradeAction(profile);
				}
			}
		}

		public static void UpgradeEncodingProfileTo13(VCProfile profile)
		{
			// Upgrade preset: translate old Enum-based values to short name strings
			if (!HandBrakeEncoderHelpers.VideoEncoders.Any(e => e.ShortName == profile.VideoEncoder))
			{
				string newVideoEncoder = "x264";
				switch (profile.VideoEncoder)
				{
					case "X264":
						newVideoEncoder = "x264";
						break;
					case "FFMpeg":
						newVideoEncoder = "ffmpeg4";
						break;
					case "FFMpeg2":
						newVideoEncoder = "ffmpeg2";
						break;
					case "Theora":
						newVideoEncoder = "theora";
						break;
				}

				profile.VideoEncoder = newVideoEncoder;
			}

			foreach (AudioEncoding encoding in profile.AudioEncodings)
			{
				if (!HandBrakeEncoderHelpers.AudioEncoders.Any(e => e.ShortName == encoding.Encoder))
				{
					string newAudioEncoder = UpgradeAudioEncoder(encoding.Encoder);
					if (newAudioEncoder == null)
					{
						newAudioEncoder = "faac";
					}

					encoding.Encoder = newAudioEncoder;
				}

				if (!HandBrakeEncoderHelpers.Mixdowns.Any(m => m.ShortName == encoding.Mixdown))
				{
					string newMixdown = "dpl2";
					switch (encoding.Mixdown)
					{
						case "Auto":
							newMixdown = "none";
							break;
						case "None":
							newMixdown = "none";
							break;
						case "Mono":
							newMixdown = "mono";
							break;
						case "Stereo":
							newMixdown = "stereo";
							break;
						case "DolbySurround":
							newMixdown = "dpl1";
							break;
						case "DolbyProLogicII":
							newMixdown = "dpl2";
							break;
						case "SixChannelDiscrete":
							newMixdown = "6ch";
							break;
					}

					encoding.Mixdown = newMixdown;
				}
			}
		}

		private static void UpgradeEncodingProfileTo14(VCProfile profile)
		{
			profile.AudioEncoderFallback = UpgradeAudioEncoder(profile.AudioEncoderFallback);
		}

		private static void UpgradeEncodingProfileTo15(VCProfile profile)
		{
			if (profile.Framerate == 0)
			{
				// Profile had only VFR for Same as Source framerate before.
				profile.ConstantFramerate = false;
			}
			else
			{
				// If Peak Framerate was checked under a specific framerate
				// that meant we wanted VFR. Otherwise, CFR with the listed framerate
#pragma warning disable 612,618
				profile.ConstantFramerate = !profile.PeakFramerate;
#pragma warning restore 612,618
			}

			foreach (AudioEncoding encoding in profile.AudioEncodings)
			{
				if (encoding.Mixdown == "6ch")
				{
					encoding.Mixdown = "5point1";
				}
			}
		}

		private static void UpgradeEncodingProfileTo16(VCProfile profile)
		{
#pragma warning disable 612,618
			if (profile.CustomCropping)
#pragma warning restore 612,618
			{
				profile.CroppingType = VCCroppingType.Custom;
			}
			else
			{
                profile.CroppingType = VCCroppingType.Automatic;
			}

#pragma warning disable 612,618
			if (string.IsNullOrWhiteSpace(profile.X264Tune))
			{
				profile.X264Tunes = new List<string> { profile.X264Tune };
			}
#pragma warning restore 612,618
		}

		private static void UpgradeEncodingProfileTo17(VCProfile profile)
		{
			if (profile.X264Profile == "high444")
			{
				profile.X264Profile = null;
			}
		}

		private static void UpgradeEncodingProfileTo19(VCProfile profile)
		{
			foreach (AudioEncoding encoding in profile.AudioEncodings)
			{
				if (!HandBrakeEncoderHelpers.AudioEncoders.Any(e => e.ShortName == encoding.Encoder))
				{
					string newAudioEncoder = UpgradeAudioEncoder2(encoding.Encoder);
					if (newAudioEncoder == null)
					{
						newAudioEncoder = "av_aac";
					}

					encoding.Encoder = newAudioEncoder;
				}
			}
		}

		private static void UpgradeEncodingProfileTo20(VCProfile profile)
		{
			if (!HandBrakeEncoderHelpers.VideoEncoders.Any(e => e.ShortName == profile.VideoEncoder))
			{
				string newVideoEncoder = UpgradeVideoEncoder(profile.VideoEncoder);
				if (newVideoEncoder == null)
				{
					newVideoEncoder = "x264";
				}

				profile.VideoEncoder = newVideoEncoder;
			}
		}

		private static void UpgradeEncodingProfileTo21(VCProfile profile)
		{
#pragma warning disable 612,618
			if (profile.OutputFormat == VCContainer.Mp4)
			{
				profile.ContainerName = "av_mp4";
			}
			else
			{
				profile.ContainerName = "av_mkv";
			}
#pragma warning restore 612,618
		}

		private static void UpgradeEncodingProfileTo22(VCProfile profile)
		{
			profile.QsvDecode = true;
		}

		private static void UpgradeEncodingProfileTo23(VCProfile profile)
		{
			if (profile.ContainerName == "mp4v2")
			{
				profile.ContainerName = "av_mp4";
			}
			else if (profile.ContainerName == "libmkv")
			{
				profile.ContainerName = "av_mkv";
			}
		}

		private static void UpgradeEncodingProfileTo24(VCProfile profile)
		{
			profile.VideoOptions = profile.X264Options;
			profile.VideoTunes = profile.X264Tunes;
			profile.VideoPreset = profile.X264Preset;
			profile.VideoProfile = profile.X264Profile;
			profile.VideoLevel = profile.H264Level;

			// If QSV was the old encoder and QSV is available, use the QSV preset.
			string videoEncoderName = profile.VideoEncoder;
			if (HandBrakeEncoderHelpers.GetVideoEncoder(videoEncoderName) == null)
			{
				if (videoEncoderName == "qsv_h264")
				{
					profile.VideoPreset = profile.QsvPreset;
				}
			}
		}

		private static void UpgradeEncodingProfileTo25(VCProfile profile)
		{
#pragma warning disable 618
			if (profile.Denoise == null || profile.Denoise == "Off")
			{
				profile.DenoiseType = VCDenoise.Off;
			}
			else
			{
				profile.DenoiseType = VCDenoise.hqdn3d;
			}

			switch (profile.Denoise)
			{
				case "Off":
					break;
				case "Weak":
					profile.DenoisePreset = "light";
					break;
				case "Medium":
					profile.DenoisePreset = "medium";
					break;
				case "Strong":
					profile.DenoisePreset = "strong";
					break;
				case "Custom":
					profile.DenoisePreset = null;
					profile.UseCustomDenoise = true;
					break;
				default:
					profile.DenoisePreset = "medium";
					break;
			}
#pragma warning restore 618
		}

		private static void UpgradeEncodingProfileTo26(VCProfile profile)
	    {
			// Suppress this for now, JSON serializer doesn't know how to handle two enum values with different caps

			//if (profile.DenoiseType == VCDenoise.NlMeans)
			//{
			//    profile.DenoiseType = VCDenoise.NLMeans;
			//}
        }

		private static void UpgradeEncodingProfileTo29(VCProfile profile)
		{
			// Earlier versions had an issue where they might wipe the container name.
			if (profile.ContainerName == null)
			{
				profile.ContainerName = "av_mp4";
			}
		}

		private static void UpgradeEncodingProfileTo31(VCProfile profile)
		{
			if (profile.AudioEncodings != null)
			{
				foreach (var audioEncoding in profile.AudioEncodings)
				{
					if (audioEncoding.Encoder == "fdk_aac" || audioEncoding.Encoder == "fdk_haac")
					{
						audioEncoding.Encoder = "av_aac";
					}
				}
			}

			if (profile.AudioEncoderFallback == "fdk_aac" || profile.AudioEncoderFallback == "fdk_haac")
			{
				profile.AudioEncoderFallback = "av_aac";
			}
		}

		private static void UpgradeEncodingProfileTo32(VCProfile profile)
		{
			if (profile.Detelecine != null)
			{
				profile.Detelecine = profile.Detelecine.ToLowerInvariant();
			}

			if (!string.IsNullOrEmpty(profile.CustomDetelecine))
			{
				profile.CustomDetelecine = UpgradeCustomFilter(profile.CustomDetelecine, hb_filter_ids.HB_FILTER_DETELECINE);
			}

			if (!string.IsNullOrEmpty(profile.Decomb) && profile.Decomb != "Off")
			{
				profile.DeinterlaceType = VCDeinterlace.Decomb;

				switch (profile.Decomb)
				{
					case "Fast":
						profile.DeinterlacePreset = "default";
						profile.CombDetect = "fast";
						break;
					case "Bob":
					case "Default":
						profile.DeinterlacePreset = profile.Decomb.ToLowerInvariant();
						profile.CombDetect = "default";
						break;
					case "Custom":
						List<string> filterValues = GetFilterValuesFromOldCustomFilter(profile.CustomDecomb ?? string.Empty);
						List<string> parameterNames = GetFilterParameterNames(hb_filter_ids.HB_FILTER_DECOMB);
						Dictionary<string, string> filterParameterDictionary = CreateFilterParameterDictionary(filterValues, parameterNames);

						int mode = ExtractInt(filterParameterDictionary, "mode", 7);
						int spatialMetric = ExtractInt(filterParameterDictionary, "spatial-metric", 2);
						int motionThreshold = ExtractInt(filterParameterDictionary, "motion-thresh", 3);
						int spatialThreshold = ExtractInt(filterParameterDictionary, "spatial-thresh", 3);
						int filterMode = ExtractInt(filterParameterDictionary, "filter-mode", 2);
						int blockThreshold = ExtractInt(filterParameterDictionary, "block-thresh", 40);
						int blockWidth = ExtractInt(filterParameterDictionary, "block-width", 16);
						int blockHeight = ExtractInt(filterParameterDictionary, "block-height", 16);
						int magnitudeThreshold = ExtractInt(filterParameterDictionary, "magnitude-thresh", 10);
						int varianceThreshold = ExtractInt(filterParameterDictionary, "variance-thresh", 20);
						int laplacianThreshold = ExtractInt(filterParameterDictionary, "laplacian-thresh", 20);
						int dilationThreshold = ExtractInt(filterParameterDictionary, "dilation-thresh", 4);
						int erosionThreshold = ExtractInt(filterParameterDictionary, "erosion-thresh", 2);
						int noiseThreshold = ExtractInt(filterParameterDictionary, "noise-thresh", 50);
						int maximumSearchDistance = ExtractInt(filterParameterDictionary, "search-distance", 24);
						int postProcessing = ExtractInt(filterParameterDictionary, "postproc", 1);
						int parity = ExtractInt(filterParameterDictionary, "parity", -1);

						int yadif = (mode & 1) == 0 ? 0 : 1;
						int blend = (mode & 2) == 0 ? 0 : 1;
						int cubic = (mode & 4) == 0 ? 0 : 1;
						int eedi2 = (mode & 8) == 0 ? 0 : 1;
						int mask = (mode & 32) == 0 ? 0 : 1;
						int bob = (mode & 64) == 0 ? 0 : 1;
						int gamma = (mode & 128) == 0 ? 0 : 1;
						int filter = (mode & 256) == 0 ? 0 : 1;
						int composite = (mode & 512) == 0 ? 0 : 1;

						int detectMode = gamma + filter * 2 + mask * 4 + composite * 8;
						int decombMode = yadif + blend * 2 + cubic * 4 + eedi2 * 8 + bob * 16;

						string combDetectCustom =
							FormattableString.Invariant($"mode={detectMode}:spatial-metric={spatialMetric}:motion-thresh={motionThreshold}:spatial-thresh={spatialThreshold}:") +
							FormattableString.Invariant($"filter-mode={filterMode}:block-thresh={blockThreshold}:block-width={blockWidth}:block-height={blockHeight}");
						profile.CombDetect = "custom";
						profile.CustomCombDetect = combDetectCustom;

						string decombCustom =
							FormattableString.Invariant($"mode={decombMode}:magnitude-thresh={magnitudeThreshold}:variance-thresh={varianceThreshold}:laplacian-thresh={laplacianThreshold}:dilation-thresh={dilationThreshold}:") +
							FormattableString.Invariant($"erosion-thresh={erosionThreshold}:noise-thresh={noiseThreshold}:search-distance={maximumSearchDistance}:postproc={postProcessing}");
						if (parity >= 0)
						{
							decombCustom += ":parity" + parity;
						}

						profile.DeinterlacePreset = "custom";
						profile.CustomDeinterlace = decombCustom;

						break;
					default:
						break;
				}
			}
			else if (!string.IsNullOrEmpty(profile.Deinterlace) && profile.Deinterlace != "Off")
			{
				profile.DeinterlaceType = VCDeinterlace.Yadif;

				switch (profile.Deinterlace)
				{
					case "Fast":
					case "Slow":
						profile.DeinterlacePreset = "skip-spatial";
						break;
					case "Bob":
						profile.DeinterlacePreset = "bob";
						break;
					case "Custom":
						profile.DeinterlacePreset = "custom";
						break;
					default:
						profile.DeinterlacePreset = "default";
						break;
				}

				if (!string.IsNullOrEmpty(profile.CustomDeinterlace))
				{
					profile.CustomDeinterlace = UpgradeCustomFilter(profile.CustomDeinterlace, hb_filter_ids.HB_FILTER_DEINTERLACE);
				}

				profile.CombDetect = "off";
			}

			if (profile.UseCustomDenoise)
			{
				profile.DenoisePreset = "custom";

				if (profile.DenoiseType != VCDenoise.Off && !string.IsNullOrEmpty(profile.CustomDenoise))
				{
					hb_filter_ids denoiseFilterid = profile.DenoiseType == VCDenoise.hqdn3d ? hb_filter_ids.HB_FILTER_HQDN3D : hb_filter_ids.HB_FILTER_NLMEANS;
					profile.CustomDenoise = UpgradeCustomFilter(profile.CustomDenoise, denoiseFilterid);
				}
				else
				{
					profile.CustomDenoise = string.Empty;
				}
			}
			else
			{
				profile.CustomDenoise = string.Empty;
			}
		}

		private static void UpgradeEncodingProfileTo33(VCProfile profile)
		{
			profile.PaddingMode = VCPaddingMode.None;

			if (profile.Width > 0 && profile.Height > 0 && !profile.KeepDisplayAspect)
			{
				profile.SizingMode = VCSizingMode.Manual;
				if (profile.Anamorphic != VCAnamorphic.Custom || profile.UseDisplayWidth)
				{
					profile.PixelAspectX = 1;
					profile.PixelAspectY = 1;
				}
			}
			else if (profile.Width > 0 && profile.Height > 0)
			{
				profile.SizingMode = VCSizingMode.Automatic;
				profile.ScalingMode = VCScalingMode.UpscaleFill;
			}
			else
			{
				profile.SizingMode = VCSizingMode.Automatic;
				profile.ScalingMode = VCScalingMode.DownscaleOnly;

				profile.Width = profile.MaxWidth;
				profile.Height = profile.MaxHeight;
			}

			profile.UseAnamorphic = profile.Anamorphic != VCAnamorphic.None;
		}

#region For v31 upgrade
		private static string UpgradeCustomFilter(string custom, hb_filter_ids filterId)
		{
			List<string> filterValues = GetFilterValuesFromOldCustomFilter(custom);
			List<string> parameterNames = GetFilterParameterNames(filterId);

			Dictionary<string, string> filterParameterDictionary = CreateFilterParameterDictionary(filterValues, parameterNames);
			return CreateCustomFilterStringFromDictionary(filterParameterDictionary);
		}

		private static List<string> GetFilterValuesFromOldCustomFilter(string oldCustomFilter)
		{
			return oldCustomFilter.Split(':').ToList();
		}

		private static List<string> GetFilterParameterNames(hb_filter_ids filterId)
		{
			switch (filterId)
			{
				case hb_filter_ids.HB_FILTER_DETELECINE:
					return new List<string> { "skip-left", "skip-right", "skip-top", "skip-bottom", "strict-breaks", "plane", "parity", "disable" };
				case hb_filter_ids.HB_FILTER_DEINTERLACE:
					return new List<string> { "mode", "parity" };
				case hb_filter_ids.HB_FILTER_DECOMB:
					return new List<string> { "mode", "spatial-metric", "motion-thresh", "spatial-thresh", "block-threshold", "block-width", "block-height", "magnitude-thresh", "variance-thresh", "laplacian-thresh", "dilation-thresh", "erosion-thresh", "noise-thresh", "search-distance", "postproc", "parity" };
				case hb_filter_ids.HB_FILTER_HQDN3D:
					return new List<string> { "y-spatial", "cb-spatial", "cr-spatial", "y-temporal", "cb-temporal", "cr-temporal" };
				case hb_filter_ids.HB_FILTER_NLMEANS:
					return new List<string> { "y-strength", "y-origin-tune", "y-patch-size", "y-range", "y-frame-count", "y-prefilter", "cb-strength", "cb-origin-tune", "cb-patch-size", "cb-range", "cb-frame-count", "cb-prefilter", "cr-strength", "cr-origin-tune", "cr-patch-size", "cr-range", "cr-frame-count", "cr-prefilter" };
				default:
					throw new ArgumentException("Filter id not recognized: " + filterId);
			}
		}

		private static Dictionary<string, string> CreateFilterParameterDictionary(List<string> filterValues, List<string> parameterNames)
		{
			var result = new Dictionary<string, string>();
			for (int i = 0; i < filterValues.Count; i++)
			{
				if (i < parameterNames.Count)
				{
					result.Add(parameterNames[i], filterValues[i]);
				}
			}

			if (result.ContainsKey("parity") && result["parity"] == "-1")
			{
				result.Remove("parity");
			}

			return result;
		}

		private static string CreateCustomFilterStringFromDictionary(IDictionary<string, string> dictionary)
		{
			return string.Join(":", dictionary.Select(pair => FormattableString.Invariant($"{pair.Key}={pair.Value}")));
		}

		private static int ExtractInt(Dictionary<string, string> dictionary, string parameterName, int defaultValue)
		{
			string valueString;
			if (dictionary.TryGetValue(parameterName, out valueString))
			{
				int value;
				if (int.TryParse(valueString, out value))
				{
					return value;
				}
				else
				{
					return defaultValue;
				}
			}
			else
			{
				return defaultValue;
			}
		}
#endregion

		private static void ErrorCheckPresets(List<Preset> presets)
		{
			for (int i = presets.Count - 1; i >= 0; i--)
			{
				var preset = presets[i];

				if (preset.EncodingProfile == null)
				{
					presets.RemoveAt(i);

					// Splash screen eats first dialog, need to show twice.
					Ioc.Get<IMessageBoxService>().Show("Could not load corrupt preset '" + preset.GetDisplayName() + "'.");
					Ioc.Get<IMessageBoxService>().Show("Could not load corrupt preset '" + preset.GetDisplayName() + "'.");
				}
				else
				{
					ErrorCheckPreset(preset);
				}
			}
		}

		private static void ErrorCheckPreset(Preset preset)
		{
			// mp4v2 only available on x86
			string containerName = preset.EncodingProfile.ContainerName;
			if (HandBrakeEncoderHelpers.GetContainer(containerName) == null)
			{
				if (containerName == "mp4v2")
				{
					preset.EncodingProfile.ContainerName = "av_mp4";
				}
			}

			// QSV H.264 only available on systems with the right hardware.
			string videoEncoderName = preset.EncodingProfile.VideoEncoder;
			if (HandBrakeEncoderHelpers.GetVideoEncoder(videoEncoderName) == null)
			{
				if (videoEncoderName == "qsv_h264")
				{
					preset.EncodingProfile.VideoEncoder = "x264";
				}
			}
		}

		// For v13 upgrade
		private static string UpgradeAudioEncoder(string oldEncoder)
		{
			switch (oldEncoder)
			{
				case "Faac":
					return "faac";
				case "Lame":
					return "lame";
				case "Ac3":
					return "ffac3";
				case "Passthrough":
					return "copy";
				case "Ac3Passthrough":
					return "copy:ac3";
				case "DtsPassthrough":
					return "copy:dts";
				case "Vorbis":
					return "vorbis";
			}

			return oldEncoder;
		}

		// For v19 upgrade
		private static string UpgradeAudioEncoder2(string oldEncoder)
		{
			switch (oldEncoder)
			{
				case "faac":
				case "ffaac":
					return "av_aac";
				case "ffflac":
					return "flac16";
				case "ffflac24":
					return "flac24";
				case "ffac3":
					return "ac3";
				case "lame":
					return "mp3";
			}

			return oldEncoder;
		}

		// For v20 upgrade
		private static string UpgradeVideoEncoder(string oldEncoder)
		{
			switch (oldEncoder)
			{
				case "ffmpeg4":
				case "ffmpeg":
					return "mpeg4";
				case "ffmpeg2":
					return "mpeg2";
				case "libtheora":
					return "theora";
			}

			return oldEncoder;
		}

		private static int PresetToDbVersion(int presetVersion)
		{
			if (presetVersion >= CurrentPresetVersion)
			{
				return Utilities.CurrentDatabaseVersion;
			}

			if (presetVersion < 4)
			{
				return 12;
			}

			if (presetVersion < 5)
			{
				return 13;
			}

			if (presetVersion < 6)
			{
				return 14;
			}

			if (presetVersion < 7)
			{
				return 15;
			}

			if (presetVersion < 9)
			{
				return 18;
			}

			if (presetVersion < 10)
			{
				return 19;
			}

			if (presetVersion < 11)
			{
				return 20;
			}

			if (presetVersion < 12)
			{
				return 21;
			}

			if (presetVersion < 13)
			{
				return 22;
			}

			if (presetVersion < 14)
			{
				return 23;
			}

			if (presetVersion < 15)
			{
				return 24;
			}

			if (presetVersion < 16)
			{
				return 25;
			}

			if (presetVersion < 17)
			{
				return 26;
			}

			if (presetVersion < 18)
			{
				return 29;
			}

			if (presetVersion < 19)
			{
				return 31;
			}

			if (presetVersion < 20)
			{
				return 32;
			}

			return Utilities.CurrentDatabaseVersion;
		}

		/// <summary>
		/// Saves the given preset data.
		/// </summary>
		/// <param name="presetJsonListObject">List&lt;Preset&gt; to save.</param>
		private static void SaveUserPresetsBackground(object presetJsonListObject)
		{
			lock (userPresetSync)
			{
				// If the pending save list is different from the one we've been passed, we abort.
				// This means that another background task has been scheduled with a more recent save.
				if (presetJsonListObject != pendingSavePresetList)
				{
					return;
				}

				if (Directory.Exists(UserPresetsFolder))
				{
					string[] existingFiles = Directory.GetFiles(UserPresetsFolder);
					foreach (string existingFile in existingFiles)
					{
						File.Delete(existingFile);
					}

					Directory.Delete(UserPresetsFolder);
				}

				var presetList = (List<Preset>)presetJsonListObject;
				var presetJsonList = presetList.Select(SerializePreset).ToList();

				SQLiteConnection connection = Database.CreateConnection();

				using (SQLiteTransaction transaction = connection.BeginTransaction())
				{
					SavePresets(presetJsonList, connection);
					transaction.Commit();
				}
			}
		}

		public static void SavePresets(List<string> presetJsonList, SQLiteConnection connection)
		{
			Database.ExecuteNonQuery("DELETE FROM presetsJson", connection);

			var insertCommand = new SQLiteCommand("INSERT INTO presetsJson (json) VALUES (?)", connection);
			SQLiteParameter insertJsonParam = insertCommand.Parameters.Add("json", DbType.String);

			foreach (string presetJson in presetJsonList)
			{
				insertJsonParam.Value = presetJson;
				insertCommand.ExecuteNonQuery();
			}
		}

		public static List<Preset> GetJsonPresetListFromDb()
		{
			var result = new List<Preset>();

			var selectPresetsCommand = new SQLiteCommand("SELECT * FROM presetsJson", Database.Connection);
			using (SQLiteDataReader reader = selectPresetsCommand.ExecuteReader())
			{
				while (reader.Read())
				{
					string presetJson = reader.GetString("json");
					result.Add(ParsePresetJson(presetJson));
				}
			}

			return result;
		}

		private class EncodingProfileUpgrade
		{
			public EncodingProfileUpgrade(int targetDatabaseVersion, Action<VCProfile> upgradeAction)
			{
				this.TargetDatabaseVersion = targetDatabaseVersion;
				this.UpgradeAction = upgradeAction;
			}

			public int TargetDatabaseVersion { get; private set; }

			public Action<VCProfile> UpgradeAction { get; private set; }
		}
	}
}
