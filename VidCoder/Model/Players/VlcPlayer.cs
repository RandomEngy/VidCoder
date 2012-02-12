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

			var process = new Process();
			process.StartInfo =
				new ProcessStartInfo
			    {
					FileName = vlcExePath,
					Arguments = "DVD://\"" + discPath + "\"@" + title
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
