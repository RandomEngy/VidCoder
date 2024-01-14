using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VidCoder.Model;

public class LoggedEntry
{
	public LogEntry Entry { get; set; }

	public long ByteCount { get; set; }

	public LogChunk Chunk { get; set; }
}
