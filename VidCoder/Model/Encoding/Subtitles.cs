namespace VidCoder.Model.Encoding
{
    using System.Collections.Generic;

    public class VCSubtitles
    {
        public List<SourceSubtitle> SourceSubtitles { get; set; }

        public List<SrtSubtitle> SrtSubtitles { get; set; }
    }
}