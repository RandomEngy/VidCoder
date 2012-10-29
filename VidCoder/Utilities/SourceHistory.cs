using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VidCoder
{
	public static class SourceHistory
	{
		private const int MaxHistoryLength = 2;

		public static void AddToHistory(string sourcePath)
		{
			if (!Config.RememberPreviousFiles)
			{
				return;
			}

			List<string> history = GetHistory();
			history.Remove(sourcePath);

			history.Insert(0, sourcePath);

			if (history.Count > MaxHistoryLength)
			{
				history.RemoveAt(history.Count - 1);
			}

			Config.SourceHistory = string.Join("|", history);
		}

		/// <summary>
		/// Gets the source history. Most recent are in front.
		/// </summary>
		public static List<string> GetHistory()
		{
			string historySetting = Config.SourceHistory;
			if (string.IsNullOrEmpty(historySetting))
			{
				return new List<string>();
			}
			
			return new List<string>(historySetting.Split('|'));
		} 
	}
}
