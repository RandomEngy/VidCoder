using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using HandBrake.ApplicationServices.Interop;
using Newtonsoft.Json;
using VidCoder.Model;
using VidCoder.Properties;
using VidCoder.Services;
using VidCoder.Services.Windows;
using VidCoder.ViewModel;
using System.IO.Pipes;
using System.IO;
using System.ComponentModel;
using System.Runtime.InteropServices;
using VidCoder.Services.Notifications;
using VidCoder.View;
using VidCoderCommon;
using VidCoderCommon.Utilities;

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

		private static Mutex mutex;

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

#if !DEBUG
			this.DispatcherUnhandledException += this.OnDispatcherUnhandledException;
#endif

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

			// Takes about 50ms
			Config.EnsureInitialized(Database.Connection);

			var interfaceLanguageCode = Config.InterfaceLanguageCode;
			if (!string.IsNullOrWhiteSpace(interfaceLanguageCode))
			{
				var cultureInfo = new CultureInfo(interfaceLanguageCode);
				Thread.CurrentThread.CurrentCulture = cultureInfo;
				Thread.CurrentThread.CurrentUICulture = cultureInfo;
				CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
				CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;
			}

			if (Config.UseCustomPreviewFolder && FileUtilities.HasWriteAccessOnFolder(Config.PreviewOutputFolder))
			{
				Environment.SetEnvironmentVariable("TMP", Config.PreviewOutputFolder, EnvironmentVariableTarget.Process);
				FileUtilities.OverrideTempFolder = true;
				FileUtilities.TempFolderOverride = Config.PreviewOutputFolder;
			}

			var updater = Ioc.Get<IUpdater>();
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
			Ioc.Get<IWindowManager>().OpenWindow(mainVM);
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
