using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using VidCoderCommon;

namespace VidCoder.ViewModel
{
	public class WatcherWindowViewModel : ReactiveObject
	{
		public WatcherWindowViewModel()
		{
			ServiceController controller = ServiceController.GetServices().FirstOrDefault(service => service.ServiceName == CommonUtilities.WatcherServiceName);
			this.serviceIsInstalled = controller != null;
		}

		private bool serviceIsInstalled;
		public bool ServiceIsInstalled
		{
			get { return this.serviceIsInstalled; }
			set { this.RaiseAndSetIfChanged(ref this.serviceIsInstalled, value); }
		}
	}
}
