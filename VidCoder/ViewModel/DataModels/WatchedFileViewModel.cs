using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VidCoderCommon.Model;

namespace VidCoder.ViewModel.DataModels
{
	public class WatchedFileViewModel : ReactiveObject
	{
		public WatchedFileViewModel(WatchedFile watchedFile)
		{
			WatchedFile = watchedFile;
		}

		public WatchedFile WatchedFile { get; }
	}
}
