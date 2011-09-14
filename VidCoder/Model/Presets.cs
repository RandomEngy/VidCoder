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
using HandBrake.Interop.Model.Encoding;

namespace VidCoder.Model
{
	public static class Presets
	{
		private const int CurrentPresetVersion = 3;

		private static readonly string UserPresetsFolder = Path.Combine(Utilities.AppFolder, "UserPresets");
		private static readonly string BuiltInPresetsPath = "BuiltInPresets.xml";
		private static XmlSerializer presetListSerializer = new XmlSerializer(typeof(PresetCollection));
		private static XmlSerializer presetSerializer = new XmlSerializer(typeof(Preset));
		private static object userPresetSync = new object();

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

					return result;
				}
			}

			set
			{
				var presetXmlList = new List<string>();

				foreach (Preset preset in value)
				{
					presetXmlList.Add(SerializePreset(preset));
				}

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
					if (version < CurrentPresetVersion)
					{
						// Remove the preset if it hasn't been upgraded by now.
						return null;
					}

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
						return presetSerializer.Deserialize(xmlReader) as Preset;
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

				SQLiteConnection connection = Database.CreateConnection();

				using (SQLiteTransaction transaction = connection.BeginTransaction())
				{
					Database.ExecuteNonQuery("DELETE FROM presetsXml", connection);

					var insertCommand = new SQLiteCommand("INSERT INTO presetsXml (xml) VALUES (?)", connection);
					SQLiteParameter insertXmlParam = insertCommand.Parameters.Add("xml", DbType.String);

					var presetXmlList = presetXmlListObject as List<string>;
					foreach (string presetXml in presetXmlList)
					{
						insertXmlParam.Value = presetXml;
						insertCommand.ExecuteNonQuery();
					}

					transaction.Commit();
				}
			}
		}
	}
}
