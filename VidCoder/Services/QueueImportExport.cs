using System;
using System.Collections.Generic;
using System.IO;
using VidCoder.Model;
using VidCoder.Resources;
using VidCoder.ViewModel;

namespace VidCoder.Services
{
	public class QueueImportExport : IQueueImportExport
	{
		private IFileService fileService;
		private IMessageBoxService messageBoxService;
		private ProcessingService processingService = Ioc.Get<ProcessingService>();
		private ILogger logger;

		public QueueImportExport(IFileService fileService, IMessageBoxService messageBoxService, ILogger logger)
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
					this.processingService.Queue(new EncodeJobViewModel(job.Job)
						{
							SourceParentFolder = job.SourceParentFolder,
							ManualOutputPath = job.ManualOutputPath,
							NameFormatOverride = job.NameFormatOverride,
							PresetName = job.PresetName
						});
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
}
