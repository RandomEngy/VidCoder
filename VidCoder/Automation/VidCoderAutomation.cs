using System;
using System.ServiceModel;
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
			var processingService = Resolver.Resolve<ProcessingService>();
			DispatchUtilities.Invoke(() =>
			{
				try
				{
					processingService.Process(source, destination, preset, picker);
				}
				catch (Exception exception)
				{
					throw new FaultException<AutomationError>(new AutomationError { Message = exception.Message });
				}
			});
		}

		public void Scan(string source)
		{
			var mainVM = Resolver.Resolve<MainViewModel>();
			DispatchUtilities.Invoke(() =>
			{
				try
				{
					mainVM.ScanFromAutoplay(source);
				}
				catch (Exception exception)
				{
					throw new FaultException<AutomationError>(new AutomationError { Message = exception.Message });
				}
			});
		}

		public void ImportPreset(string filePath)
		{
			var presetImporter = Resolver.Resolve<IPresetImportExport>();
			DispatchUtilities.Invoke(() =>
			{
				try
				{
					Preset preset = presetImporter.ImportPreset(filePath);
					this.ShowMessage(string.Format(MainRes.PresetImportSuccessMessage, preset.Name));
				}
				catch (Exception exception)
				{
					this.ShowMessage(MainRes.PresetImportErrorMessage);
					throw new FaultException<AutomationError>(new AutomationError { Message = exception.Message });
				}
			});
		}

		public void ImportQueue(string filePath)
		{
			var queueImporter = Resolver.Resolve<IQueueImportExport>();
			DispatchUtilities.Invoke(() =>
			{
				try
				{
					queueImporter.Import(filePath);
					this.ShowMessage(MainRes.QueueImportSuccessMessage);
				}
				catch (Exception exception)
				{
					this.ShowMessage(MainRes.QueueImportErrorMessage);
					throw new FaultException<AutomationError>(new AutomationError { Message = exception.Message });
				}
			});
		}

		private void ShowMessage(string message)
		{
			Resolver.Resolve<StatusService>().Show(message);
			Resolver.Resolve<IWindowManager>().Activate(Resolver.Resolve<MainViewModel>());
		}
	}
}
