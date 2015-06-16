using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Xml;
using Newtonsoft.Json;
using VidCoder.Resources;

namespace VidCoder.Model
{
	public static class EncodeJobStorage
	{
		public static IList<EncodeJobWithMetadata> EncodeJobs
		{
			get
			{
				string jobsJson = Config.EncodeJobs2;
				if (string.IsNullOrEmpty(jobsJson))
				{
					return new List<EncodeJobWithMetadata>();
				}

				return ParseJobsJson(jobsJson);
			}

			set
			{
				Config.EncodeJobs2 = SerializeJobs(value);
			}
		}

		public static IList<EncodeJobWithMetadata> LoadQueueFile(string queueFile)
		{
			string extension = Path.GetExtension(queueFile).ToLowerInvariant();

			if (extension != ".xml" && extension != ".vjqueue")
			{
				return null;
			}

			try
			{
				if (extension == ".xml")
				{
					DataContractSerializer serializer = new DataContractSerializer(typeof(EncodeJobsXml));

					using (var reader = XmlReader.Create(queueFile))
					{
						var jobsXmlObject = serializer.ReadObject(reader) as EncodeJobsXml;
						return jobsXmlObject.Jobs;
					}
				}
				else
				{
					return ParseJobsJson(File.ReadAllText(queueFile));
				}
			}
			catch (Exception)
			{
				return null;
			}
		}

		public static bool SaveQueueToFile(IList<EncodeJobWithMetadata> jobs, string filePath)
		{
			try
			{
				File.WriteAllText(filePath, SerializeJobs(jobs));

				return true;
			}
			catch (Exception exception)
			{
				System.Windows.MessageBox.Show(string.Format(MainRes.CouldNotSaveQueueMessage, Environment.NewLine, exception));
			}

			return false;
		}

		internal static string SerializeJobs(IList<EncodeJobWithMetadata> jobs)
		{
			return JsonConvert.SerializeObject(jobs);
		}

		internal static IList<EncodeJobWithMetadata> ParseJobsJson(string jobsJson)
		{
			try
			{
				var jobsList = JsonConvert.DeserializeObject<IList<EncodeJobWithMetadata>>(jobsJson);
				if (jobsList == null)
				{
					return new List<EncodeJobWithMetadata>();
				}
				else
				{
					return jobsList;
				}
			}
			catch (Exception exception)
			{
				System.Windows.MessageBox.Show(
					"Could not load encode queue: " +
					exception +
					Environment.NewLine +
					Environment.NewLine +
					jobsJson);
			}

			return new List<EncodeJobWithMetadata>();
		}
	}
}
