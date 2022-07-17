// Run setup actions for VidCoder
using Microsoft.Win32;
using System.Diagnostics;
using System.Reflection;
using VidCoderCommon;
using VidCoderCommon.Services;

bool isBeta = CommonUtilities.Beta;
string localAppFolder = CommonUtilities.LocalAppFolder;
string windowlessCliExePath = Path.Combine(localAppFolder, "VidCoderWindowlessCLI.exe");

string eventHandlersKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\AutoplayHandlers\EventHandlers";

string playBluRayOnArrivalKeyPath = @$"{eventHandlersKeyPath}\PlayBluRayOnArrival";
string playDvdOnArrivalKeyPath = @$"{eventHandlersKeyPath}\PlayDVDMovieOnArrival";

string appName = isBeta ? "VidCoder Beta" : "VidCoder";
string appNameNoSpace = isBeta ? "VidCoderBeta" : "VidCoder";

string ripDriveActionName = appNameNoSpace + "RipDriveOnArrival";
string ripDriveKeyPath = @$"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\AutoplayHandlers\Handlers\{ripDriveActionName}";

string ripDriveProgId = $"{appNameNoSpace}.RipDrive";

string vjPresetKeyPath = ".vjpreset";
string vjQueueKeyPath = ".vjqueue";

string presetProgId = "VidCoderPreset";
string queueProgId = "VidCoderQueue";

string windowsServiceName = isBeta ? "VidCoder Beta File Watcher" : "VidCoder File Watcher";

SetupLogger? logger = null;

if (args.Length > 0)
{
	try
	{
		switch(args[0])
		{
			case "install":
				logger = new SetupLogger("ElevatedInstall");
				logger.Log("Running elevated jobs for install...");
				AddRipDriveAction();
				AddFileAssociations();
				logger.Log("Jobs finished.");
				break;
			case "uninstall":
				logger = new SetupLogger("ElevatedUninstall");
				logger.Log("Running elevated jobs for uninstall...");
				RemoveRipDriveAction();
				RemoveFileAssociations();
				RemoveFileWatcherService();
				logger.Log("Jobs finished.");
				break;
			case "activateFileWatcher":
				AddFileWatcherService();
				break;
			case "deactivateFileWatcher":
				RemoveFileWatcherService();
				break;
			default:
				break;
		}
	}
	finally
	{
		logger?.Close();
	}
}

void AddRipDriveAction()
{
	try
	{
		// 1: Register keys to hook into Blu-ray and DVD actions
		RegistryKey? playBluRayKey = Registry.LocalMachine.OpenSubKey(playBluRayOnArrivalKeyPath, writable: true);
		if (playBluRayKey != null)
		{
			playBluRayKey.SetValue(ripDriveActionName, string.Empty);
			playBluRayKey.Close();
		}

		RegistryKey? playDvdKey = Registry.LocalMachine.OpenSubKey(playDvdOnArrivalKeyPath, writable: true);
		if (playDvdKey != null)
		{
			playDvdKey.SetValue(ripDriveActionName, string.Empty);
			playDvdKey.Close();
		}

		// 2: Define what the rip drive action does
		string? programFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
		if (programFolder == null)
		{
			throw new Exception("Could not get executing directory.");
		}

		string mainExePath = Path.Combine(programFolder, "VidCoder.exe");

		RegistryKey ripDriveActionKey = Registry.LocalMachine.CreateSubKey(ripDriveKeyPath);
		if (ripDriveActionKey != null)
		{
			ripDriveActionKey.SetValue("Action", "Rip");
			ripDriveActionKey.SetValue("DefaultIcon", $"{mainExePath},0");
			ripDriveActionKey.SetValue("InvokeProgId", ripDriveProgId);
			ripDriveActionKey.SetValue("InvokeVerb", "rip");
			ripDriveActionKey.SetValue("Provider", appName);
			ripDriveActionKey.Close();
		}

		// 3: Define the program the rip drive action is invoking

		// Clean up old path if it's there.
		Registry.ClassesRoot.DeleteSubKeyTree(ripDriveProgId, throwOnMissingSubKey: false);

		RegistryKey progEntryKey = Registry.ClassesRoot.CreateSubKey(@$"{ripDriveProgId}\shell\rip\command");
		progEntryKey.SetValue(null, $"\"{windowlessCliExePath}\" scan -s %L");
		progEntryKey.Close();
	}
	catch (Exception exception)
	{
		logger.Log(exception.ToString());
	}
}

