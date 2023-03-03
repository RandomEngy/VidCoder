using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Microsoft.Win32;

namespace VidCoder.Model;

using System.IO;

public class MpchcPlayer : VideoPlayerBase
{
	public override string PlayerExecutable
	{
		get
		{
			using (RegistryKey key = NewRegKey ?? OldRegKey)
			{
				return key?.GetValue("ExePath") as string;
			}
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

	public override string Id => "mpchc";

	public override string Display => "MPC-HC";

	private static RegistryKey OldRegKey => Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Gabest\Media Player Classic");

	private static RegistryKey NewRegKey => Registry.CurrentUser.OpenSubKey(@"SOFTWARE\MPC-HC\MPC-HC");
}
