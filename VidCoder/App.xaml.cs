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
using Fluent;
using HandBrake.Interop.Interop;
using VidCoder.View;
using VidCoderCommon;
using VidCoderCommon.Utilities;

namespace VidCoder
{
	using System.Globalization;
	using System.Threading;
	using Automation;
	using Microsoft.AnyContainer;
	using Resources;

	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application
	{
		public static bool IsPrimaryInstance { get; private set; }

		private static Mutex mutex;

		private IAppThemeService appThemeService;

		private AppTheme currentTheme = AppTheme.Light;

		static App()
		{
			if (CommonUtilities.Beta)
			{
				mutex = new Mutex(true, "VidCoderBetaPrimaryInstanceMutex");
			}
			else
			{
				mutex = new Mutex(true, "VidCoderPrimaryInstanceMutex");
			}
		}

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

			JsonSettings.SetDefaultSerializationSettings();

			Ioc.SetUp();

			try
			{
				Database.Initialize();
			}
			catch (Exception)
			{
				Environment.Exit(0);
			}

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

			updater.HandlePendingUpdate();

			try
			{
				// Check if we're a secondary instance
				IsPrimaryInstance = mutex.WaitOne(TimeSpan.Zero, true);
			}
			catch (AbandonedMutexException)
			{
			}

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
					ThemeManager.ChangeTheme(this, fluentTheme + ".Cobalt");

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
				mainVM.HandlePaths(new List<string> { e.Args[0] });
			}

			if (!Utilities.IsPortable && IsPrimaryInstance)
			{
				AutomationHost.StartListening();
			}

			base.OnStartup(e);
		}

		protected override void OnExit(ExitEventArgs e)
		{
			if (IsPrimaryInstance)
			{
				try
				{
					mutex.ReleaseMutex();
				}
				catch (ApplicationException)
				{
				}
			}

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
