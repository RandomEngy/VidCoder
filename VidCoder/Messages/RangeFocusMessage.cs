
namespace VidCoder.Messages
{
	using HandBrake.Interop.Model;
	using Model;

	public class RangeFocusMessage
	{
		public VideoRangeType RangeType { get; set; }

		public bool GotFocus { get; set; }

		public bool Start { get; set; }
	}
}
