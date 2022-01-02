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
using VidCoderCommon.Utilities;

namespace VidCoder
{
	using System.Globalization;
	using System.Runtime.InteropServices;
	using System.Threading;
	using Automation;
	using ControlzEx.Theming;
	using Microsoft.AnyContainer;
	using Microsoft.Toolkit.Uwp.Notifications;
	using Resources;
	using Squirrel;
	using VidCoder.Services.Notifications;
	using VidCoderCommon.Services;
	using Windows.Foundation.Metadata;

	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application
	{
		private Mutex mutex;

		private IAppThemeService appThemeService;

		private AppTheme currentTheme = AppTheme.Light;

		[DllImport("shell32.dll", SetLastError = true)]
		static extern void SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] string AppID);

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

			// Set the AppUserModelID to match the shortcut that Squirrel is creating. This will allow any pinned shortcut on the taskbar to be updated.
			SetCurrentProcessExplicitAppUserModelID("com.squirrel.VidCoder-Beta.VidCoder");

			SquirrelAwareApp.HandleEvents(onInitialInstall: OnInitialInstall, onAppUninstall: OnAppUninstall);

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

			Ioc.SetUp();

			Database.Initialize();

			Config.EnsureInitialized(Database.Connection);

			var interfaceLanguageCode = Config.InterfaceLanguageCode;
			if (!string.IsNullOrWhiteSpace(interfaceLanguageCode))
			{
				var cultureInfo = new CultureInfo(interfaceLanguageCode);
				Thread.CurrentThread.CurrentUICulture = cultureInfo;
				CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;

				// Don't set CurrentCulture as well; we don't need to override the number/date formatting as well.
			}

			Stopwatch sw = Stopwatch.StartNew();

			if (Config.UseCustomPreviewFolder && FileUtilities.HasWriteAccessOnFolder(Config.PreviewOutputFolder))
			{
				Environment.SetEnvironmentVariable("TMP", Config.PreviewOutputFolder, EnvironmentVariableTarget.Process);
				FileUtilities.OverrideTempFolder = true;
				FileUtilities.TempFolderOverride = Config.PreviewOutputFolder;
			}

			var updater = StaticResolver.Resolve<IUpdater>();

			sw.Stop();
			System.Diagnostics.Debug.WriteLine("Startup time: " + sw.Elapsed);

			this.GlobalInitialize();

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
			StaticResolver.Resolve<IWindowManager>().OpenWindow(mainVM);
			mainVM.OnLoaded();

			if (e.Args.Length > 0)
			{
				if (!e.Args[0].StartsWith("-", StringComparison.Ordinal))
				{
					mainVM.HandlePaths(new List<string> { e.Args[0] });
				}
			}

			splashWindow.Close();

			AutomationHost.StartListening();
				
			ActivityService activityService = StaticResolver.Resolve<ActivityService>();
			this.Activated += (object sender, EventArgs e2) =>
			{
				activityService.ReportActivated();
			};
			this.Deactivated += (object sender, EventArgs e2) =>
			{
				activityService.ReportDeactivated();
			};

			//if (ApiInformation.IsTypePresent("Windows.UI.Notifications.Notification"))
			if (Utilities.UwpApisAvailable)
			{
				ToastNotificationManagerCompat.OnActivated += this.ToastOnActivated;
			}

			base.OnStartup(e);
		}

		private static void OnInitialInstall(Version version)
		{
			using var mgr = new UpdateManager(Utilities.SquirrelUpdateUrl);
			mgr.CreateUninstallerRegistryEntry();
			mgr.CreateShortcutForThisExe(ShortcutLocation.StartMenu | ShortcutLocation.Desktop);
		}

		private static void OnAppUninstall(Version version)
		{
			using var mgr = new UpdateManager(Utilities.SquirrelUpdateUrl);
			mgr.RemoveUninstallerRegistryEntry();
			mgr.RemoveShortcutForThisExe(ShortcutLocation.StartMenu | ShortcutLocation.Desktop);
		}

		private void ToastOnActivated(ToastNotificationActivatedEventArgsCompat e)
		{
			StaticResolver.Resolve<Main>().EnsureVisible();
		}

		protected override void OnExit(ExitEventArgs e)
		{
			this.appThemeService?.Dispose();
		}

		private void GlobalInitialize()
		{
			HandBrakeUtils.EnsureGlobalInit(initNoHardwareMode: false);
			HandBrakeUtils.SetDvdNav(Config.EnableLibDvdNav);
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
}
