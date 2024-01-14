using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VidCoderCommon.Model;

public class WatchedFile
{
	public string Path { get; set; }

	public WatchedFileStatus Status { get; set; }

	public WatchedFileStatusReason Reason { get; set; }
}
