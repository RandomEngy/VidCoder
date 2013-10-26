using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VidCoder.Automation
{
	using System.ServiceModel;
	using VidCoderCLI;
	using ViewModel;
	using ViewModel.Components;

	public class VidCoderAutomation : IVidCoderAutomation
	{
		public void Encode(string source, string destination, string preset)
		{
			var processingVM = Ioc.Container.GetInstance<ProcessingViewModel>();

			try
			{
				processingVM.Process(source, destination, preset);
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