void RemoveRipDriveAction()
{
	try
	{
		RegistryKey? playBluRayKey = Registry.LocalMachine.OpenSubKey(playBluRayOnArrivalKeyPath, writable: true);
		if (playBluRayKey != null)
		{
			playBluRayKey.DeleteValue(ripDriveActionName, throwOnMissingValue: false);
			playBluRayKey.Close();
		}

		RegistryKey? playDvdKey = Registry.LocalMachine.OpenSubKey(playDvdOnArrivalKeyPath, writable: true);
		if (playDvdKey != null)
		{
			playDvdKey.DeleteValue(ripDriveActionName, throwOnMissingValue: false);
			playDvdKey.Close();
		}

		Registry.LocalMachine.DeleteSubKey(ripDriveKeyPath, throwOnMissingSubKey: false);

		Registry.ClassesRoot.DeleteSubKeyTree(ripDriveProgId, throwOnMissingSubKey: false);
	}
	catch (Exception exception)
	{
		logger.Log(exception.ToString());
	}
}

void AddFileAssociations()
{
	try
	{
		// .vjpreset file association
		RegistryKey presetKey = Registry.ClassesRoot.CreateSubKey(vjPresetKeyPath);
		presetKey.SetValue(null, presetProgId);
		presetKey.Close();

		string presetIconPath = Path.Combine(CommonUtilities.LocalAppFolder, "VidCoderPreset.ico");

		RegistryKey presetIconKey = Registry.ClassesRoot.CreateSubKey($@"{presetProgId}\DefaultIcon");
		presetIconKey.SetValue(null, $"\"{presetIconPath}\"");
		presetIconKey.Close();

		RegistryKey presetOpenCommandKey = Registry.ClassesRoot.CreateSubKey($@"{presetProgId}\shell\open\command");
		presetOpenCommandKey.SetValue(null, $"\"{windowlessCliExePath}\" importpreset \"%1\"");
		presetOpenCommandKey.Close();

		// .vjqueue file association
		RegistryKey queueKey = Registry.ClassesRoot.CreateSubKey(vjQueueKeyPath);
		queueKey.SetValue(null, queueProgId);
		queueKey.Close();

		string queueIconPath = Path.Combine(CommonUtilities.LocalAppFolder, "VidCoderQueue.ico");

		RegistryKey queueIconKey = Registry.ClassesRoot.CreateSubKey($@"{queueProgId}\DefaultIcon");
		queueIconKey.SetValue(null, $"\"{queueIconPath}\"");
		queueIconKey.Close();

		RegistryKey queueOpenCommandKey = Registry.ClassesRoot.CreateSubKey($@"{queueProgId}\shell\open\command");
		queueOpenCommandKey.SetValue(null, $"\"{windowlessCliExePath}\" importqueue \"%1\"");
		queueOpenCommandKey.Close();

	}
	catch (Exception exception)
	{
		logger.Log(exception.ToString());
	}
}

void RemoveFileAssociations()
{
	try
	{
		Registry.ClassesRoot.DeleteSubKey(vjPresetKeyPath);
		Registry.ClassesRoot.DeleteSubKey(vjQueueKeyPath);

		Registry.ClassesRoot.DeleteSubKeyTree(presetProgId);
		Registry.ClassesRoot.DeleteSubKeyTree(queueProgId);
	}
	catch (Exception exception)
	{
		logger.Log(exception.ToString());
	}
}

void RunSCCommand(string arguments)
{
	var startInfo = new ProcessStartInfo("sc.exe", arguments);
	startInfo.CreateNoWindow = true;

	Process process = Process.Start(startInfo);
	process.WaitForExit();
}

void AddFileWatcherService()
{
	string serviceExePath = Path.Combine(CommonUtilities.ProgramFolder, "VidCoderFileWatcher.exe");

	RunSCCommand($"create \"{windowsServiceName}\" binpath=\"{serviceExePath}\" start= auto");
	RunSCCommand($"description \"{windowsServiceName}\" \"Watches folders for new video files and encodes them with VidCoder.\"");
	RunSCCommand($"start \"{windowsServiceName}\"");
}

void RemoveFileWatcherService()
{
	RunSCCommand($"stop \"{windowsServiceName}\"");
	RunSCCommand($"delete \"{windowsServiceName}\"");
}