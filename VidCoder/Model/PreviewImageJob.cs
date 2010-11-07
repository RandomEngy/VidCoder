using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HandBrake.Interop;

namespace VidCoder.Model
{
	public class PreviewImageJob
	{
		public HandBrakeInstance ScanInstance { get; set; }

		public int UpdateVersion { get; set; }

		public int PreviewNumber { get; set; }

		public EncodeJob EncodeJob { get; set; }
	}
}
