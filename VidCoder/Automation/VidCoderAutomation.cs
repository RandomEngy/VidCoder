using System;
using Microsoft.AnyContainer;
using VidCoder.Resources;
using VidCoder.Services;
using VidCoder.Services.Windows;
using VidCoder.ViewModel;
using VidCoderCLI;
using VidCoderCommon.Model;

namespace VidCoder.Automation
{
	public class VidCoderAutomation : IVidCoderAutomation
	{
		public void Encode(string source, string destination, string preset, string picker)
		{
			var processingService = StaticResolver.Resolve<ProcessingService>();
			DispatchUtilities.Invoke(() =>
			{
				processingService.Process(source, destination, preset, picker);
			});
		}

		public void Scan(string source)
		{
			var mainVM = StaticResolver.Resolve<MainViewModel>();
			DispatchUtilities.Invoke(() =>
			{
				mainVM.ScanFromAutoplay(source);
			});
		}

		public void ImportPreset(string filePath)
		{
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

		private void ShowMessage(string message)
		{
			StaticResolver.Resolve<StatusService>().Show(message);
			StaticResolver.Resolve<IWindowManager>().Activate(StaticResolver.Resolve<MainViewModel>());
		}
	}
}
