using VidCoderCommon.Model;

namespace VidCoder.Model
{
	/// <summary>
	/// Wrapper for a Preset object for when it is stored in a file. When it's in the database the version is implied.
	/// </summary>
	public class PresetWrapper
	{
		public int Version { get; set; }
		public Preset Preset { get; set; }
	}
}
