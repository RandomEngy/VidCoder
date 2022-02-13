using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VidCoder.Model
{
	public enum UpdateState
	{
		NotStarted,
		Checking,
		UpToDate,
		UpdateReady,
		Failed
	}
}
