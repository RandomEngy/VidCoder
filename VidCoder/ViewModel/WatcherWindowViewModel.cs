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
using VidCoder.Model;
using VidCoder.Resources;
using VidCoder.Services;
using VidCoder.Services.Windows;
using VidCoderCommon;
using VidCoderCommon.Model;

namespace VidCoder.ViewModel
{
	public class WatcherWindowViewModel : ReactiveObject
	{
		private IWindowManager windowManager = StaticResolver.Resolve<IWindowManager>();

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

		private ReactiveCommand<Unit, Unit> openConfig;
		public ICommand OpenConfig
		{
			get
			{
				return this.openConfig ?? (this.openConfig = ReactiveCommand.Create(
					() =>
					{
						var watcherConfigDialog = new WatcherConfigDialogViewModel();
						this.windowManager.OpenDialog(watcherConfigDialog, this);

						if (watcherConfigDialog.DialogResult)
						{
							WatcherStorage.SaveWatchedFolders(Database.Connection, watcherConfigDialog.WatchedFolders.Items.Select(watchedFolderViewModel => watchedFolderViewModel.WatchedFolder).ToList());
						}
					}));
			}
		}
	}
}
