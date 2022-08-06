using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VidCoder.Model
{
	/// <summary>
	/// Status of a watched file as shown in the Watcher window, reflecting queued and encoding status.
	/// </summary>
	public enum WatchedFileStatusLive
	{
		/// <summary>
		/// VidCoder has planned to encode this file but it has not yet been added to the queue. It should only be in this state as long as it takes to scan the item.
		/// </summary>
		Planned,

		/// <summary>
		/// The file has been added to the queue.
		/// </summary>
		Queued,

		/// <summary>
		/// The file is currently encoding.
		/// </summary>
		Encoding,

		/// <summary>
		/// VidCoder has successfully encoded this file.
		/// </summary>
		Succeeded,

		/// <summary>
		/// VidCoder tried to encode the file but it failed.
		/// </summary>
		Failed,

		/// <summary>
		/// The user canceled the encode, so we won't retry until asked.
		/// </summary>
		Canceled,

		/// <summary>
		/// The file was skipped by the system due to filtering rules.
		/// </summary>
		Skipped
	}
}
