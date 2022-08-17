using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VidCoder.Model;
using VidCoderCommon;
using VidCoderCommon.Model;

namespace VidCoder
{
	public static class RegistryUtilities
	{
		private static readonly bool IsBeta = CommonUtilities.Beta;
		private static readonly string LocalAppFolder = CommonUtilities.LocalAppFolder;
		private static readonly string WindowlessCliExePath = Path.Combine(LocalAppFolder, "VidCoderWindowlessCLI.exe");

		private const string EventHandlersKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\AutoplayHandlers\EventHandlers";

		private const string PlayBluRayOnArrivalKeyPath = $@"{EventHandlersKeyPath}\PlayBluRayOnArrival";
		private const string PlayDvdOnArrivalKeyPath = $@"{EventHandlersKeyPath}\PlayDVDMovieOnArrival";

		private static readonly string AppName = IsBeta ? "VidCoder Beta" : "VidCoder";
		private static readonly string AppNameNoSpace = IsBeta ? "VidCoderBeta" : "VidCoder";

		private static readonly string RipDriveActionName = AppNameNoSpace + "RipDriveOnArrival";
		private static readonly string RipDriveHandlerKeyPath = $@"Software\Microsoft\Windows\CurrentVersion\Explorer\AutoplayHandlers\Handlers\{RipDriveActionName}";

		private static readonly string RipDriveProgId = $"{AppNameNoSpace}.RipDrive";

		private const string ClassesKeyPath = @"Software\Classes";

		private static readonly string RipDriveClassesKeyPath = $@"{ClassesKeyPath}\{RipDriveProgId}";

		private const string VJPresetExtensionKeyPath = $@"{ClassesKeyPath}\.vjpreset";
		private const string VJQueueExtensionKeyPath = $@"{ClassesKeyPath}\.vjqueue";

		private const string PresetProgId = "VidCoderPreset";
		private const string QueueProgId = "VidCoderQueue";

		private const string PresetClassesKeyPath = $@"{ClassesKeyPath}\{PresetProgId}";
		private const string QueueClassesKeyPath = $@"{ClassesKeyPath}\{QueueProgId}";

		private const string AutoRunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
		private static readonly string AutoRunAppName = IsBeta ? "VidCoderBetaFileWatcher" : "VidCoderFileWatcher";

		public static void Install(IBasicLogger logger)
		{
			logger.Log("Installing registry keys...");
			AddRipDriveAction(logger);
			AddFileAssociations(logger);
			logger.Log("Finished installing registry keys.");
		}

		public static void Uninstall(IBasicLogger logger)
		{
			logger.Log("Uninstalling registry keys...");
			RemoveRipDriveAction(logger);
			RemoveFileAssociations(logger);
			RemoveFileWatcherAutoStart(logger);
			logger.Log("Finished uninstalling registry keys.");
		}

		public static bool AreRegKeysInstalled()
		{
			RegistryKey presetKey = Registry.CurrentUser.OpenSubKey(VJPresetExtensionKeyPath);
			if (presetKey == null)
			{
				return false;
			}
			else
			{
				presetKey.Close();
				return true;
			}
		}

		private static void AddRipDriveAction(IBasicLogger logger)
		{
			try
			{
				// 1: Register keys to hook into Blu-ray and DVD actions
				RegistryKey playBluRayKey = Registry.CurrentUser.CreateSubKey(PlayBluRayOnArrivalKeyPath, writable: true);
				if (playBluRayKey != null)
				{
					playBluRayKey.SetValue(RipDriveActionName, string.Empty);
					playBluRayKey.Close();
				}

				RegistryKey playDvdKey = Registry.CurrentUser.CreateSubKey(PlayDvdOnArrivalKeyPath, writable: true);
				if (playDvdKey != null)
				{
					playDvdKey.SetValue(RipDriveActionName, string.Empty);
					playDvdKey.Close();
				}

				// 2: Define what the rip drive action does
				string mainExePath = Path.Combine(CommonUtilities.LocalAppFolder, "VidCoder.exe");

				RegistryKey ripDriveActionKey = Registry.CurrentUser.CreateSubKey(RipDriveHandlerKeyPath);
				if (ripDriveActionKey != null)
				{
					ripDriveActionKey.SetValue("Action", "Rip");
					ripDriveActionKey.SetValue("DefaultIcon", $"{mainExePath},0");
					ripDriveActionKey.SetValue("InvokeProgId", RipDriveProgId);
					ripDriveActionKey.SetValue("InvokeVerb", "rip");
					ripDriveActionKey.SetValue("Provider", AppName);
					ripDriveActionKey.Close();
				}

				// 3: Define the program the rip drive action is invoking

				// Clean up old path if it's there.
				Registry.CurrentUser.DeleteSubKeyTree(RipDriveClassesKeyPath, throwOnMissingSubKey: false);

				RegistryKey progEntryKey = Registry.CurrentUser.CreateSubKey(@$"{RipDriveClassesKeyPath}\shell\rip\command");
				progEntryKey.SetValue(null, $"\"{WindowlessCliExePath}\" scan -s %L");
				progEntryKey.Close();
			}
			catch (Exception exception)
			{
				logger.Log(exception.ToString());
			}
		}

