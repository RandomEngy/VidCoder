using System;
using System.ServiceModel;
using VidCoder.Services;
using VidCoder.ViewModel;
using VidCoderCLI;

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
	}
}
