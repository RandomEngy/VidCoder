using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VidCoder.Model
{
	public enum UpdateState
	{
		NotStarted,
		DownloadingInfo,
		DownloadingInstaller,
		UpToDate,
		InstallerReady,
		Failed,
        NotSupported32BitOS
	}
}
