using GalaSoft.MvvmLight.Ioc;
using VidCoder.Services;
using VidCoder.Services.Windows;

namespace VidCoder
{
	public static class Ioc
	{
		static Ioc()
		{
			Container = new SimpleIoc();
			Container.Register<IDriveService, DriveService>();
			Container.Register<IUpdater, Updater>();
			Container.Register<IMessageBoxService, MessageBoxService>();
			Container.Register<ILogger, Logger>();
			Container.Register<IFileService, FileService>();
			Container.Register<IPresetImportExport, PresetImportExport>();
			Container.Register<IQueueImportExport, QueueImportExport>();
			Container.Register<IProcesses, Processes>();
			Container.Register<IProcessAutoPause, ProcessAutoPause>();
			Container.Register<ISystemOperations, SystemOperations>();
			Container.Register<IWindowManager, WindowManager>();
			Container.Register(() => new TrayService());
			Container.Register(() => new ClipboardService());

			Container.Register<OutputPathService>();
			Container.Register<PresetsService>();
            Container.Register<PickersService>();
			Container.Register<ProcessingService>();
			Container.Register<WindowManagerService>();
		}

		public static SimpleIoc Container { get; set; }

		public static T Get<T>()
		{
			return Container.GetInstance<T>();
		}
	}
}
