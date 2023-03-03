using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VidCoder.Model;

namespace VidCoder.Extensions
{
	public static class LogEntryExtensions
	{
		public static string FormatMessage(this LogEntry entry)
		{
			string categoryMarker = GetCategoryMarker(entry.Source);
			string errorMarker = entry.LogType == LogType.Error ? "E " : string.Empty;

			string time;
			if (entry.Source == LogSource.HandBrake)
			{
				// HandBrake log lines already have time in most cases
				time = string.Empty;
			}
			else
			{
				time = FormattableString.Invariant($"[{DateTimeOffset.Now.ToString("HH:mm:ss")}] ");
			}

			return FormattableString.Invariant($"{categoryMarker} {errorMarker}{time}{entry.Text}");
		}

		private static string GetCategoryMarker(LogSource source)
		{
			switch (source)
			{
				case LogSource.VidCoder:
					return "VC";
				case LogSource.VidCoderWorker:
					return "VCW";
				case LogSource.HandBrake:
					return "HB";
				default:
					throw new ArgumentException($"Unable to categorize LogSource {source}", nameof(source));
			}
		}
	}
}
