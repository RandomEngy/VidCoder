using Newtonsoft.Json;
using Omu.ValueInjecter;
using VidCoderCommon.Utilities.Injection;

namespace VidCoderCommon.Model
{
	[JsonObject]
    public class SourceSubtitle
    {
        /// <summary>
        /// Gets or sets a value indicating whether the subtitle track should be burned in.
        /// </summary>
		[JsonProperty]
        public bool BurnedIn { get; set; }

		[JsonProperty]
        public bool Default { get; set; }

		[JsonProperty(PropertyName = "Forced")]
        public bool ForcedOnly { get; set; }

        /// <summary>
        ///     Gets or sets the 1-based subtitle track number. 0 means foreign audio search.
        /// </summary>
		[JsonProperty]
        public int TrackNumber { get; set; }

		public SourceSubtitle Clone()
		{
			var subtitle = new SourceSubtitle();
			subtitle.InjectFrom<FastDeepCloneInjection>(this);
			return subtitle;
		}
    }
}