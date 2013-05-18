using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Microsoft.Win32;

namespace VidCoder.Model
{
	public class VlcPlayer : IVideoPlayer
	{
		public bool Installed
		{
			get
			{
				return RegKey != null;
			}
		}

		public void PlayTitle(string discPath, int title)
		{
			string vlcExePath = RegKey.GetValue(string.Empty) as string;
			string version = RegKey.GetValue("Version") as string;

			if (!discPath.EndsWith(@"\", StringComparison.Ordinal))
			{
				discPath += @"\";
			}

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
					FileName = vlcExePath,
					Arguments = arguments
				};

			process.Start();

		}

		public string Id
		{
			get
			{
				return "vlc";
			}
		}

		public string Display
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
