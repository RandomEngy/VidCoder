using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VidCoder.Model;

/// <summary>
/// Determines which track to select to burn in.
/// </summary>
public enum SubtitleBurnInSelection
{
	/// <summary>
	/// Nothing burnt in. Default when Foreign audio scan is disabled.
	/// </summary>
	None,

	/// <summary>
	/// Burn in the foreign audio track. Default when Foreign audio scan is enabled.
	/// </summary>
	ForeignAudioTrack,

	/// <summary>
	/// Burn in the first track. Only show when selection mode is not "None"
	/// </summary>
	First,

	/// <summary>
	/// Burn in the foreign audio track, or else the first track. Only show when seleciton mode is not "None" and Foreign audio scan is enabled.
	/// </summary>
	ForeignAudioTrackElseFirst
}
