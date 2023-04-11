using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;

namespace VidCoder.Model;

/// <summary>
/// A chunk of log entries, the unit for loading and unloading log UI elements into the log window.
/// </summary>
public record LogChunk
{
	public int LineCount { get; set; }

	public double MeasuredHeight { get; set; }

	/// <summary>
	/// Gets or sets the byte position in the file for the batch.
	/// </summary>
	public long BytePosition { get; set; }

	/// <summary>
	/// Gets or sets the number of bytes in this batch.
	/// </summary>
	public long ByteCount { get; set; }

	public List<Run> Runs { get; set; }
};
