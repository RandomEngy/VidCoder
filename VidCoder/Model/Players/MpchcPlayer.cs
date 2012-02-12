using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Microsoft.Win32;

namespace VidCoder.Model
{
	public class MpchcPlayer : IVideoPlayer
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
			string mpchcExePath = RegKey.GetValue("ExePath") as string;

			var process = new Process();
			process.StartInfo =
				new ProcessStartInfo
				{
					FileName = mpchcExePath,
					Arguments = "\"" + discPath + "\" /dvdpos " + title
				};

			process.Start();
		}

		public string Id
		{
			get
			{
				return "mpchc";
			}
		}

		public string Display
		{
			get
			{
				return "MPC-HC";
			}
		}

		private static RegistryKey RegKey
		{
			get
			{
				RegistryKey regKey;
				if (RegKeyNormal != null)
				{
					regKey = RegKeyNormal;
				}
				else
				{
					regKey = RegKeyWow;
				}

				return regKey;
			}
		}

		private static RegistryKey RegKeyNormal
		{
			get
			{
				return Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Gabest\Media Player Classic");
			}
		}

		private static RegistryKey RegKeyWow
		{
			get
			{
				return Registry.LocalMachine.OpenSubKey(Utilities.Wow64RegistryKey + @"\Gabest\Media Player Classic");
			}
		}
	}
}
