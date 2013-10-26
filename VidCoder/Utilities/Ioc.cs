namespace VidCoder
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;
	using GalaSoft.MvvmLight.Ioc;
	using Services;
	using ViewModel.Components;

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
			Container.Register(() => new TrayService());
			Container.Register(() => new ClipboardService());

			Container.Register<OutputPathViewModel>();
			Container.Register<PresetsViewModel>();
			Container.Register<ProcessingViewModel>();
			Container.Register<WindowManagerViewModel>();
		}

		public static SimpleIoc Container { get; set; }
	}
}
