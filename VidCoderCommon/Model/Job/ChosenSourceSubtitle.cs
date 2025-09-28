using Omu.ValueInjecter;
using System.Text.Json.Serialization;
using VidCoderCommon.Utilities.Injection;

namespace VidCoderCommon.Model;

/// <summary>
/// A chosen source subtitle with options specified.
/// </summary>
public class ChosenSourceSubtitle
{
	/// <summary>
	/// Gets or sets a value indicating whether the subtitle track should be burned in.
	/// </summary>
	public bool BurnedIn { get; set; }

	public bool Default { get; set; }

	[JsonPropertyName("Forced")]
	public bool ForcedOnly { get; set; }

	/// <summary>
	/// Gets or sets the 1-based subtitle track number. 0 means foreign audio search.
	/// </summary>
	public int TrackNumber { get; set; }

	/// <summary>
	/// Gets or sets the custom name for the track.
	/// </summary>
	public string Name { get; set; }

	public ChosenSourceSubtitle Clone()
	{
		var subtitle = new ChosenSourceSubtitle();
		subtitle.InjectFrom<CloneInjection>(this);
		return subtitle;
	}
}