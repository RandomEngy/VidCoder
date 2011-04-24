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
		private static readonly string EncodeJobsPath = Path.Combine(Utilities.AppFolder, "EncodeJobs.xml");
		private static XmlSerializer xmlSerializer = new XmlSerializer(typeof(List<EncodeJob>));

		public static List<EncodeJob> EncodeJobs
		{
			get
			{
				if (File.Exists(EncodeJobsPath))
				{
					XDocument doc = XDocument.Load(EncodeJobsPath);
					XElement presetArray = doc.Element("EncodeJobs").Element("ArrayOfEncodeJob");

					using (XmlReader reader = presetArray.CreateReader())
					{
						var presetList = xmlSerializer.Deserialize(reader) as List<EncodeJob>;
						File.Delete(EncodeJobsPath);

						return presetList;
					}
				}

				return LoadJobsXmlString(DatabaseConfig.GetConfigString("EncodeJobs", Database.Connection));
			}

			set
			{
				DatabaseConfig.SetConfigValue("EncodeJobs", SerializeJobs(value), Database.Connection);
			}
		}

		private static string SerializeJobs(List<EncodeJob> jobs)
		{
			var xmlBuilder = new StringBuilder();
			using (XmlWriter writer = XmlWriter.Create(xmlBuilder))
			{
				xmlSerializer.Serialize(writer, jobs);
			}

			return xmlBuilder.ToString();
		}

		private static List<EncodeJob> LoadJobsXmlString(string jobsXml)
		{
			try
			{
				using (var stringReader = new StringReader(jobsXml))
				{
					using (var xmlReader = new XmlTextReader(stringReader))
					{
						return xmlSerializer.Deserialize(xmlReader) as List<EncodeJob>;
					}
				}
			}
			catch (XmlException exception)
			{
				System.Windows.MessageBox.Show(
					"Could not load encode queue: " +
					exception +
					Environment.NewLine +
					Environment.NewLine +
					jobsXml);
			}

			return new List<EncodeJob>();
		}
	}
}
