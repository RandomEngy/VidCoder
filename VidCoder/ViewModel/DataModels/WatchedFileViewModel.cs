using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
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

			// CanCancel
			this.WhenAnyValue(x => x.Status)
				.Select(status =>
				{
					return status == WatchedFileStatusLive.Queued;
				})
				.ToProperty(this, x => x.CanCancel, out this.canCancel);

			// CanRetry
			this.WhenAnyValue(x => x.Status)
				.Select(status =>
				{
					return status == WatchedFileStatusLive.Succeeded || status == WatchedFileStatusLive.Failed || status == WatchedFileStatusLive.Canceled;
				})
				.ToProperty(this, x => x.CanRetry, out this.canRetry);
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

		private ObservableAsPropertyHelper<bool> canCancel;
		public bool CanCancel => this.canCancel.Value;

		private ReactiveCommand<Unit, Unit> cancel;
		public ICommand Cancel
		{
			get
			{
				return this.cancel ?? (this.cancel = ReactiveCommand.Create(
					() =>
					{

					}));
			}
		}

		private ObservableAsPropertyHelper<bool> canRetry;
		public bool CanRetry => this.canRetry.Value;

		private ReactiveCommand<Unit, Unit> retry;
		public ICommand Retry
		{
			get
			{
				return this.retry ?? (this.retry = ReactiveCommand.Create(
					() =>
					{

					}));
			}
		}

		private ReactiveCommand<Unit, Unit> openContainingFolder;
		public ICommand OpenContainingFolder
		{
			get
			{
				return this.openContainingFolder ?? (this.openContainingFolder = ReactiveCommand.Create(
					() =>
					{
						FileUtilities.OpenFolderAndSelectItem(this.WatchedFile.Path);
					}));
			}
		}

	}
}
