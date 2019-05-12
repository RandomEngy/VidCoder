using HandBrake.Interop.Interop.Json.Scan;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VidCoder.Model;

namespace VidCoder
{
	public static class PickerUtilities
	{
		public static (int chapterStart, int chapterEnd) GetChapterRange(Picker picker, SourceTitle title)
		{
			int chapterStart = picker.ChapterRangeStart ?? 1;
			int chapterEnd = picker.ChapterRangeEnd ?? title.ChapterList.Count;

			if (chapterStart > title.ChapterList.Count)
			{
				chapterStart = title.ChapterList.Count;
			}

			if (chapterEnd > title.ChapterList.Count)
			{
				chapterEnd = title.ChapterList.Count;
			}

			// If start is greater than end, swap them.
			if (chapterStart > chapterEnd)
			{
				int temp = chapterStart;
				chapterStart = chapterEnd;
				chapterEnd = temp;
			}

			return (chapterStart, chapterEnd);
		}
	}
}