		private static void RemoveRipDriveAction(IBasicLogger logger)
		{
			try
			{
				RegistryKey playBluRayKey = Registry.CurrentUser.OpenSubKey(PlayBluRayOnArrivalKeyPath, writable: true);
				if (playBluRayKey != null)
				{
					playBluRayKey.DeleteValue(RipDriveActionName, throwOnMissingValue: false);
					playBluRayKey.Close();
				}

				RegistryKey playDvdKey = Registry.CurrentUser.OpenSubKey(PlayDvdOnArrivalKeyPath, writable: true);
				if (playDvdKey != null)
				{
					playDvdKey.DeleteValue(RipDriveActionName, throwOnMissingValue: false);
					playDvdKey.Close();
				}

				Registry.CurrentUser.DeleteSubKey(RipDriveHandlerKeyPath, throwOnMissingSubKey: false);

				Registry.CurrentUser.DeleteSubKeyTree(RipDriveClassesKeyPath, throwOnMissingSubKey: false);
			}
			catch (Exception exception)
			{
				logger.Log(exception.ToString());
			}
		}

		private static void AddFileAssociations(IBasicLogger logger)
		{
			try
			{
				// .vjpreset file association
				RegistryKey presetKey = Registry.CurrentUser.CreateSubKey(VJPresetExtensionKeyPath);
				presetKey.SetValue(null, PresetProgId);
				presetKey.Close();

				string presetIconPath = Path.Combine(CommonUtilities.LocalAppFolder, "VidCoderPreset.ico");

				RegistryKey presetIconKey = Registry.CurrentUser.CreateSubKey($@"{PresetClassesKeyPath}\DefaultIcon");
				presetIconKey.SetValue(null, $"\"{presetIconPath}\"");
				presetIconKey.Close();

				RegistryKey presetOpenCommandKey = Registry.CurrentUser.CreateSubKey($@"{PresetClassesKeyPath}\shell\open\command");
				presetOpenCommandKey.SetValue(null, $"\"{WindowlessCliExePath}\" importpreset \"%1\"");
				presetOpenCommandKey.Close();

				// .vjqueue file association
				RegistryKey queueKey = Registry.CurrentUser.CreateSubKey(VJQueueExtensionKeyPath);
				queueKey.SetValue(null, QueueProgId);
				queueKey.Close();

				string queueIconPath = Path.Combine(CommonUtilities.LocalAppFolder, "VidCoderQueue.ico");

				RegistryKey queueIconKey = Registry.CurrentUser.CreateSubKey($@"{QueueClassesKeyPath}\DefaultIcon");
				queueIconKey.SetValue(null, $"\"{queueIconPath}\"");
				queueIconKey.Close();

				RegistryKey queueOpenCommandKey = Registry.CurrentUser.CreateSubKey($@"{QueueClassesKeyPath}\shell\open\command");
				queueOpenCommandKey.SetValue(null, $"\"{WindowlessCliExePath}\" importqueue \"%1\"");
				queueOpenCommandKey.Close();

			}
			catch (Exception exception)
			{
				logger.Log(exception.ToString());
			}
		}

		private static void RemoveFileAssociations(IBasicLogger logger)
		{
			try
			{
				Registry.CurrentUser.DeleteSubKey(VJPresetExtensionKeyPath);
				Registry.CurrentUser.DeleteSubKey(VJQueueExtensionKeyPath);

				Registry.CurrentUser.DeleteSubKeyTree(PresetClassesKeyPath);
				Registry.CurrentUser.DeleteSubKeyTree(QueueClassesKeyPath);
			}
			catch (Exception exception)
			{
				logger.Log(exception.ToString());
			}
		}

		public static bool IsFileWatcherAutoStart()
		{
			RegistryKey autoRunKey = Registry.CurrentUser.CreateSubKey(AutoRunKeyPath);
			object value = autoRunKey.GetValue(AutoRunAppName);
			autoRunKey.Close();

			return value != null;
		}

		public static void SetFileWatcherAutoStart(IBasicLogger logger)
		{
			try
			{
				if (!CommonUtilities.DebugMode)
				{
					string fileWatcherPath;
					if (Utilities.InstallType == VidCoderInstallType.SquirrelInstaller)
					{
						// For Squirrel installer, use the "stable" path in the local app data folder that is not specific to a version.
						fileWatcherPath = Path.Combine(CommonUtilities.LocalAppFolder, Utilities.FileWatcherExecutableName + ".exe");
					}
					else
					{
						// Otherwise (Zip install) use the version-specific path.
						fileWatcherPath = Utilities.FileWatcherFullPath;
					}

					RegistryKey autoRunKey = Registry.CurrentUser.CreateSubKey(AutoRunKeyPath);
					autoRunKey.SetValue(AutoRunAppName, fileWatcherPath);
					autoRunKey.Close();
				}
			}
			catch (Exception exception)
			{
				logger.Log(exception.ToString());
			}
		}

		public static void RemoveFileWatcherAutoStart(IBasicLogger logger)
		{
			try
			{
				if (!CommonUtilities.DebugMode)
				{
					RegistryKey autoRunKey = Registry.CurrentUser.CreateSubKey(AutoRunKeyPath);
					autoRunKey.DeleteValue(AutoRunAppName);
					autoRunKey.Close();
				}
			}
			catch (Exception exception)
			{
				logger.Log(exception.ToString());
			}
		}
	}
}
