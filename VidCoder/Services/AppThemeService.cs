using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Management;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using VidCoder.Model;

namespace VidCoder.Services
{
	public class AppThemeService : IAppThemeService
	{
		private const string RegistryKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

		private const string RegistryValueName = "AppsUseLightTheme";

		private readonly ManagementEventWatcher watcher;

		public AppThemeService()
		{
			// Construct the query.
			var currentUser = WindowsIdentity.GetCurrent();
			if (currentUser.User == null)
			{
				return;
			}

			string query = string.Format(
				CultureInfo.InvariantCulture,
				@"SELECT * FROM RegistryValueChangeEvent WHERE Hive = 'HKEY_USERS' AND KeyPath = '{0}\\{1}' AND ValueName = '{2}'",
				currentUser.User.Value,
				RegistryKeyPath.Replace(@"\", @"\\"),
				RegistryValueName);

			var windowsThemeSubject = new BehaviorSubject<WindowsTheme>(GetWindowsTheme());

			try
			{
				this.watcher = new ManagementEventWatcher(query);
				this.watcher.EventArrived += (sender, args) =>
				{
					windowsThemeSubject.OnNext(GetWindowsTheme());
				};

				// Start listening for events
				this.watcher.Start();
			}
			catch (Exception)
			{
				// If we fail to set up the event watched, use Light theme
				windowsThemeSubject.OnNext(WindowsTheme.Light);
			}

			var highContrastSubject = new BehaviorSubject<bool>(SystemParameters.HighContrast);

			SystemParameters.StaticPropertyChanged += (sender, args) =>
			{
				if (args.PropertyName == nameof(SystemParameters.HighContrast))
				{
					highContrastSubject.OnNext(SystemParameters.HighContrast);
				}
			};

			this.AppThemeObservable = Observable.CombineLatest(
				windowsThemeSubject,
				highContrastSubject,
				Config.Observables.AppTheme,
				(windowsTheme, highContrast, appThemeChoiceString) =>
				{
					if (highContrast)
					{
						return AppTheme.HighContrast;
					}

					var appThemeChoice = (AppThemeChoice)Enum.Parse(typeof(AppThemeChoice), appThemeChoiceString);

					if (appThemeChoice == AppThemeChoice.Auto)
					{
						return windowsTheme == WindowsTheme.Light ? AppTheme.Light : AppTheme.Dark;
					}

					return appThemeChoice == AppThemeChoice.Light ? AppTheme.Light : AppTheme.Dark;
				});
		}

		public IObservable<AppTheme> AppThemeObservable { get; }

		private static WindowsTheme GetWindowsTheme()
		{
			using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath))
			{
				object registryValueObject = key?.GetValue(RegistryValueName);
				if (registryValueObject == null)
				{
					return WindowsTheme.Light;
				}

				int registryValue = (int)registryValueObject;

				return registryValue > 0 ? WindowsTheme.Light : WindowsTheme.Dark;
			}
		}

		public void Dispose()
		{
			try
			{
				this.watcher?.Dispose();
			}
			catch (COMException)
			{
				// Can happen if the user has already disconnected. Ignore and continue shutting down.
			}
		}

		private enum WindowsTheme
		{
			Light,
			Dark
		}
	}
}
