using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml.Serialization;
using System.Xml;
using System.Xml.Linq;
using System.Threading;
using System.Globalization;
using HandBrake.Interop;
using HandBrake.Interop.Model;
using HandBrake.Interop.Model.Encoding;

namespace VidCoder.Model
{
	public static class Presets
	{
		private const int CurrentPresetVersion = 6;

		private static readonly string UserPresetsFolder = Path.Combine(Utilities.AppFolder, "UserPresets");
		private static readonly string BuiltInPresetsPath = "BuiltInPresets.xml";
		private static XmlSerializer presetListSerializer = new XmlSerializer(typeof(PresetCollection));
		private static XmlSerializer presetSerializer = new XmlSerializer(typeof(Preset));
		private static object userPresetSync = new object();

		static Presets()
		{

		}

		public static List<Preset> BuiltInPresets
		{
			get
			{
				using (var stream = new FileStream(BuiltInPresetsPath, FileMode.Open, FileAccess.Read))
				{
					var presetCollection = presetListSerializer.Deserialize(stream) as PresetCollection;
					return presetCollection.Presets;
				}
			}
		}

		public static List<Preset> UserPresets
		{
			get
			{
				lock (userPresetSync)
				{
					var result = GetPresetListFromDb();

					if (result.Count == 0 && Directory.Exists(UserPresetsFolder))
					{
						// If we have no DB presets, check for old .xml files in the UserPresets folder
						string[] presetFiles = Directory.GetFiles(UserPresetsFolder);
						foreach (string presetFile in presetFiles)
						{
							Preset preset = LoadPresetFile(presetFile);
							if (preset != null)
							{
								result.Add(preset);
							}
						}
					}

					ErrorCheckPresets(result);

					return result;
				}
			}

			set
			{
				var presetXmlList = value.Select(SerializePreset).ToList();

				// Do the actual save asynchronously.
				ThreadPool.QueueUserWorkItem(SaveUserPresetsBackground, presetXmlList);
			}
		}

		/// <summary>
		/// Serializes a preset to XML. Does not include wrapper.
		/// </summary>
		/// <param name="preset">The preset to serialize.</param>
		/// <returns>The serialized XML of the preset.</returns>
		public static string SerializePreset(Preset preset)
		{
			var xmlBuilder = new StringBuilder();
			using (XmlWriter writer = XmlWriter.Create(xmlBuilder))
			{
				presetSerializer.Serialize(writer, preset);
			}

			return xmlBuilder.ToString();
		}

