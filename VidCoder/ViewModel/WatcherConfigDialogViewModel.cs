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
			this.watchedFolders.AddRange(WatcherStorage.GetWatchedFolders(Database.Connection).Select(watchedFolder => new WatchedFolderViewModel(watchedFolder)));
			this.watchedFolders.Connect().Bind(this.WatchedFoldersBindable).Subscribe();
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
						this.WatchedFolders.Add(new WatchedFolderViewModel(new WatchedFolder { Preset = this.presetsService.AllPresets[0].Preset.Name }));
					}));
			}
		}
	}
}
