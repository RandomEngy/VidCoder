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
			if (title == null)
			{
				return string.Empty;
			}

			string playlistPortion = string.Empty;
			if (title.Type == (int)TitleType.Bluray)
			{
				playlistPortion = FormattableString.Invariant($" {title.Playlist:d5}.MPLS");
			}

			int hours, minutes, seconds;
			if (title.Duration != null)
			{
				hours = title.Duration.Hours;
				minutes = title.Duration.Minutes;
				seconds = title.Duration.Seconds;
			}
			else
			{
				hours = 0;
				minutes = 0;
				seconds = 0;
			}

			return string.Format(
				"{0}{1} ({2:00}:{3:00}:{4:00})",
				title.Index,
				playlistPortion,
				hours,
				minutes,
				seconds);
		}
	}
}
