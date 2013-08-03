using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using Microsoft.Practices.Unity;
using VidCoder.Services;
using VidCoder.ViewModel;
using VidCoder.ViewModel.Components;

namespace VidCoder
{
	public static class Unity
	{
		static Unity()
		{
			Container = new UnityContainer();
			Container.RegisterType<IDriveService, DriveService>(new ContainerControlledLifetimeManager());
			Container.RegisterType<IUpdater, Updater>();
			Container.RegisterType<IMessageBoxService, MessageBoxService>(new ContainerControlledLifetimeManager());
			Container.RegisterType<ILogger, Logger>(new ContainerControlledLifetimeManager(), new InjectionConstructor());
			Container.RegisterType<IFileService, FileService>(new ContainerControlledLifetimeManager());
			Container.RegisterType<IPresetImportExport, PresetImportExport>();
			Container.RegisterType<IQueueImportExport, QueueImportExport>();
			Container.RegisterType<IProcesses, Processes>(new ContainerControlledLifetimeManager());
			Container.RegisterType<IProcessAutoPause, ProcessAutoPause>();
			Container.RegisterType<ISystemOperations, SystemOperations>();
			Container.RegisterType<TrayService>();
			Container.RegisterType<ClipboardService>();

			//Container.RegisterType<MainViewModel>(new ContainerControlledLifetimeManager());
			Container.RegisterType<OutputPathViewModel>(new ContainerControlledLifetimeManager());
			Container.RegisterType<PresetsViewModel>(new ContainerControlledLifetimeManager());
			Container.RegisterType<ProcessingViewModel>(new ContainerControlledLifetimeManager());
			Container.RegisterType<WindowManagerViewModel>(new ContainerControlledLifetimeManager());
		}

		public static IUnityContainer Container { get; set; }
	}
}
