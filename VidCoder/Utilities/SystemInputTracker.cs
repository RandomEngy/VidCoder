using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace VidCoder;

public static class SystemInputTracker
{
	[DllImport("user32.dll")]
	private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

	public static TimeSpan GetTimeSinceLastInput()
	{
		int systemUptime = Environment.TickCount,
			idleTicks = 0;

		LASTINPUTINFO lastInputInfo = new();
		lastInputInfo.cbSize = (uint)Marshal.SizeOf(lastInputInfo);
		lastInputInfo.dwTime = 0;

		if (GetLastInputInfo(ref lastInputInfo))
		{
			idleTicks = systemUptime - (int)lastInputInfo.dwTime;
		}

		return new TimeSpan(0, 0, 0, 0, idleTicks);
	}

	private struct LASTINPUTINFO
	{
		public uint cbSize;
		public uint dwTime;
	}
}
