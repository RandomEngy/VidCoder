using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VidCoder.Model
{
	public enum SubtitleSelectionMode
	{
		// Now called "Last selected", picks last selected subtitle if a match is found, otherwise none
		Disabled = 0,
		None = 4,
		First = 5,
		ByIndex = 6,
		Language = 2,
		All = 3,

		// Obsolete. Now controlled by SubtitleAddForeignAudioScan
		ForeignAudioSearch = 1
	}
}
