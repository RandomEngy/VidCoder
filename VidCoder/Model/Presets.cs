using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml.Serialization;
using System.Xml;
using System.Xml.Linq;
using System.Threading;
using HandBrake.Interop;
using System.Globalization;

namespace VidCoder.Model
{
	public static class Presets
	{
		private const int CurrentPresetVersion = 3;

		private static readonly string UserPresetsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"VidCoder\UserPresets");
		private static readonly string BuiltInPresetsPath = "BuiltInPresets.xml";
		private static XmlSerializer presetListSerializer = new XmlSerializer(typeof(List<Preset>));
		private static XmlSerializer presetSerializer = new XmlSerializer(typeof(Preset));
		private static object userPresetSync = new object();

		public static List<Preset> BuiltInPresets
		{
			get
			{
				EnsureFolderCreated();
				using (FileStream stream = new FileStream(BuiltInPresetsPath, FileMode.Open, FileAccess.Read))
				{
					var presetList = presetListSerializer.Deserialize(stream) as List<Preset>;
					return presetList;
				}
			}

			set
			{
				EnsureFolderCreated();
				using (FileStream stream = new FileStream(BuiltInPresetsPath, FileMode.Create, FileAccess.Write))
				{
					presetListSerializer.Serialize(stream, value);
				}
			}
		}

		public static List<Preset> UserPresets
		{
			get
			{
				EnsureFolderCreated();

				lock (userPresetSync)
				{
					var result = new List<Preset>();

					string[] presetFiles = Directory.GetFiles(UserPresetsFolder);

					foreach (string presetFile in presetFiles)
					{
						Preset preset = LoadPreset(presetFile);

						if (preset != null)
						{
							result.Add(preset);
						}
					}

					return result;
				}
			}

			set
			{
				EnsureFolderCreated();
				var presetXmlList = new List<Tuple<string, string>>();

				foreach (Preset preset in value)
				{
					StringBuilder xmlBuilder = new StringBuilder();
					using (XmlWriter writer = XmlWriter.Create(xmlBuilder))
					{
						presetSerializer.Serialize(writer, preset);
					}

					string fileName = preset.Name;
					if (preset.IsModified)
					{
						fileName += "_Modified";
					}

					presetXmlList.Add(Tuple.Create<string, string>(fileName, xmlBuilder.ToString()));
				}

				// Do the actual save asynchronously.
				ThreadPool.QueueUserWorkItem(SaveUserPresetsBackground, presetXmlList);
			}
		}

		/// <summary>
		/// Loads in a preset from a file.
		/// </summary>
		/// <param name="presetFile">The file to load the preset from.</param>
		/// <returns>The parsed preset from the file, or null if the preset is invalid.</returns>
		public static Preset LoadPreset(string presetFile)
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
						UpgradePreset(preset);
					}

					return preset;
				}
			}
			catch (XmlException)
			{
				return null;
			}
		}

		private static void UpgradePreset(Preset preset)
		{
			foreach (AudioEncoding audioEncoding in preset.EncodingProfile.AudioEncodings)
			{
				if (!string.IsNullOrEmpty(audioEncoding.SampleRate))
				{
					double sampleRate = double.Parse(audioEncoding.SampleRate);
					audioEncoding.SampleRateRaw = (int)(sampleRate * 1000);
					audioEncoding.SampleRate = null;
				}

				if (preset.EncodingProfile.OutputFormat == OutputFormat.Mkv)
				{
					if (audioEncoding.Encoder == AudioEncoder.Ac3Passthrough || audioEncoding.Encoder == AudioEncoder.DtsPassthrough)
					{
						audioEncoding.Encoder = AudioEncoder.Passthrough;
					}
				}
			}
		}

		/// <summary>
		/// Saves the given preset data.
		/// </summary>
		/// <param name="presetXmlListObject">List&lt;Tuple&lt;string, string&gt;&gt; with the file name and XML string to save.</param>
		private static void SaveUserPresetsBackground(object presetXmlListObject)
		{
			lock (userPresetSync)
			{
				string[] existingFiles = Directory.GetFiles(UserPresetsFolder);
				foreach (string existingFile in existingFiles)
				{
					File.Delete(existingFile);
				}

				var presetXmlList = presetXmlListObject as List<Tuple<string, string>>;
				foreach (Tuple<string, string> listItem in presetXmlList)
				{
					XElement element = XElement.Parse(listItem.Item2);
					XDocument doc = new XDocument(
						new XElement("UserPreset",
							new XAttribute("Version", CurrentPresetVersion.ToString(CultureInfo.InvariantCulture)),
							element));

					doc.Save(FindUserPresetPath(listItem.Item1));
				}
			}
		}

		private static string FindUserPresetPath(string baseName)
		{
			string cleanName = Utilities.CleanFileName(baseName);

			string proposedPresetPath = Path.Combine(UserPresetsFolder, cleanName + ".xml");

			if (File.Exists(proposedPresetPath))
			{
				for (int i = 1; i < 100; i++)
				{
					proposedPresetPath = Path.Combine(UserPresetsFolder, cleanName + "_" + i + ".xml");
					if (!File.Exists(proposedPresetPath))
					{
						break;
					}
				}
			}

			return proposedPresetPath;
		}

		private static void EnsureFolderCreated()
		{
			if (!Directory.Exists(UserPresetsFolder))
			{
				Directory.CreateDirectory(UserPresetsFolder);
			}
		}
	}
}
