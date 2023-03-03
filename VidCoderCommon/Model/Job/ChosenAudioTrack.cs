using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VidCoderCommon.Model;

/// <summary>
/// A chosen audio track with options specified.
/// </summary>
public class ChosenAudioTrack
{
	/// <summary>
	/// Gets or sets the 1-based audio track number.
	/// </summary>
	public int TrackNumber { get; set; }

	/// <summary>
	/// Gets or sets the custom name for the track.
	/// </summary>
	public string Name { get; set; }

	public ChosenAudioTrack Clone()
	{
		return new ChosenAudioTrack { TrackNumber = this.TrackNumber, Name = this.Name };
	}
}
