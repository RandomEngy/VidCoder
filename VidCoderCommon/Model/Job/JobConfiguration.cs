using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VidCoderCommon.Model.Job
{
	public class JobConfiguration
	{
		public bool EnableQuickSyncDecoding { get; set; }
		public bool UseQsvDecodeForNonQsvEncodes { get; set; }
		public bool EnableQuickSyncHyperEncode { get; set; }
		public bool EnableNVDec { get; set; }
		public bool EnableDirectXDecoding { get; set; }
	}
}
