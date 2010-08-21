using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using System.Xml.Linq;
using HandBrake.Interop;

namespace VidCoder.Model
{
	public static class EncodeJobsPersist
	{
		private static readonly string DataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"VidCoder");
		private static readonly string EncodeJobsPath = Path.Combine(DataFolder, @"EncodeJobs.xml");
		private static XmlSerializer xmlSerializer = new XmlSerializer(typeof(List<EncodeJob>));

		public static List<EncodeJob> EncodeJobs
		{
			get
			{
				EnsureFolderCreated();

				try
				{
					XDocument doc = XDocument.Load(EncodeJobsPath);
					XElement presetArray = doc.Element("EncodeJobs").Element("ArrayOfEncodeJob");

					using (XmlReader reader = presetArray.CreateReader())
					{
						var presetList = xmlSerializer.Deserialize(reader) as List<EncodeJob>;
						return presetList;
					}
				}
				catch (FileNotFoundException)
				{
					return new List<EncodeJob>();
				}
			}

			set
			{
				EnsureFolderCreated();
				StringBuilder xmlBuilder = new StringBuilder();
				using (XmlWriter writer = XmlWriter.Create(xmlBuilder))
				{
					xmlSerializer.Serialize(writer, value);
				}

				XElement element = XElement.Parse(xmlBuilder.ToString());
				XDocument doc = new XDocument(
					new XElement("EncodeJobs",
						new XAttribute("Version", "1"),
						element));

				doc.Save(EncodeJobsPath);
			}
		}

		private static void EnsureFolderCreated()
		{
			if (!Directory.Exists(DataFolder))
			{
				Directory.CreateDirectory(DataFolder);
			}
		}
	}
}
