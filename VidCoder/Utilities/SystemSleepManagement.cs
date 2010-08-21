using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using VidCoder.Services;

namespace VidCoder
{
	[Flags]
	public enum EXECUTION_STATE : uint
	{
		ES_SYSTEM_REQUIRED = 0x00000001,
		ES_DISPLAY_REQUIRED = 0x00000002,
		// Legacy flag, should not be used.
		// ES_USER_PRESENT   = 0x00000004,
		ES_CONTINUOUS = 0x80000000,
	}

	public static class SystemSleepManagement
	{
		[DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
		private static extern EXECUTION_STATE SetThreadExecutionState(EXECUTION_STATE esFlags);

		public static void PreventSleep()
		{
			DispatchService.BeginInvoke(() =>
			{
				SetThreadExecutionState(EXECUTION_STATE.ES_CONTINUOUS | EXECUTION_STATE.ES_SYSTEM_REQUIRED);
			});
		}

		public static void AllowSleep()
		{
			DispatchService.BeginInvoke(() =>
			{
				SetThreadExecutionState(EXECUTION_STATE.ES_CONTINUOUS);
			});
		}
	}
}
