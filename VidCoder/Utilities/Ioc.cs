using System;
using Microsoft.AnyContainer;
using Microsoft.AnyContainer.DryIoc;
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
			var container = new DryIocAnyContainer();
			container.RegisterSingleton<IDriveService, DriveService>();
			container.RegisterSingleton<IAppThemeService, AppThemeService>();

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
			container.RegisterSingleton<IAppLogger, MainAppLogger>();
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
			container.RegisterSingleton<StatusService>();
			container.RegisterSingleton<PreviewUpdateService>();
			container.RegisterSingleton<PreviewImageService>();
			container.RegisterSingleton<ClipboardService>();
			container.RegisterSingleton<TrayService>();
			container.RegisterSingleton<AppLoggerFactory>();

			container.RegisterSingleton<EncodingWindowViewModel>();

			container.RegisterTransient<PreviewWindowViewModel>();
			container.RegisterTransient<PickerWindowViewModel>();
			container.RegisterTransient<OptionsDialogViewModel>();
			container.RegisterTransient<LogWindowViewModel>();
			container.RegisterTransient<QueueTitlesWindowViewModel>();
			container.RegisterTransient<EncodeDetailsWindowViewModel>();

			StaticResolver.SetResolver(container);
			Container = container;
		}

		public static AnyContainerBase Container { get; set; }
	}
}
