using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VidCoder.Model;

namespace VidCoder
{
	public static class LogEntryClassificationUtilities
	{
		private static readonly Dictionary<LogColor, string> ColorToPrefixMapping = new Dictionary<LogColor, string>();

		static LogEntryClassificationUtilities()
		{
			ColorToPrefixMapping.Add(LogColor.VidCoder, "# ");
			ColorToPrefixMapping.Add(LogColor.VidCoderWorker, "w# ");
			ColorToPrefixMapping.Add(LogColor.Error, "ERROR: ");
		}

		public static LogColor GetEntryColor(LogEntry entry)
		{
			return GetEntryColor(entry.LogType, entry.Source);
		}

		public static LogColor GetEntryColor(LogType type, LogSource source)
		{
			if (type == LogType.Error)
			{
				return LogColor.Error;
			}
			else if (source == LogSource.VidCoder)
			{
				return LogColor.VidCoder;
			}
			else if (source == LogSource.VidCoderWorker)
			{
				return LogColor.VidCoderWorker;
			}

			return LogColor.Normal;
		}

		public static string GetEntryPrefix(LogType type, LogSource source)
		{
			LogColor color = GetEntryColor(type, source);
			return GetEntryPrefix(color);
		}

		public static string GetEntryPrefix(LogColor color)
		{
			if (ColorToPrefixMapping.TryGetValue(color, out string prefix))
			{
				return prefix;
			}

			return string.Empty;
		}

		public static LogColor GetLineColor(string line)
		{
			foreach (var pair in ColorToPrefixMapping)
			{
				if (line.StartsWith(pair.Value, StringComparison.Ordinal))
				{
					return pair.Key;
				}
			}

			return LogColor.Normal;
		}
	}
}
