using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VidCoderFileWatcher.Model
{
	public interface IWatcherCommands
	{
		/// <summary>
		/// Tells the file watcher that the watched folders have changed and the folders need to be enumerated again.
		/// </summary>
		void RefreshFromWatchedFolders();

		/// <summary>
		/// Tells the file watcher process that it needs to re-fetch the Pickers from the database.
		/// </summary>
		void InvalidatePickers();

		void Ping();
	}
}
