using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using System.Xml.Linq;
using HandBrake.Interop;
using HandBrake.Interop.Model;

namespace VidCoder.Model
{
	public static class EncodeJobsPersist
	{
		private static XmlSerializer xmlSerializer = new XmlSerializer(typeof(List<EncodeJob>));

		public static List<EncodeJob> EncodeJobs
		{
			get
			{
				string jobsXml = DatabaseConfig.GetConfigString("EncodeJobs", Database.Connection);
				if (string.IsNullOrEmpty(jobsXml))
				{
					return new List<EncodeJob>();
				}

				return LoadJobsXmlString(jobsXml);
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
