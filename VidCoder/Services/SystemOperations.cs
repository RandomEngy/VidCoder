using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using VidCoder.Model;
using System.Windows.Forms;
using Microsoft.AnyContainer;


namespace VidCoder.Services;

public class SystemOperations : ISystemOperations
{
	private static IAppLogger logger = StaticResolver.Resolve<IAppLogger>();

	[Flags]
	private enum ExitWindows : uint
	{
		// ONE of the following five:
		LogOff = 0x00,
		ShutDown = 0x01,
		Reboot = 0x02,
		PowerOff = 0x08,
		RestartApps = 0x40,
		// plus AT MOST ONE of the following two:
		Force = 0x04,
		ForceIfHung = 0x10,
	}

	[Flags]
	private enum ShutdownReason : uint
	{
		MajorApplication = 0x00040000,
		MajorHardware = 0x00010000,
		MajorLegacyApi = 0x00070000,
		MajorOperatingSystem = 0x00020000,
		MajorOther = 0x00000000,
		MajorPower = 0x00060000,
		MajorSoftware = 0x00030000,
		MajorSystem = 0x00050000,

		MinorBlueScreen = 0x0000000F,
		MinorCordUnplugged = 0x0000000b,
		MinorDisk = 0x00000007,
		MinorEnvironment = 0x0000000c,
		MinorHardwareDriver = 0x0000000d,
		MinorHotfix = 0x00000011,
		MinorHung = 0x00000005,
		MinorInstallation = 0x00000002,
		MinorMaintenance = 0x00000001,
		MinorMMC = 0x00000019,
		MinorNetworkConnectivity = 0x00000014,
		MinorNetworkCard = 0x00000009,
		MinorOther = 0x00000000,
		MinorOtherDriver = 0x0000000e,
		MinorPowerSupply = 0x0000000a,
		MinorProcessor = 0x00000008,
		MinorReconfig = 0x00000004,
		MinorSecurity = 0x00000013,
		MinorSecurityFix = 0x00000012,
		MinorSecurityFixUninstall = 0x00000018,
		MinorServicePack = 0x00000010,
		MinorServicePackUninstall = 0x00000016,
		MinorTermSrv = 0x00000020,
		MinorUnstable = 0x00000006,
		MinorUpgrade = 0x00000003,
		MinorWMI = 0x00000015,

		FlagUserDefined = 0x40000000,
		FlagPlanned = 0x80000000
	}

	[DllImport("user32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool ExitWindowsEx(ExitWindows uFlags, ShutdownReason dwReason);

	[DllImport("advapi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
	private static extern bool InitiateSystemShutdownEx(
		string lpMachineName,
		string lpMessage,
		uint dwTimeout,
		bool bForceAppsClosed,
		bool bRebootAfterShutdown,
		ShutdownReason dwReason);

	[DllImport("winmm.dll")]
	static extern Int32 mciSendString(String command, StringBuilder buffer, Int32 bufferSize, IntPtr hwndCallback);

	public void Sleep()
	{
		logger.Log("Sleeping.");
		Application.SetSuspendState(PowerState.Suspend, true, true);
	}

	public void LogOff()
	{
		logger.Log("Logging off.");
		ExitWindowsEx(ExitWindows.LogOff, ShutdownReason.MajorOther | ShutdownReason.MinorOther | ShutdownReason.FlagPlanned);
	}

	public void ShutDown()
	{
		logger.Log("Shutting down.");
		InitiateSystemShutdownEx(
			null, 
			null, 
			0,
			true, 
			false, 
			ShutdownReason.MajorOther | ShutdownReason.MinorOther | ShutdownReason.FlagPlanned);
	}

	public void Restart()
	{
		logger.Log("Restarting.");
		ExitWindowsEx(ExitWindows.Reboot, ShutdownReason.MajorOther | ShutdownReason.MinorOther | ShutdownReason.FlagPlanned);
	}

	public void Hibernate()
	{
		logger.Log("Hibernating.");
		Application.SetSuspendState(PowerState.Hibernate, true, true);
	}

	public void Eject(string driveLetter)
	{
		mciSendString("open " + driveLetter + ": type CDAudio alias drive" + driveLetter, null, 0, IntPtr.Zero);
		mciSendString("set drive" + driveLetter + " door open", null, 0, IntPtr.Zero);
	}
}
