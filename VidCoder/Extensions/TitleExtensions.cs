using System;
using HandBrake.ApplicationServices.Interop.Json.Scan;
using VidCoder.Model;
using VidCoderCommon.Extensions;

namespace VidCoder.Extensions
{
	public static class TitleExtensions
	{
		public static int GetEstimatedFrames(this SourceTitle title)
		{
			return (int)Math.Ceiling(title.Duration.ToSpan().TotalSeconds * title.FrameRate.ToDouble());
		}

		public static string GetDisplayString(this SourceTitle title)
		{
			string playlistPortion = string.Empty;
			if (title.Type == (int)TitleType.Bluray)
			{
				playlistPortion = string.Format(" {0:d5}.MPLS", title.Playlist);
			}

			return string.Format(
				"{0}{1} ({2:00}:{3:00}:{4:00})",
				title.Index,
				playlistPortion,
				title.Duration.Hours,
				title.Duration.Minutes,
				title.Duration.Seconds);
		}
	}
}
