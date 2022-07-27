// Run elevated actions for VidCoder
// These LocalMachine and ClassesRoot registry entries are obsolete as of v8.4 Beta. (July 2022)
// The file is kept to be able to clean them up from earlier versions.
// Eventually we should be able to remove this executable entirely.

using Microsoft.Win32;
using VidCoderCommon;
using VidCoderCommon.Services;

bool isBeta = CommonUtilities.Beta;
string localAppFolder = CommonUtilities.LocalAppFolder;

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

SupportLogger? logger = null;

if (args.Length > 0)
{
	try
	{
		switch(args[0])
		{
			case "uninstall":
				logger = new SupportLogger("ElevatedUninstall");
				logger.Log("Running elevated jobs for uninstall...");
				RemoveRipDriveAction();
				RemoveFileAssociations();
				logger.Log("Jobs finished.");
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