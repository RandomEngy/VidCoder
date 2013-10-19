using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using VidCoder.Model;
using VidCoder.Properties;
using VidCoder.Services;
using VidCoder.ViewModel;
using System.IO.Pipes;
using System.IO;
using System.ComponentModel;
using HandBrake.Interop;
using VidCoder.View;
using Microsoft.Practices.Unity;

namespace VidCoder
{
	using System.Globalization;
	using System.Threading;
	using Automation;
	using Resources;

	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application
	{
#if BETA
		static Mutex mutex = new Mutex(true, "VidCoderBetaSingleInstanceMutex");
#else
		static Mutex mutex = new Mutex(true, "VidCoderSingleInstanceMutex");
#endif

		protected override void OnStartup(StartupEventArgs e)
		{
#if !DEBUG
			this.DispatcherUnhandledException += this.OnDispatcherUnhandledException;
#endif
			base.OnStartup(e);

			OperatingSystem OS = Environment.OSVersion; 
			if ((OS.Platform == PlatformID.Win32NT) && (OS.Version.Major == 5 && OS.Version.Minor == 1)) 
			{ 
				MessageBox.Show(MiscRes.WinXPError, MiscRes.NoticeMessageTitle, MessageBoxButton.OK, MessageBoxImage.Warning); 
				Application.Current.Shutdown(); 
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

			// Takes about 50ms
			Config.Initialize(Database.Connection);

			if (!Config.MigratedConfigs && Directory.Exists(Utilities.LocalAppFolder))
			{
				// Upgrade configs from previous version before migrating
				try
				{
					if (Settings.Default.ApplicationVersion != Utilities.CurrentVersion)
					{
						Settings.Default.Upgrade();
						Settings.Default.ApplicationVersion = Utilities.CurrentVersion;

						if (Settings.Default.NativeLanguageCode != string.Empty)
						{
							var languageCode = Settings.Default.NativeLanguageCode;
							Settings.Default.NativeLanguageCode = string.Empty;

							if (languageCode != "und")
							{
								if (Settings.Default.DubAudio)
								{
									Settings.Default.AudioLanguageCode = languageCode;
									Settings.Default.AutoAudio = AutoAudioType.Language;
								}
								else
								{
									Settings.Default.SubtitleLanguageCode = languageCode;
									Settings.Default.AutoSubtitle = AutoSubtitleType.Language;
								}
							}
						}

						Settings.Default.Save();
					}

					ConfigMigration.MigrateConfigSettings();
				}
				catch (ConfigurationErrorsException)
				{
					// If we had problems loading the old config we can't recover.
					MessageBox.Show(MainRes.UserConfigCorrupted);

					Config.MigratedConfigs = true;
				}
			}

			var interfaceLanguageCode = Config.InterfaceLanguageCode;
			if (!string.IsNullOrWhiteSpace(interfaceLanguageCode))
			{
				var cultureInfo = new CultureInfo(interfaceLanguageCode);
				Thread.CurrentThread.CurrentCulture = cultureInfo;
				Thread.CurrentThread.CurrentUICulture = cultureInfo;
			}

			var updater = Unity.Container.Resolve<IUpdater>();
			bool updateSucceeded = updater.HandlePendingUpdate();

			if (updateSucceeded)
			{
				var updateSuccess = new UpdateSuccess();
				updateSuccess.Show();
			}
			else
			{
#if !DEBUG
				try
				{
					// Check if we're a duplicate instance
					if (!mutex.WaitOne(TimeSpan.Zero, true))
					{
						NativeMethods.PostMessage(
							(IntPtr)NativeMethods.HWND_BROADCAST,
							NativeMethods.WM_SHOWME,
							IntPtr.Zero,
							IntPtr.Zero);
						Environment.Exit(0);
					}
				}
				catch (AbandonedMutexException)
				{
				}
#endif

				this.GlobalInitialize();

				var mainVM = new MainViewModel();
				WindowManager.OpenWindow(mainVM);
				mainVM.OnLoaded();

				if (!Utilities.IsPortable)
				{
					AutomationHost.StartListening();
				}
			}
		}

		protected override void OnExit(ExitEventArgs e)
		{
			try
			{
				mutex.ReleaseMutex();
			}
			catch (ApplicationException)
			{
			}
		}

		private void GlobalInitialize()
		{
			HandBrakeUtils.SetDvdNav(Config.EnableLibDvdNav);
		}

		private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
		{
			var exceptionDialog = new ExceptionDialog(e.Exception);
			exceptionDialog.ShowDialog();
		}
	}
}
