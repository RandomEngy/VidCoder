using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VidCoderCommon.Model
{
	public enum WatchedFileStatusReason
	{
		None,
		SkippedNoTitlesPicked,
		SkippedOutputFile,
		CanceledEncodeStopped,
		CanceledScan,
		CanceledRemovedFromQueue
	}
}
