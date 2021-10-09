using System;
using System.Globalization;
using HandBrake.Interop.Interop.Json.Scan;
using VidCoder.Model;
using VidCoderCommon.Extensions;

namespace VidCoder.Extensions
{
	public static class TitleExtensions
	{
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
				CultureInfo.CurrentCulture,
				"{0}{1} ({2:0}:{3:00}:{4:00})",
				title.Index,
				playlistPortion,
				hours,
				minutes,
				seconds);
		}

		public static TimeSpan GetChapterRangeDuration(this SourceTitle title, int startChapter, int endChapter)
		{
			if (startChapter > endChapter ||
				endChapter > title.ChapterList.Count ||
				startChapter < 1)
			{
				return TimeSpan.Zero;
			}

			TimeSpan rangeTime = TimeSpan.Zero;

			for (int i = startChapter; i <= endChapter; i++)
			{
				rangeTime += title.ChapterList[i - 1].Duration.ToSpan();
			}

			return rangeTime;
		}
	}
}
