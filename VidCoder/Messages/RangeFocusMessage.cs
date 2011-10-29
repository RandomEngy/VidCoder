using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HandBrake.Interop.Model;

namespace VidCoder.Messages
{
	public class RangeFocusMessage
	{
		public VideoRangeType RangeType { get; set; }

		public bool GotFocus { get; set; }

		public bool Start { get; set; }
	}
}
