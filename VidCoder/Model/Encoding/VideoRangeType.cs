namespace VidCoder.Model.Encoding
{
	public enum VideoRangeType
	{
		/// <summary>
		/// The entire title.
		/// </summary>
		All,

		/// <summary>
		/// A chapter range.
		/// </summary>
		Chapters, 

		/// <summary>
		/// A timespan range in seconds.
		/// </summary>
		Seconds, 

		/// <summary>
		/// A frame range.
		/// </summary>
		Frames
	}
}