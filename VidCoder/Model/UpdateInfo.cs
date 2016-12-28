using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VidCoder.Model
{
	public class UpdateInfo
	{
		public Version LatestVersion { get; set; }
		public string DownloadUrl { get; set; }
		public string ChangelogUrl { get; set; }
	}
}
