using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VidCoderFileWatcher.Model
{
	public interface IWatcherCommands
	{
		void RefreshFromWatchedFolderUpdate();
	}
}
