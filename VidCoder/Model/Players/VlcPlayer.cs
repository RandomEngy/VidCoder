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
				if (RegKey == null)
				{
					return null;
				}

				return RegKey.GetValue(string.Empty) as string;
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
				return Registry.LocalMachine.OpenSubKey(Utilities.Wow64RegistryKey + @"\VideoLAN\VLC");
			}
		}
	}
}
