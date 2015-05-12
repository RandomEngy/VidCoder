using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VidCoder.Messages
{
	public class ProgressChangedMessage
	{
		public bool Encoding { get; set; }

		public double OverallProgressFraction { get; set; }

		public int TaskNumber { get; set; }

		public int TotalTasks { get; set; }

		public TimeSpan OverallElapsedTime { get; set; }

		public TimeSpan OverallEta { get; set; }

		public string FileName { get; set; }

		public bool EncodeSpeedDetailsAvailable { get; set; }

		public double FileProgressFraction { get; set; }

		public long FileSizeBytes { get; set; }

		public TimeSpan FileElapsedTime { get; set; }

		public TimeSpan FileEta { get; set; }

		public double CurrentFps { get; set; }

		public double AverageFps { get; set; }

		public bool HasScanPass { get; set; }

		public bool TwoPass { get; set; }

		public int CurrentPassId { get; set; }

		public double PassProgressFraction { get; set; }
	}
}
