using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace VidCoder
{
	public static class SystemPowerStatus
	{
		public static PowerState GetPowerState()
		{
			PowerState state = new PowerState();
			if (GetSystemPowerStatusRef(state))
			{
				return state;
			}

			return null;
		}

		[DllImport("Kernel32", EntryPoint = "GetSystemPowerStatus")]
		private static extern bool GetSystemPowerStatusRef(PowerState sps);

		[StructLayout(LayoutKind.Sequential)]
		public class PowerState
		{
			public ACLineStatus ACLineStatus;
			public BatteryFlag BatteryFlag;
			public Byte BatteryLifePercent;
			public Byte SystemStatusFlag;
			public Int32 BatteryLifeTime;
			public Int32 BatteryFullLifeTime;

			public static PowerState GetPowerState()
			{
				PowerState state = new PowerState();
				if (GetSystemPowerStatusRef(state))
				{
					return state;
				}

				return null;
			}

			[DllImport("Kernel32", EntryPoint = "GetSystemPowerStatus")]
			private static extern bool GetSystemPowerStatusRef(PowerState sps);
		}

		public enum ACLineStatus : byte
		{
			Offline = 0,
			Online = 1,
			Unknown = 255
		}

		public enum BatteryFlag : byte
		{
			High = 1,
			Low = 2,
			Critical = 4,
			Charging = 8,
			NoSystemBattery = 128,
			Unknown = 255
		}
	}
}
