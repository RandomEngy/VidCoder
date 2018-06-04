using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VidCoder.Model
{
    public class FileEncodeProgress
    {
		public string FileName { get; set; }

	    public double ProgressFraction { get; set; }

	    public long FileSizeBytes { get; set; }

	    public TimeSpan ElapsedTime { get; set; }

	    public TimeSpan Eta { get; set; }

	    public bool HasScanPass { get; set; }

	    public bool TwoPass { get; set; }

	    public int CurrentPassId { get; set; }

	    public double PassProgressFraction { get; set; }
	}
}
