namespace VidCoder.Model
{
	using System.ComponentModel.DataAnnotations;
	using LocalResources;

	public enum VideoRangeTypeCombo
	{
		[Display(ResourceType = typeof(EnumsRes), Name = "VideoRangeType_Chapters")]
		Chapters,

		[Display(ResourceType = typeof(EnumsRes), Name = "VideoRangeType_Seconds")]
		Seconds,

		[Display(ResourceType = typeof(EnumsRes), Name = "VideoRangeType_Frames")]
		Frames
	}
}
