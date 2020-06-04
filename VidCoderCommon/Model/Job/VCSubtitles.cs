namespace VidCoderCommon.Model
{
	using System;
	using System.Collections.Generic;
	using System.Runtime.Serialization;

	public class VCSubtitles
    {
        public List<ChosenSourceSubtitle> SourceSubtitles { get; set; }

		[Obsolete("Use FileSubtitles instead")]
		[DeserializeOnly]
		public List<FileSubtitle> SrtSubtitles { get; set; }

        public List<FileSubtitle> FileSubtitles { get; set; }

		[OnDeserialized]
		internal void OnDeserializedMethod(StreamingContext context)
		{
#pragma warning disable 618
			if (this.SrtSubtitles != null && this.FileSubtitles == null)
			{
				this.FileSubtitles = this.SrtSubtitles;
			}
#pragma warning restore 618
		}
	}
}