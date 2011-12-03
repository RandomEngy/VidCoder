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
		private static XmlSerializer oldXmlSerializer = new XmlSerializer(typeof(EncodeJobCollection));
		private static XmlSerializer xmlSerializer = new XmlSerializer(typeof(EncodeJobPersistGroup));

		public static EncodeJobPersistGroup EncodeJobs
		{
			get
			{
				string jobsXml = DatabaseConfig.GetConfigString("EncodeJobs2", Database.Connection);
				if (string.IsNullOrEmpty(jobsXml))
				{
					// Check if there's an old queue collection we should upgrade
					List<EncodeJob> oldJobs = EncodeJobsOld;
					if (oldJobs.Count > 0)
					{
						// Populate the SourceType enum. We need this for editing queue items.
						for (int i = oldJobs.Count - 1; i >= 0; i--)
						{
							if (oldJobs[i].SourceType == SourceType.None)
							{
								if (Directory.Exists(oldJobs[i].SourcePath))
								{
									oldJobs[i].SourceType = SourceType.VideoFolder;
								}
								else if (File.Exists(oldJobs[i].SourcePath))
								{
									oldJobs[i].SourceType = SourceType.File;
								}
								else
								{
									oldJobs.RemoveAt(i);
								}
							}
						}

						foreach (EncodeJob job in oldJobs)
						{
							Presets.UpgradeEncodingProfile(job.EncodingProfile, 12);
						}

						var persistGroup = new EncodeJobPersistGroup();
						persistGroup.EncodeJobs.AddRange(oldJobs.Select(j => new EncodeJobWithMetadata { Job = j, ManualOutputPath = true }));

						EncodeJobs = persistGroup;
						return persistGroup;
					}

					return new EncodeJobPersistGroup();
				}

				return LoadJobsXmlString(jobsXml);
			}

			set
			{
				// Old are stored in EncodeJobs, new in EncodeJobs2
				DatabaseConfig.SetConfigValue("EncodeJobs2", SerializeJobs(value), Database.Connection);
			}
		}

		public static List<EncodeJob> EncodeJobsOld
		{
			get
			{
				string jobsXml = DatabaseConfig.GetConfigString("EncodeJobs", Database.Connection);
				if (string.IsNullOrEmpty(jobsXml))
				{
					return new List<EncodeJob>();
				}

				return LoadJobsXmlStringOld(jobsXml);
			}
		}

		internal static string SerializeJobs(EncodeJobPersistGroup jobPersistGroup)
		{
			var xmlBuilder = new StringBuilder();
			using (XmlWriter writer = XmlWriter.Create(xmlBuilder))
			{
				xmlSerializer.Serialize(writer, jobPersistGroup);
			}

			return xmlBuilder.ToString();
		}

		internal static EncodeJobPersistGroup LoadJobsXmlString(string jobsXml)
		{
			try
			{
				using (var stringReader = new StringReader(jobsXml))
				{
					using (var xmlReader = new XmlTextReader(stringReader))
					{
						var jobPersistGroup = xmlSerializer.Deserialize(xmlReader) as EncodeJobPersistGroup;
						if (jobPersistGroup == null)
						{
							return new EncodeJobPersistGroup();
						}

						return jobPersistGroup;
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

			return new EncodeJobPersistGroup();
		}

		private static List<EncodeJob> LoadJobsXmlStringOld(string jobsXml)
		{
			try
			{
				using (var stringReader = new StringReader(jobsXml))
				{
					using (var xmlReader = new XmlTextReader(stringReader))
					{
						var jobCollection = oldXmlSerializer.Deserialize(xmlReader) as EncodeJobCollection;
						if (jobCollection == null)
						{
							return new List<EncodeJob>();
						}

						return jobCollection.EncodeJobs;
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
