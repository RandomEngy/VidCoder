using System;
using Microsoft.Practices.Unity;
using VidCoder.Services;
using VidCoder.Services.Windows;
using VidCoder.ViewModel;

namespace VidCoder
{
	public static class Ioc
	{
		static Ioc()
		{
			Container = new UnityContainer();
			Container.RegisterType<IDriveService, DriveService>(Singleton);
			Container.RegisterType<IUpdater, Updater>(Singleton);
			Container.RegisterType<IMessageBoxService, MessageBoxService>(Singleton);
			Container.RegisterType<IAppLogger>(Singleton, new InjectionFactory(c => new AppLogger()));
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

		public static ContainerControlledLifetimeManager Singleton 
		{
			get
			{
				return new ContainerControlledLifetimeManager();
			}
		}
	}
}
