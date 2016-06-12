using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VidCoder.Model
{
	public class UpdateInfo
	{
		public Version LatestVersion { get; set; }
		public string DownloadLocation { get; set; }
		public string ChangelogLocation { get; set; }
	}
}
