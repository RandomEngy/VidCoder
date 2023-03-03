using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using VidCoder.Model;
using VidCoder.Services;
using VidCoder.Services.Windows;
using VidCoder.ViewModel;
using System.Windows.Media;
using HandBrake.Interop.Interop;
using VidCoder.View;
using VidCoderCommon;
using System.Threading;
using System.Runtime.InteropServices;
using VidCoder.Resources;
using VidCoderCommon.Services;
using System.Globalization;
using Microsoft.AnyContainer;
using ControlzEx.Theming;
using Microsoft.Toolkit.Uwp.Notifications;
using VidCoder.Automation;
using Squirrel;
using System.IO;
using VidCoderCommon.Model;

namespace VidCoder;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
	private Mutex mutex;

	private IAppThemeService appThemeService;

	private AppTheme currentTheme = AppTheme.Light;

	protected override void OnStartup(StartupEventArgs e)
	{
		if (e.Args.Contains("-ToastActivated"))
		{
			return;
		}

		// Only include Debugger.IsAttached check in Debug build, as antivirus programs find the check suspicious.
#if DEBUG
		bool setupExceptionHandler = !Debugger.IsAttached;
#else
		bool setupExceptionHandler = true;
#endif

		if (setupExceptionHandler)
		{
			this.DispatcherUnhandledException += this.OnDispatcherUnhandledException;
		}

		OperatingSystem OS = Environment.OSVersion;
		if (OS.Version.Major <= 5)
		{
			MessageBox.Show(MiscRes.UnsupportedOSError, MiscRes.NoticeMessageTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
			this.Shutdown();
			return;
		}

#if PSEUDOLOCALIZER_ENABLED
		Delay.PseudoLocalizer.Enable(typeof(CommonRes));
		Delay.PseudoLocalizer.Enable(typeof(MainRes));
		Delay.PseudoLocalizer.Enable(typeof(EnumsRes));
		Delay.PseudoLocalizer.Enable(typeof(EncodingRes));
		Delay.PseudoLocalizer.Enable(typeof(OptionsRes));
		Delay.PseudoLocalizer.Enable(typeof(PreviewRes));
		Delay.PseudoLocalizer.Enable(typeof(LogRes));
		Delay.PseudoLocalizer.Enable(typeof(SubtitleRes));
		Delay.PseudoLocalizer.Enable(typeof(QueueTitlesRes));
		Delay.PseudoLocalizer.Enable(typeof(ChapterMarkersRes));
		Delay.PseudoLocalizer.Enable(typeof(MiscRes));
#endif

		if (Utilities.InstallType == VidCoderInstallType.SquirrelInstaller)
		{
			// Let Squirrel set the AppUserModelID. This will allow any pinned shortcut on the taskbar to be updated properly.
			var updateManager = new UpdateManager(Utilities.SquirrelUpdateUrl);
			updateManager.SetProcessAppUserModelId();

			VidCoderInstall.HandleSquirrelEvents();
		}

		// If we get here we know we are actually trying to launch the app (and this isn't part of a Squirrel update process.
		// Enforce single-instance restrictions.
		string mutexName = CommonUtilities.Beta ? "VidCoderBetaInstanceMutex" : "VidCoderInstanceMutex";
		bool createdNew = true;

		try
		{
			this.mutex = new Mutex(true, mutexName, out createdNew);

			if (!createdNew)
			{
				var automationClient = new AutomationClient();
				automationClient.RunActionAsync(a => a.BringToForeground()).Wait();

				// BringToForeground succeeded. We can end this process and use the other one.
				Environment.Exit(0);
				return;
			}
		}
		catch (AbandonedMutexException)
		{
			// Another instance has crashed, abandoning its mutex. But that's fine.
		}
		catch
		{
			// If we get here then we've failed to activate the other instance. It might be in a bad state so we'll kill it and continue.
			Process[] processes = Process.GetProcesses();
			foreach (Process process in processes)
			{
				if (process.ProcessName == "VidCoder" && process.Id != Process.GetCurrentProcess().Id)
				{
					process.Kill();
				}
			}
		}

		var splashWindow = new SplashWindow();
		splashWindow.Show();

		// Run some global initialization for the HandBrake library. This needs to run before doing database upgrades, as we access HandBrake helper functions there.
		this.HandBrakeGlobalInitialize();

		Ioc.SetUp();

		Database.Initialize();

		Config.EnsureInitialized(Database.Connection);

		if (Utilities.InstallType == VidCoderInstallType.SquirrelInstaller && !string.IsNullOrEmpty(Config.UninstallerPath))
		{
			if (File.Exists(Config.UninstallerPath))
			{
				MessageBox.Show(MiscRes.OneTimeInstallerCleanup);

				var uninstallProcess = new Process();
				uninstallProcess.StartInfo = new ProcessStartInfo(Config.UninstallerPath, "/SILENT");
				uninstallProcess.Start();

				uninstallProcess.WaitForExit();
			}

			Config.UninstallerPath = string.Empty;
		}

		var interfaceLanguageCode = Config.InterfaceLanguageCode;
		if (!string.IsNullOrWhiteSpace(interfaceLanguageCode))
		{
			var cultureInfo = new CultureInfo(interfaceLanguageCode);
			Thread.CurrentThread.CurrentUICulture = cultureInfo;
			CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;

			// Don't set CurrentCulture as well; we don't need to override the number/date formatting as well.
		}

		if (Config.UseCustomPreviewFolder && FileUtilities.HasWriteAccessOnFolder(Config.PreviewOutputFolder))
		{
			Environment.SetEnvironmentVariable("TMP", Config.PreviewOutputFolder, EnvironmentVariableTarget.Process);
			FileUtilities.OverrideTempFolder = true;
			FileUtilities.TempFolderOverride = Config.PreviewOutputFolder;
		}

		this.appThemeService = StaticResolver.Resolve<IAppThemeService>();
		this.appThemeService.AppThemeObservable.Subscribe(appTheme =>
		{
			if (appTheme != this.currentTheme)
			{
				this.currentTheme = appTheme;
				this.ChangeTheme(new Uri($"/Themes/{appTheme}.xaml", UriKind.Relative));

				bool isDark = appTheme == AppTheme.Dark;

				string fluentTheme = isDark ? "Dark" : "Light";
				ThemeManager.Current.ChangeTheme(this, fluentTheme + ".Cobalt");

				Color ribbonTextColor = isDark ? Colors.White : Colors.Black;
				Color ribbonBackgroundColor = isDark ? Colors.Black : Colors.White;
				this.Resources["Fluent.Ribbon.Brushes.LabelTextBrush"] = new SolidColorBrush(ribbonTextColor);
				this.Resources["Fluent.Ribbon.Brushes.RibbonTabControl.Content.Background"] = new SolidColorBrush(ribbonBackgroundColor);
			}
		});

		var mainVM = new MainViewModel();
		this.MainWindow = StaticResolver.Resolve<IWindowManager>().OpenWindow(mainVM);
		mainVM.OnLoaded();

		if (e.Args.Length > 0)
		{
			if (!e.Args[0].StartsWith("-", StringComparison.Ordinal))
			{
				mainVM.HandlePaths(new List<string> { e.Args[0] });
			}
		}

		splashWindow.Close();

		if (Utilities.InstallType != VidCoderInstallType.Portable)
		{
			AutomationHost.StartListening();
		}
			
		ActivityService activityService = StaticResolver.Resolve<ActivityService>();
		this.Activated += (object sender, EventArgs e2) =>
		{
			activityService.ReportActivated();
		};
		this.Deactivated += (object sender, EventArgs e2) =>
		{
			activityService.ReportDeactivated();
		};

		if (Utilities.UwpApisAvailable)
		{
			ToastNotificationManagerCompat.OnActivated += this.ToastOnActivated;
		}

		base.OnStartup(e);
	}

	private void ToastOnActivated(ToastNotificationActivatedEventArgsCompat e)
	{
		StaticResolver.Resolve<Main>().EnsureVisible();
	}

	protected override void OnExit(ExitEventArgs e)
	{
		this.appThemeService?.Dispose();
	}

	private void HandBrakeGlobalInitialize()
	{
		HandBrakeUtils.EnsureGlobalInit(initNoHardwareMode: false);
		HandBrakeUtils.SetDvdNav(DatabaseConfig.Get<bool>("EnableLibDvdNav", true, Database.Connection)); // This runs early so we need to use the raw version to get the config value.
	}

	public void ChangeTheme(Uri uri)
	{
		this.Resources.MergedDictionaries[0].Source = uri;
	}

	private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
	{
		var exceptionDialog = new ExceptionDialog(e.Exception);
		exceptionDialog.ShowDialog();
	}
}
