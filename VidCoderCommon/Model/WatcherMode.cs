using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VidCoderCommon.Model;

public enum WatcherMode
{
	/// <summary>
	/// Use the built-in OS watcher to notify us of file events in the watched folders.
	/// </summary>
	FileSystemWatcher,

	/// <summary>
	/// Every so often, enumerate the contents of the watched folders.
	/// </summary>
	Polling
}
