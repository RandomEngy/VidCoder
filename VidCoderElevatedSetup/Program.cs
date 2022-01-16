// Run setup actions for VidCoder
using Microsoft.Win32;
using System.Reflection;
using VidCoderCommon;

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

if (args.Length > 0)
{

	if (args[0] == "install")
	{
		Console.WriteLine("Running elevated jobs for install...");
		AddRipDriveAction();
	}
	else if (args[0] == "uninstall")
	{
		Console.WriteLine("Running elevated jobs for uninstall...");
		RemoveRipDriveAction();
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
		string programFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
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
		string windowlessCliExePath = Path.Combine(localAppFolder, "VidCoderWindowlessCLI.exe");

		// Clean up old path if it's there.
		Registry.ClassesRoot.DeleteSubKey(ripDriveProgId, throwOnMissingSubKey: false);

		RegistryKey progEntryKey = Registry.ClassesRoot.CreateSubKey(@$"{ripDriveProgId}\shell\rip\command");
		progEntryKey.SetValue(null, $"\"{windowlessCliExePath}\" scan -s %L");
		progEntryKey.Close();
	}
	catch (Exception exception)
	{
		Console.WriteLine(exception.ToString());
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
		Console.WriteLine(exception.ToString());
	}
}