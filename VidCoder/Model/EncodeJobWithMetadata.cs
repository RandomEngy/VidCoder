using VidCoderCommon.Model;

namespace VidCoder.Model
{
	public class EncodeJobWithMetadata
	{
		public VCJob Job { get; set; }

		// The parent folder for the item (if it was inside a folder of files added in a batch)
		public string SourceParentFolder { get; set; }

		public bool ManualOutputPath { get; set; }

		public string NameFormatOverride { get; set; }

		public string PresetName { get; set; }

		public VideoSource VideoSource { get; set; }

		public VideoSourceMetadata VideoSourceMetadata { get; set; }
	}
}
