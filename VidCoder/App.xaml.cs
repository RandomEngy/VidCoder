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
		public static bool IsPrimaryInstance { get; private set; }

#if BETA
		static Mutex mutex = new Mutex(true, "VidCoderBetaPrimaryInstanceMutex");
#else
		static Mutex mutex = new Mutex(true, "VidCoderPrimaryInstanceMutex");
#endif

		protected override void OnStartup(StartupEventArgs e)
		{
#if !DEBUG
			this.DispatcherUnhandledException += this.OnDispatcherUnhandledException;
#endif
			base.OnStartup(e);

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
									Settings.Default.AutoAudio = AudioSelectionMode.Language;
								}
								else
								{
									Settings.Default.SubtitleLanguageCode = languageCode;
									Settings.Default.AutoSubtitle = SubtitleSelectionMode.Language;
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

			var updater = Ioc.Container.GetInstance<IUpdater>();
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

			var mainVM = new MainViewModel();
			WindowManager.OpenWindow(mainVM);
			mainVM.OnLoaded();

			if (!Utilities.IsPortable && IsPrimaryInstance)
			{
				AutomationHost.StartListening();
			}
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
