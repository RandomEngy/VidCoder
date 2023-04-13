using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.AnyContainer;
using VidCoder.Model;
using VidCoder.Resources;
using VidCoder.ViewModel;

namespace VidCoder.Services;

public class QueueImportExport : IQueueImportExport
{
	private IFileService fileService;
	private IMessageBoxService messageBoxService;
	private ProcessingService processingService = StaticResolver.Resolve<ProcessingService>();
	private IAppLogger logger;

	public QueueImportExport(IFileService fileService, IMessageBoxService messageBoxService, IAppLogger logger)
	{
		this.fileService = fileService;
		this.messageBoxService = messageBoxService;
		this.logger = logger;
	}

	public IList<EncodeJobWithMetadata> Import(string queueFile)
	{
		try
		{
			IList<EncodeJobWithMetadata> jobs = EncodeJobStorage.LoadQueueFile(queueFile);
			if (jobs == null)
			{
				throw new ArgumentException("Queue file is malformed.");
			}

			foreach (var job in jobs)
			{
				this.processingService.QueueJob(new EncodeJobViewModel(
					job.Job,
					job.VideoSource,
					job.VideoSourceMetadata,
					sourceParentFolder: job.SourceParentFolder,
					manualOutputPath: job.ManualOutputPath,
					nameFormatOverride: job.NameFormatOverride,
					presetName: job.PresetName));
			}

			return jobs;
		}
		catch (Exception exception)
		{
			this.logger.LogError("Queue import failed: " + exception.Message);
			throw;
		}
	}

	public void Export(IList<EncodeJobWithMetadata> jobs)
	{
		string initialFileName = "Queue";

		string exportFileName = this.fileService.GetFileNameSave(
			Config.RememberPreviousFiles ? Config.LastPresetExportFolder : null,
			MainRes.ExportQueueFilePickerText,
			FileUtilities.CleanFileName(initialFileName + ".vjqueue"),
			"vjqueue",
			CommonRes.QueueFileFilter + "|*.vjqueue");
		if (exportFileName != null)
		{
			if (Config.RememberPreviousFiles)
			{
				Config.LastPresetExportFolder = Path.GetDirectoryName(exportFileName);
			}

			if (EncodeJobStorage.SaveQueueToFile(jobs, exportFileName))
			{
				this.messageBoxService.Show(
					string.Format(MainRes.QueueExportSuccessMessage, exportFileName),
					CommonRes.Success,
					System.Windows.MessageBoxButton.OK);
			}
		}
	}
}
