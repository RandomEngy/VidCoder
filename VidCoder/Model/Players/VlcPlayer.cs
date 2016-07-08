using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Microsoft.Win32;

namespace VidCoder.Model
{
	public class VlcPlayer : VideoPlayerBase
	{
		public override string PlayerExecutable
		{
			get
			{
				var regKey = RegKey;

				return regKey?.GetValue(string.Empty) as string;
			}
		}

		public override void PlayTitleInternal(string executablePath, string discPath, int title)
		{
			string version = RegKey.GetValue("Version") as string;

			string arguments;
			if (version.StartsWith("1"))
			{
				arguments = "DVD://\"" + discPath + "\\\"@" + title;
			}
			else
			{
				arguments = "dvd:///\"" + discPath + "\\\"#" + title;
			}

			var process = new Process();
			process.StartInfo =
				new ProcessStartInfo
				{
					FileName = executablePath,
					Arguments = arguments
				};

			process.Start();
		}

		public override string Id
		{
			get
			{
				return "vlc";
			}
		}

		public override string Display
		{
			get
			{
				return "VLC";
			}
		}

		private static RegistryKey RegKey
		{
			get
			{
				RegistryKey key = RegKey64;

				if (key != null)
				{
					return key;
				}

				return RegKey32;
			}
		}

		private static RegistryKey RegKey32 => Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Wow6432Node\VideoLAN\VLC");

		private static RegistryKey RegKey64 => Registry.LocalMachine.OpenSubKey(@"SOFTWARE\VideoLAN\VLC");
	}
}
