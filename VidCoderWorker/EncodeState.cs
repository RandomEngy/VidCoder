using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VidCoderWorker;

internal enum EncodeState
{
	NotStarted,
	Scanning,
	Encoding,
	Stopping,
	Finished
}
