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
		private static readonly Dictionary<string, LogSource> SourcePrefixes = new Dictionary<string, LogSource> {
			{ "VC ", LogSource.VidCoder },
			{ "VW ", LogSource.VidCoderWorker },
			{ "HB ", LogSource.HandBrake },
		};

		static LogEntryClassificationUtilities()
		{
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

		public static LogColor GetLineColor(string line)
		{
			if (line.Length < 3)
			{
				return LogColor.NoChange;
			}

			string sourceString = line.Substring(0, 3);
			if (SourcePrefixes.TryGetValue(sourceString, out LogSource source))
			{
				LogType logType = line.Length >= 5 && line.Substring(3, 2) == "E " ? LogType.Error : LogType.Message;

				return GetEntryColor(logType, source);
			}

			return LogColor.NoChange;
		}
	}
}
