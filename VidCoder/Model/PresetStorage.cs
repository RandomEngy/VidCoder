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
using System.Threading.Tasks;
using System.Windows.Markup.Localizer;
using HandBrake.Interop.Interop;
using HandBrake.Interop.Interop.HbLib;
using HandBrake.Interop.Interop.Model.Encoding;
using Microsoft.AnyContainer;
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
					newPreset.InjectFrom<CloneInjection>(p);

					return newPreset;
				}).ToList();

				pendingSavePresetList = clonedList;

				// Do the actual save asynchronously.
				Task.Run(() =>
				{
					SaveUserPresetsBackground(clonedList);
				});
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

			if (extension == ".xml")
			{
				throw new ArgumentException("Exported preset file is too old to open. Open with VidCoder 3.15 and export to upgrade it.");
			}

			if (extension != ".vjpreset")
			{
				throw new ArgumentException("File extension " + extension + " is not recognized.");
			}

			if (!File.Exists(presetFile))
			{
				throw new ArgumentException("Preset file could not be found.");
			}

			PresetWrapper presetWrapper = JsonConvert.DeserializeObject<PresetWrapper>(File.ReadAllText(presetFile));
			if (presetWrapper.Version < 20)
			{
				throw new ArgumentException("Exported preset file is too old to open. Open with VidCoder 3.15 and export to upgrade it.");
			}

			UpgradeEncodingProfile(presetWrapper.Preset.EncodingProfile, PresetToDbVersion(presetWrapper.Version));

			return presetWrapper.Preset;
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
				//new EncodingProfileUpgrade(31, UpgradeEncodingProfileTo31),
				//new EncodingProfileUpgrade(32, UpgradeEncodingProfileTo32),
				//new EncodingProfileUpgrade(33, UpgradeEncodingProfileTo33),
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

		private static void ErrorCheckPresets(List<Preset> presets)
		{
			for (int i = presets.Count - 1; i >= 0; i--)
			{
				var preset = presets[i];

				if (preset.EncodingProfile == null)
				{
					presets.RemoveAt(i);

					// Splash screen eats first dialog, need to show twice.
					StaticResolver.Resolve<IMessageBoxService>().Show("Could not load corrupt preset '" + preset.GetDisplayName() + "'.");
					StaticResolver.Resolve<IMessageBoxService>().Show("Could not load corrupt preset '" + preset.GetDisplayName() + "'.");
				}
				else
				{
					ErrorCheckPreset(preset);
				}
			}
		}

		private static void ErrorCheckPreset(Preset preset)
		{
			ErrorCheckEncodingProfile(preset.EncodingProfile);
		}

		public static void ErrorCheckEncodingProfile(VCProfile profile)
		{
			// mp4v2 only available on x86
			string containerName = profile.ContainerName;
			if (HandBrakeEncoderHelpers.GetContainer(containerName) == null)
			{
				if (containerName == "mp4v2")
				{
					profile.ContainerName = "av_mp4";
				}
			}

			// QSV H.264 only available on systems with the right hardware.
			string videoEncoderName = profile.VideoEncoder;
			if (HandBrakeEncoderHelpers.GetVideoEncoder(videoEncoderName) == null)
			{
				if (videoEncoderName == "qsv_h264")
				{
					profile.VideoEncoder = "x264";
				}
			}

			foreach (var audioEncoding in profile.AudioEncodings)
			{
				if (audioEncoding.Encoder == "fdk_aac")
				{
					// Make sure this version of VidCoder recognizes the encoder
					HBAudioEncoder encoder = HandBrakeEncoderHelpers.GetAudioEncoder(audioEncoding.Encoder);
					if (encoder == null)
					{
						audioEncoding.Encoder = "av_aac";
						StaticResolver.Resolve<IAppLogger>().Log("Preset specified fdk_aac but it is not supported in this build of VidCoder. Fell back to av_aac.");
					}
				}
			}
		}

		private static int PresetToDbVersion(int presetVersion)
		{
			if (presetVersion >= CurrentPresetVersion)
			{
				return Utilities.CurrentDatabaseVersion;
			}

			//if (presetVersion < 19)
			//{
			//	return 31;
			//}

			//if (presetVersion < 20)
			//{
			//	return 32;
			//}

			return Utilities.CurrentDatabaseVersion;
		}

		/// <summary>
		/// Saves the given preset data.
		/// </summary>
		/// <param name="presetList">List&lt;Preset&gt; to save.</param>
		private static void SaveUserPresetsBackground(List<Preset> presetList)
		{
			lock (userPresetSync)
			{
				// If the pending save list is different from the one we've been passed, we abort.
				// This means that another background task has been scheduled with a more recent save.
				if (presetList != pendingSavePresetList)
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
