using Newtonsoft.Json;
using Omu.ValueInjecter;
using VidCoderCommon.Utilities.Injection;

namespace VidCoderCommon.Model
{
	/// <summary>
	/// A chosen source subtitle with options specified.
	/// </summary>
	[JsonObject]
    public class ChosenSourceSubtitle
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
        /// Gets or sets the 1-based subtitle track number. 0 means foreign audio search.
        /// </summary>
		[JsonProperty]
        public int TrackNumber { get; set; }

		/// <summary>
		/// Gets or sets the custom name for the track.
		/// </summary>
		[JsonProperty]
		public string Name { get; set; }

		public ChosenSourceSubtitle Clone()
		{
			var subtitle = new ChosenSourceSubtitle();
			subtitle.InjectFrom<CloneInjection>(this);
			return subtitle;
		}
    }
}