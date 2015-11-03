using System;
using VidCoderCommon.Model;

namespace VidCoder.Model
{
	public class RangeFocusEventArgs : EventArgs
	{
		public VideoRangeType RangeType { get; set; }

		public bool GotFocus { get; set; }

		public bool Start { get; set; }
	}
}
