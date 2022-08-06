using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VidCoder.Controls;
using VidCoder.Extensions;
using VidCoder.Model;
using VidCoderCommon.Model;

namespace VidCoder.ViewModel.DataModels
{
	public class WatchedFileViewModel : ReactiveObject, IListItemViewModel
	{
		public WatchedFileViewModel(WatchedFile watchedFile)
		{
			WatchedFile = watchedFile;
			this.status = watchedFile.Status.ToLive();
		}

		public WatchedFile WatchedFile { get; }

		private WatchedFileStatusLive status;
		public WatchedFileStatusLive Status
		{
			get => this.status;
			set => this.RaiseAndSetIfChanged(ref this.status, value);
		}

		private bool isSelected;
		public bool IsSelected
		{
			get => this.isSelected;
			set => this.RaiseAndSetIfChanged(ref this.isSelected, value);
		}
	}
}
