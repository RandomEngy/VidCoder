using System;
using System.Linq;
using System.Windows;
using Microsoft.AnyContainer;
using VidCoder.Model;
using VidCoder.Resources;
using VidCoder.Services;
using VidCoder.Services.Windows;
using VidCoder.View;
using VidCoder.ViewModel;
using VidCoderCommon.Model;
using VidCoderCommon.Services;

namespace VidCoder.Automation;

public class VidCoderAutomation : IVidCoderAutomation
{
	private IAppLogger logger = StaticResolver.Resolve<IAppLogger>();

	public void Encode(string source, string destination, string preset, string picker)
	{
		this.logger.Log("Processing Encode request");
		var processingService = StaticResolver.Resolve<ProcessingService>();
		DispatchUtilities.Invoke(() =>
		{
			processingService.QueueAndStartFromNames(source, destination, preset, picker);
		});
	}

	public void EncodeWatchedFiles(string watchedFolderPath, string[] sourcePaths, string preset, string picker)
	{
		this.logger.Log("Processing file watcher queue request");
		var processingService = StaticResolver.Resolve<ProcessingService>();
		DispatchUtilities.Invoke(() =>
		{
			processingService.QueueFromFileWatcher(watchedFolderPath, sourcePaths, preset, picker);
		});
	}

	public void NotifyRemovedFiles(string[] removedFiles)
	{
		this.logger.Log("Processing file removal notification");
		var processingService = StaticResolver.Resolve<ProcessingService>();
		DispatchUtilities.Invoke(() =>
		{
			processingService.NotifyWatchedFilesRemoved();
		});
	}

	public void Scan(string source)
	{
		this.logger.Log("Processing Scan request");
		var mainVM = StaticResolver.Resolve<MainViewModel>();
		DispatchUtilities.Invoke(() =>
		{
			mainVM.ScanFromAutoplay(source);
		});
	}

	public void ImportPreset(string filePath)
	{
		this.logger.Log("Processing Import Preset request");
		var presetImporter = StaticResolver.Resolve<IPresetImportExport>();
		DispatchUtilities.Invoke(() =>
		{
			try
			{
				Preset preset = presetImporter.ImportPreset(filePath);
				this.ShowMessage(string.Format(MainRes.PresetImportSuccessMessage, preset.Name));
			}
			catch (Exception)
			{
				this.ShowMessage(MainRes.PresetImportErrorMessage);
				throw;
			}
		});
	}

	public void ImportQueue(string filePath)
	{
		this.logger.Log("Processing Import Queue request");
		var queueImporter = StaticResolver.Resolve<IQueueImportExport>();
		DispatchUtilities.Invoke(() =>
		{
			try
			{
				queueImporter.Import(filePath);
				this.ShowMessage(MainRes.QueueImportSuccessMessage);
			}
			catch (Exception)
			{
				this.ShowMessage(MainRes.QueueImportErrorMessage);
				throw;
			}
		});
	}

	public void BringToForeground()
	{
		this.logger.Log("Processing Bring to Foreground request");
		StaticResolver.Resolve<Main>().EnsureVisible();
	}

	private void ShowMessage(string message)
	{
		StaticResolver.Resolve<StatusService>().Show(message);
		StaticResolver.Resolve<IWindowManager>().Activate(StaticResolver.Resolve<MainViewModel>());
	}
}
