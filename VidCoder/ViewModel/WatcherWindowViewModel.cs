using Microsoft.AnyContainer;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using VidCoder.Resources;
using VidCoder.Services;
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

		private ReactiveCommand<Unit, Unit> installWatcherService;
		public ICommand InstallWatcherService
		{
			get
			{
				return this.installWatcherService ?? (this.installWatcherService = ReactiveCommand.Create(
					() =>
					{
						if (VidCoderInstall.RunElevatedSetup("activateFileWatcher", StaticResolver.Resolve<IAppLogger>()))
						{
							this.ServiceIsInstalled = true;
						}
						else
						{
							Utilities.MessageBox.Show(WatcherRes.ServiceInstallError);
						}
					}));
			}
		}

		private ReactiveCommand<Unit, Unit> uninstallWatcherService;
		public ICommand UninstallWatcherService
		{
			get
			{
				return this.uninstallWatcherService ?? (this.uninstallWatcherService = ReactiveCommand.Create(
					() =>
					{
						if (VidCoderInstall.RunElevatedSetup("deactivateFileWatcher", StaticResolver.Resolve<IAppLogger>()))
						{
							this.ServiceIsInstalled = false;
						}
						else
						{
							Utilities.MessageBox.Show(WatcherRes.ServiceUninstallError);
						}
					}));
			}
		}
	}
}
