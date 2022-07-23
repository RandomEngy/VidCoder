using DynamicData;
using DynamicData.Binding;
using Microsoft.AnyContainer;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using VidCoder.Model;
using VidCoder.Services;
using VidCoder.ViewModel.DataModels;
using VidCoderCommon.Model;

namespace VidCoder.ViewModel
{
	public class WatcherConfigDialogViewModel : OkCancelDialogViewModel
	{
		private PresetsService presetsService = StaticResolver.Resolve<PresetsService>();

		public WatcherConfigDialogViewModel()
		{
			this.watchedFolders.AddRange(WatcherStorage.GetWatchedFolders(Database.Connection).Select(watchedFolder => new WatchedFolderViewModel(this, watchedFolder)));
			this.watchedFolders.Connect().Bind(this.WatchedFoldersBindable).Subscribe();

			if (this.watchedFolders.Count == 0)
			{
				this.AddWatchedFolder.Execute(null);
			}
		}

		public SourceList<WatchedFolderViewModel> WatchedFolders => watchedFolders;


		private readonly SourceList<WatchedFolderViewModel> watchedFolders = new SourceList<WatchedFolderViewModel>();
		public ObservableCollectionExtended<WatchedFolderViewModel> WatchedFoldersBindable { get; } = new ObservableCollectionExtended<WatchedFolderViewModel>();

		private ReactiveCommand<Unit, Unit> addWatchedFolder;
		public ICommand AddWatchedFolder
		{
			get
			{
				return this.addWatchedFolder ?? (this.addWatchedFolder = ReactiveCommand.Create(
					() =>
					{
						var newViewModel = new WatchedFolderViewModel(
							this,
							new WatchedFolder
							{
								Picker = string.Empty, // This is the default picker
								Preset = this.presetsService.AllPresets[0].Preset.Name
							});
						this.WatchedFolders.Add(newViewModel);
						newViewModel.PickFolder.Execute(null);
					}));
			}
		}

		public void RemoveFolder(WatchedFolderViewModel folderToRemove)
		{
			this.WatchedFolders.Remove(folderToRemove);
		}
	}
}
