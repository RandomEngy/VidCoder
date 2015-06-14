namespace VidCoderCommon.Model
{
	public class Preset
	{
		public string Name { get; set; }
		public bool IsBuiltIn { get; set; }
		public bool IsModified { get; set; }
		public bool IsQueue { get; set; }
		public VCProfile EncodingProfile { get; set; }
	}
}
