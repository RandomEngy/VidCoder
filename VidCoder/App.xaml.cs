using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Windows;
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
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application
	{
		protected override void OnStartup(StartupEventArgs e)
		{
#if !DEBUG
			this.DispatcherUnhandledException += this.OnDispatcherUnhandledException;
#endif
			base.OnStartup(e);

			try
			{
				// Upgrade from previous user settings.
				if (Settings.Default.ApplicationVersion != Utilities.CurrentVersion)
				{
					Settings.Default.Upgrade();
					Settings.Default.ApplicationVersion = Utilities.CurrentVersion;
					Settings.Default.Save();
				}
			}
			catch (ConfigurationErrorsException exception)
			{
				// This exception will happen if the user.config Settings file becomes corrupt.
				string userConfigFileName = ((ConfigurationErrorsException)exception.InnerException).Filename;

				// Need to show two message boxes since the first is dismissed by the splash screen due to a bug
				MessageBox.Show(string.Empty);
				MessageBoxResult result = MessageBox.Show(
					"Could not load a required user settings file; it may have become corrupt. Do you want to delete it? This will not affect your saved presets.",
					"Error loading settings",
					MessageBoxButton.YesNo,
					MessageBoxImage.Error);

				if (result == MessageBoxResult.Yes)
				{
					// Clear out any user.config files
					try
					{
						File.Delete(userConfigFileName);
					}
					catch (IOException)
					{
						MessageBox.Show("Unable to delete " + userConfigFileName);
					}

					// Need to relaunch the process to get the configuration system working again
					Process.Start(System.Reflection.Assembly.GetExecutingAssembly().Location);
					Environment.Exit(0);
				}
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
				var mainVM = new MainViewModel();
				WindowManager.OpenWindow(mainVM);
				mainVM.OnLoaded();
			}
		}

		private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
		{
			var exceptionDialog = new ExceptionDialog(e.Exception);
			exceptionDialog.ShowDialog();
		}
	}
}
