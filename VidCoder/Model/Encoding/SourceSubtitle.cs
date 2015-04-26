namespace VidCoder.Model.Encoding
{
    public class SourceSubtitle
    {
        /// <summary>
        /// Gets or sets a value indicating whether the subtitle track should be burned in.
        /// </summary>
        public bool BurnedIn { get; set; }

        public bool Default { get; set; }

        public bool Forced { get; set; }

        /// <summary>
        ///     Gets or sets the 1-based subtitle track number. 0 means foreign audio search.
        /// </summary>
        public int TrackNumber { get; set; }

        public SourceSubtitle Clone()
        {
            return new SourceSubtitle
                       {
                           TrackNumber = this.TrackNumber, 
                           Default = this.Default, 
                           Forced = this.Forced, 
                           BurnedIn = this.BurnedIn
                       };
        }
    }
}