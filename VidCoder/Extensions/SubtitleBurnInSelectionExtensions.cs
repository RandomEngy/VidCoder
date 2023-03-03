using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VidCoder.Model;

namespace VidCoder.Extensions;

public static class SubtitleBurnInSelectionExtensions
{
	public static bool ForeignAudioIncluded(this SubtitleBurnInSelection subtitleBurnInSelection)
	{
		return subtitleBurnInSelection == SubtitleBurnInSelection.ForeignAudioTrack || subtitleBurnInSelection == SubtitleBurnInSelection.ForeignAudioTrackElseFirst;
	}

	public static bool FirstTrackIncluded(this SubtitleBurnInSelection subtitleBurnInSelection)
	{
		return subtitleBurnInSelection == SubtitleBurnInSelection.First || subtitleBurnInSelection == SubtitleBurnInSelection.ForeignAudioTrackElseFirst;
	}
}
