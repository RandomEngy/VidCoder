namespace VidCoderCommon.Model
{
	using System;
	using System.Collections.Generic;
	using System.Runtime.Serialization;

	public class VCSubtitles
    {
        public List<ChosenSourceSubtitle> SourceSubtitles { get; set; }

		[Obsolete("Use FileSubtitles instead")]
		public List<FileSubtitle> SrtSubtitles { get; set; }

        public List<FileSubtitle> FileSubtitles { get; set; }
	}
}