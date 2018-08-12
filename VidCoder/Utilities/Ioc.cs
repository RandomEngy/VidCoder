using System;
using CommonServiceLocator;
using Microsoft.AnyContainer;
using Microsoft.AnyContainer.Unity;
using Unity;
using Unity.Injection;
using Unity.Lifetime;
using Unity.ServiceLocation;
using VidCoder.Services;
using VidCoder.Services.Notifications;
using VidCoder.Services.Windows;
using VidCoder.ViewModel;
using VidCoderCommon.Services;

namespace VidCoder
{
	public static class Ioc
	{
		public static void SetUp()
		{
			var container = new UnityAnyContainer();
			container.RegisterSingleton<IDriveService, DriveService>();

			if (Utilities.IsRunningAsAppx)
			{
				container.RegisterSingleton<IToastNotificationService, ToastNotificationService>();
			}
			else
			{
				container.RegisterSingleton<IToastNotificationService, StubToastNotificationService>();
			}

			container.RegisterSingleton<IUpdater, Updater>();
			container.RegisterSingleton<IMessageBoxService, MessageBoxService>();
			container.RegisterSingleton<AppLogger>(() => new AppLogger());
			container.RegisterSingleton<IAppLogger, AppLogger>();
			container.RegisterSingleton<ILogger, AppLogger>();
			container.RegisterSingleton<IFileService, FileService>();
			container.RegisterSingleton<IPresetImportExport, PresetImportExport>();
			container.RegisterSingleton<IQueueImportExport, QueueImportExport>();
			container.RegisterSingleton<IProcesses, Processes>();
			container.RegisterSingleton<IProcessAutoPause, ProcessAutoPause>();
			container.RegisterSingleton<ISystemOperations, SystemOperations>();
			container.RegisterSingleton<IWindowManager, WindowManager>();

			container.RegisterSingleton<OutputPathService>();
			container.RegisterSingleton<OutputSizeService>();
			container.RegisterSingleton<PresetsService>();
			container.RegisterSingleton<PickersService>();
			container.RegisterSingleton<ProcessingService>();
			container.RegisterSingleton<SubtitlesService>();
			container.RegisterSingleton<EncodingWindowViewModel>();
			container.RegisterSingleton<StatusService>();
			container.RegisterSingleton<PreviewUpdateService>();
			container.RegisterSingleton<PreviewImageService>();

			Resolver.SetResolver(container);
			Container = container;
		}

		public static AnyContainerBase Container { get; set; }
	}
}
