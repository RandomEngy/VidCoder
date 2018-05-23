using System;
using Microsoft.Practices.ServiceLocation;
using Microsoft.Practices.Unity;
using VidCoder.Services;
using VidCoder.Services.Notifications;
using VidCoder.Services.Windows;
using VidCoder.ViewModel;
using VidCoderCommon.Services;

namespace VidCoder
{
	public static class Ioc
	{
		static Ioc()
		{
			Container = new UnityContainer();
			Container.RegisterType<IDriveService, DriveService>(Singleton);

			if (Utilities.IsRunningAsAppx)
			{
				Container.RegisterType<IToastNotificationService, ToastNotificationService>(Singleton);
			}
			else
			{
				Container.RegisterType<IToastNotificationService, StubToastNotificationService>(Singleton);
			}

			Container.RegisterType<IUpdater, Updater>(Singleton);
			Container.RegisterType<IMessageBoxService, MessageBoxService>(Singleton);
			Container.RegisterType<AppLogger>(Singleton, new InjectionFactory(c => new AppLogger()));
			Container.RegisterType<IAppLogger, AppLogger>(Singleton);
			Container.RegisterType<ILogger, AppLogger>(Singleton);
			Container.RegisterType<IFileService, FileService>(Singleton);
			Container.RegisterType<IPresetImportExport, PresetImportExport>(Singleton);
			Container.RegisterType<IQueueImportExport, QueueImportExport>(Singleton);
			Container.RegisterType<IProcesses, Processes>(Singleton);
			Container.RegisterType<IProcessAutoPause, ProcessAutoPause>(Singleton);
			Container.RegisterType<ISystemOperations, SystemOperations>(Singleton);
			Container.RegisterType<IWindowManager, WindowManager>(Singleton);

			Container.RegisterType<OutputPathService>(Singleton);
			Container.RegisterType<OutputSizeService>(Singleton);
			Container.RegisterType<PresetsService>(Singleton);
			Container.RegisterType<PickersService>(Singleton);
			Container.RegisterType<ProcessingService>(Singleton);
			Container.RegisterType<EncodingWindowViewModel>(Singleton);
			Container.RegisterType<StatusService>(Singleton);
			Container.RegisterType<PreviewUpdateService>(Singleton);

			ServiceLocator.SetLocatorProvider(() => new UnityServiceLocator(Container));
		}

		public static UnityContainer Container { get; set; }

		public static T Get<T>()
		{
			return Container.Resolve<T>();
		}

		public static object Get(Type type)
		{
			return Container.Resolve(type);
		}

		public static ContainerControlledLifetimeManager Singleton => new ContainerControlledLifetimeManager();
	}
}
