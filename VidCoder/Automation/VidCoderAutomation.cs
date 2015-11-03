using System;
using System.ServiceModel;
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
			var processingService = Ioc.Get<ProcessingService>();

			try
			{
				processingService.Process(source, destination, preset, picker);
			}
			catch (Exception exception)
			{
				throw new FaultException<AutomationError>(new AutomationError { Message = exception.Message });
			}
		}

		public void Scan(string source)
		{
			var mainVM = Ioc.Get<MainViewModel>();

			try
			{
				mainVM.ScanFromAutoplay(source);
			}
			catch (Exception exception)
			{
				throw new FaultException<AutomationError>(new AutomationError { Message = exception.Message });
			}
		}

		public void ImportPreset(string filePath)
		{
			var presetImporter = Ioc.Get<IPresetImportExport>();

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
		}

		public void ImportQueue(string filePath)
		{
			var queueImporter = Ioc.Get<IQueueImportExport>();

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
		}

		private void ShowMessage(string message)
		{
			Ioc.Get<StatusService>().Show(message);
			Ioc.Get<IWindowManager>().Activate(Ioc.Get<MainViewModel>());
		}
	}
}
