using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using VidCoder.Resources;
using VidCoderCommon.Utilities;

namespace VidCoder.Model;

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

			return ParseAndErrorCheckJobsJson(jobsJson);
		}

		set
		{
			Config.EncodeJobs2 = SerializeJobs(value);
		}
	}

	public static IList<EncodeJobWithMetadata> LoadQueueFile(string queueFile)
	{
		string extension = Path.GetExtension(queueFile);
		if (extension != null)
		{
			extension = extension.ToLowerInvariant();
		}

		if (extension == ".xml")
		{
			throw new ArgumentException("Exported queue file is too old to open. Open with VidCoder 3.15 and export to upgrade it.");
		}

		if (extension != ".vjqueue")
		{
			throw new ArgumentException("File extension '" + extension + "' is not recognized.");
		}

		if (!File.Exists(queueFile))
		{
			throw new ArgumentException("Queue file could not be found.");
		}

		return ParseAndErrorCheckJobsJson(File.ReadAllText(queueFile));
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
		return JsonSerializer.Serialize(jobs, JsonOptions.WithUpgraders);
	}

	internal static IList<EncodeJobWithMetadata> ParseAndErrorCheckJobsJson(string jobsJson)
	{
		try
		{
			var jobsList = JsonSerializer.Deserialize<IList<EncodeJobWithMetadata>>(jobsJson, JsonOptions.WithUpgraders);
			if (jobsList == null)
			{
				return new List<EncodeJobWithMetadata>();
			}
			else
			{
				foreach (var encodeJobWithMetadata in jobsList)
				{
					PresetStorage.ErrorCheckEncodingProfile(encodeJobWithMetadata.Job.EncodingProfile);
				}

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
