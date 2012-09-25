
namespace VidCoder.Messages
{
	using Model;

	public class RangeFocusMessage
	{
		public VideoRangeTypeCombo RangeType { get; set; }

		public bool GotFocus { get; set; }

		public bool Start { get; set; }
	}
}
