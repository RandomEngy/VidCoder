using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using HandBrake.ApplicationServices.Interop;
using Newtonsoft.Json;
using VidCoder.Extensions;
using VidCoder.Resources;
using VidCoder.Services;
using VidCoderCommon.Model;

namespace VidCoder.Model
{
	public static class PresetStorage
	{
		private const int CurrentPresetVersion = 16;

		private static readonly string UserPresetsFolder = Path.Combine(Utilities.AppFolder, "UserPresets");
		private static readonly string BuiltInPresetsPath = "BuiltInPresets.json";
		private static object userPresetSync = new object();

		// This field holds the copy of the preset list that has been most recently queued for saving.
		private static volatile List<string> pendingSavePresetList;

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
				var presetJsonList = value.Select(SerializePreset).ToList();

				pendingSavePresetList = presetJsonList;

				// Do the actual save asynchronously.
				ThreadPool.QueueUserWorkItem(SaveUserPresetsBackground, presetJsonList);
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
			string extension = Path.GetExtension(presetFile).ToLowerInvariant();

			if (extension != ".xml" && extension != ".vjpreset")
			{
				return null;
			}

			try
			{
				if (extension == ".xml")
				{
					XDocument doc = XDocument.Load(presetFile);
					if (doc.Element("UserPreset") == null)
					{
						return null;
					}

					XElement presetElement = doc.Element("UserPreset").Element("Preset");
					int version = int.Parse(doc.Element("UserPreset").Attribute("Version").Value);

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
			catch (Exception)
			{
				return null;
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

		public static void UpgradeEncodingProfile(VCProfile profile, int databaseVersion)
		{
			if (databaseVersion >= Utilities.CurrentDatabaseVersion)
			{
				return;
			}

			if (databaseVersion < 13)
			{
				UpgradeEncodingProfileTo13(profile);
			}

			if (databaseVersion < 14)
			{
				UpgradeEncodingProfileTo14(profile);
			}

			if (databaseVersion < 15)
			{
				UpgradeEncodingProfileTo15(profile);
			}

			if (databaseVersion < 16)
			{
				UpgradeEncodingProfileTo16(profile);
			}

			if (databaseVersion < 17)
			{
				UpgradeEncodingProfileTo17(profile);
			}

			if (databaseVersion < 19)
			{
				UpgradeEncodingProfileTo19(profile);
			}

			if (databaseVersion < 20)
			{
				UpgradeEncodingProfileTo20(profile);
			}

			if (databaseVersion < 21)
			{
				UpgradeEncodingProfileTo21(profile);
			}

			if (databaseVersion < 22)
			{
				UpgradeEncodingProfileTo22(profile);
			}

			if (databaseVersion < 23)
			{
				UpgradeEncodingProfileTo23(profile);
			}

			if (databaseVersion < 24)
			{
				UpgradeEncodingProfileTo24(profile);
			}

		    if (databaseVersion < 25)
		    {
		        UpgradeEncodingProfileTo25(profile);
		    }

		    if (databaseVersion < 26)
		    {
		        UpgradeEncodingProfileTo26(profile);
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

		public static void UpgradeEncodingProfileTo14(VCProfile profile)
		{
			profile.AudioEncoderFallback = UpgradeAudioEncoder(profile.AudioEncoderFallback);
		}

		public static void UpgradeEncodingProfileTo15(VCProfile profile)
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

		public static void UpgradeEncodingProfileTo16(VCProfile profile)
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

		public static void UpgradeEncodingProfileTo17(VCProfile profile)
		{
			if (profile.X264Profile == "high444")
			{
				profile.X264Profile = null;
			}
		}

		public static void UpgradeEncodingProfileTo19(VCProfile profile)
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

		public static void UpgradeEncodingProfileTo20(VCProfile profile)
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

		public static void UpgradeEncodingProfileTo21(VCProfile profile)
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

		public static void UpgradeEncodingProfileTo22(VCProfile profile)
		{
			profile.QsvDecode = true;
		}

		public static void UpgradeEncodingProfileTo23(VCProfile profile)
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

		public static void UpgradeEncodingProfileTo24(VCProfile profile)
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

		public static void UpgradeEncodingProfileTo25(VCProfile profile)
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

	    public static void UpgradeEncodingProfileTo26(VCProfile profile)
	    {
            if (profile.DenoiseType == VCDenoise.NlMeans)
	        {
	            profile.DenoiseType = VCDenoise.NLMeans;
	        }
        }

		private static void ErrorCheckPresets(List<Preset> presets)
		{
			for (int i = presets.Count - 1; i >= 0; i--)
			{
				var preset = presets[i];

				if (preset.EncodingProfile == null)
				{
					presets.RemoveAt(i);

					// Splash screen eats first dialog, need to show twice.
					Ioc.Container.GetInstance<IMessageBoxService>().Show("Could not load corrupt preset '" + preset.GetDisplayName() + "'.");
					Ioc.Container.GetInstance<IMessageBoxService>().Show("Could not load corrupt preset '" + preset.GetDisplayName() + "'.");
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

			return Utilities.CurrentDatabaseVersion;
		}

		/// <summary>
		/// Saves the given preset data.
		/// </summary>
		/// <param name="presetJsonListObject">List&lt;Tuple&lt;string, string&gt;&gt; with the file name and JSON string to save.</param>
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

				var presetJsonList = presetJsonListObject as List<string>;

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
	}
}
