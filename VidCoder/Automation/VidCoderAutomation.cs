using System;
using System.ServiceModel;
using GalaSoft.MvvmLight.Messaging;
using VidCoder.Messages;
using VidCoder.Resources;
using VidCoder.Services;
using VidCoder.ViewModel;
using VidCoderCLI;
using VidCoderCommon.Model;

namespace VidCoder.Automation
{
	public class VidCoderAutomation : IVidCoderAutomation
	{
		public void Encode(string source, string destination, string preset, string picker)
		{
			var processingService = Ioc.Container.GetInstance<ProcessingService>();

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
			var mainVM = Ioc.Container.GetInstance<MainViewModel>();

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
			var presetImporter = Ioc.Container.GetInstance<IPresetImportExport>();

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
			var queueImporter = Ioc.Container.GetInstance<IQueueImportExport>();

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
			Messenger.Default.Send(new StatusMessage { Message = message});
			WindowManager.ActivateWindow(Ioc.Container.GetInstance<MainViewModel>());
		}
	}
}
