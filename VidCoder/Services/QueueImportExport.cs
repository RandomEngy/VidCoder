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
		private ProcessingService processingService = Ioc.Container.GetInstance<ProcessingService>();

		public QueueImportExport(IFileService fileService, IMessageBoxService messageBoxService)
		{
			this.fileService = fileService;
			this.messageBoxService = messageBoxService;
		}

		public void Import(string queueFile)
		{
			IList<EncodeJobWithMetadata> jobs = EncodeJobsPersist.LoadQueueFile(queueFile);
			if (jobs == null)
			{
				this.messageBoxService.Show(MainRes.QueueImportErrorMessage, MainRes.ImportErrorTitle, System.Windows.MessageBoxButton.OK);
				return;
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

			this.messageBoxService.Show(MainRes.QueueImportSuccessMessage, CommonRes.Success, System.Windows.MessageBoxButton.OK);
		}

		public void Export(IList<EncodeJobWithMetadata> jobPersistGroup)
		{
			string initialFileName = "Queue";

			string exportFileName = this.fileService.GetFileNameSave(
				Config.RememberPreviousFiles ? Config.LastPresetExportFolder : null,
				MainRes.ExportQueueFilePickerText,
				FileUtilities.CleanFileName(initialFileName + ".xml"),
				"xml",
				Utilities.GetFilePickerFilter("xml"));
			if (exportFileName != null)
			{
				if (Config.RememberPreviousFiles)
				{
					Config.LastPresetExportFolder = Path.GetDirectoryName(exportFileName);
				}

				if (EncodeJobsPersist.SaveQueueToFile(jobPersistGroup, exportFileName))
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
