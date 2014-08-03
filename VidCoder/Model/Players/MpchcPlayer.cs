using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Microsoft.Win32;

namespace VidCoder.Model
{
	using System.IO;

	public class MpchcPlayer : VideoPlayerBase
	{
		public override string PlayerExecutable
		{
			get
			{
				if (RegKey == null)
				{
					return null;
				}

				return RegKey.GetValue("ExePath") as string;
			}
		}

		public override void PlayTitleInternal(string executablePath, string discPath, int title)
		{
			var process = new Process();
			process.StartInfo =
				new ProcessStartInfo
				{
					FileName = executablePath,
					Arguments = "\"" + discPath + "\"\"" + " /dvdpos " + title
				};

			process.Start();
		}

		public override string Id
		{
			get
			{
				return "mpchc";
			}
		}

		public override string Display
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
				return Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Gabest\Media Player Classic");
			}
		}
	}
}