		/// <summary>
		/// Loads in a preset from a file.
		/// </summary>
		/// <param name="presetFile">The file to load the preset from.</param>
		/// <returns>The parsed preset from the file, or null if the preset is invalid.</returns>
		public static Preset LoadPresetFile(string presetFile)
		{
			if (Path.GetExtension(presetFile).ToLowerInvariant() != ".xml")
			{
				return null;
			}

			try
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
					var preset = presetSerializer.Deserialize(reader) as Preset;
					UpgradeEncodingProfile(preset.EncodingProfile, PresetToDbVersion(version));

					return preset;
				}
			}
			catch (XmlException)
			{
				return null;
			}
		}

		/// <summary>
		/// Load in a preset from an XML string.
		/// </summary>
		/// <param name="presetXml">The XML of the preset to load in (without wrapper XML).</param>
		/// <returns>The loaded Preset.</returns>
		public static Preset LoadPresetXmlString(string presetXml)
		{
			try
			{
				using (var stringReader = new StringReader(presetXml))
				{
					using (var xmlReader = new XmlTextReader(stringReader))
					{
						var preset = presetSerializer.Deserialize(xmlReader) as Preset;

						return preset;
					}
				}
			}
			catch (XmlException exception)
			{
				System.Windows.MessageBox.Show(
					"Could not load preset: " +
					exception +
					Environment.NewLine +
					Environment.NewLine +
					presetXml);
			}

			return null;
		}

		/// <summary>
		/// Saves a user preset to a file. Includes wrapper XML.
		/// </summary>
		/// <param name="preset">The preset to save.</param>
		/// <param name="filePath">The path to save the preset to.</param>
		/// <returns>True if the save succeeded.</returns>
		public static bool SavePresetToFile(Preset preset, string filePath)
		{
			try
			{
				string presetXml = SerializePreset(preset);

				XElement element = XElement.Parse(presetXml);
				var doc = new XDocument(
					new XElement("UserPreset",
					             new XAttribute("Version", CurrentPresetVersion.ToString(CultureInfo.InvariantCulture)),
					             element));

				doc.Save(filePath);
				return true;
			}
			catch (XmlException exception)
			{
				System.Windows.MessageBox.Show(string.Format("Could not save preset '{0}':{1}{2}", preset.Name, Environment.NewLine, exception));
			}

			return false;
		}

		public static void UpgradeEncodingProfile(EncodingProfile profile, int databaseVersion)
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
		}

		public static void UpgradeEncodingProfileTo13(EncodingProfile profile)
		{
			// Upgrade preset: translate old Enum-based values to short name strings
			if (!Encoders.VideoEncoders.Any(e => e.ShortName == profile.VideoEncoder))
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
				if (!Encoders.AudioEncoders.Any(e => e.ShortName == encoding.Encoder))
				{
					string newAudioEncoder = UpgradeAudioEncoder(encoding.Encoder);
					if (newAudioEncoder == null)
					{
						newAudioEncoder = "faac";
					}

					encoding.Encoder = newAudioEncoder;
				}

				if (!Encoders.Mixdowns.Any(m => m.ShortName == encoding.Mixdown))
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

		public static void UpgradeEncodingProfileTo14(EncodingProfile profile)
		{
			profile.AudioEncoderFallback = UpgradeAudioEncoder(profile.AudioEncoderFallback);
		}

		public static void UpgradeEncodingProfileTo15(EncodingProfile profile)
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
				profile.ConstantFramerate = !profile.PeakFramerate;
			}
		}

		private static void ErrorCheckPresets(List<Preset> presets)
		{
			foreach (Preset preset in presets)
			{
				if (preset.EncodingProfile.OutputFormat == Container.None)
				{
					preset.EncodingProfile.OutputFormat = Container.Mp4;
				}
			}
		}

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

			if (presetVersion == 5)
			{
				return 14;
			}

			return Utilities.CurrentDatabaseVersion;
		}

		/// <summary>
		/// Saves the given preset data.
		/// </summary>
		/// <param name="presetXmlListObject">List&lt;Tuple&lt;string, string&gt;&gt; with the file name and XML string to save.</param>
		private static void SaveUserPresetsBackground(object presetXmlListObject)
		{
			lock (userPresetSync)
			{
				if (Directory.Exists(UserPresetsFolder))
				{
					string[] existingFiles = Directory.GetFiles(UserPresetsFolder);
					foreach (string existingFile in existingFiles)
					{
						File.Delete(existingFile);
					}

					Directory.Delete(UserPresetsFolder);
				}

				var presetXmlList = presetXmlListObject as List<string>;

				SQLiteConnection connection = Database.CreateConnection();

				using (SQLiteTransaction transaction = connection.BeginTransaction())
				{
					SavePresets(presetXmlList, connection);
					transaction.Commit();
				}
			}
		}

		public static void SavePresets(List<string> presetXmlList, SQLiteConnection connection)
		{
			Database.ExecuteNonQuery("DELETE FROM presetsXml", connection);

			var insertCommand = new SQLiteCommand("INSERT INTO presetsXml (xml) VALUES (?)", connection);
			SQLiteParameter insertXmlParam = insertCommand.Parameters.Add("xml", DbType.String);

			foreach (string presetXml in presetXmlList)
			{
				insertXmlParam.Value = presetXml;
				insertCommand.ExecuteNonQuery();
			}
		}

		public static List<Preset> GetPresetListFromDb()
		{
			var result = new List<Preset>();

			var selectPresetsCommand = new SQLiteCommand("SELECT * FROM presetsXml", Database.Connection);
			using (SQLiteDataReader reader = selectPresetsCommand.ExecuteReader())
			{
				while (reader.Read())
				{
					string presetXml = reader.GetString("xml");
					result.Add(LoadPresetXmlString(presetXml));
				}
			}
			return result;
		}
	}
}
