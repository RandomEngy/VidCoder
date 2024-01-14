using Microsoft.AnyContainer;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using VidCoder.Resources;
using VidCoder.Services;
using VidCoderCommon.Model;

namespace VidCoder.ViewModel.DataModels;

public class WatchedFolderViewModel : ReactiveObject
{
	private readonly WatcherWindowViewModel windowViewModel;

	public WatchedFolder WatchedFolder { get; }

	public WatchedFolderViewModel(WatcherWindowViewModel windowViewModel, WatchedFolder watchedFolder)
	{
		this.windowViewModel = windowViewModel;
		this.WatchedFolder = watchedFolder;
	}

	private ReactiveCommand<Unit, Unit> editFolder;
	public ICommand EditFolder
	{
		get
		{
			return this.editFolder ?? (this.editFolder = ReactiveCommand.Create(
				() =>
				{
					this.windowViewModel.EditFolder(this);
				}));
		}
	}

	private ReactiveCommand<Unit, Unit> removeFolder;
	public ICommand RemoveFolder
	{
		get
		{
			return this.removeFolder ?? (this.removeFolder = ReactiveCommand.Create(
				() =>
				{
					this.windowViewModel.RemoveFolder(this);
				}));
		}
	}

	public string PickerName => string.IsNullOrEmpty(this.WatchedFolder.Picker) ? CommonRes.Default : this.WatchedFolder.Picker;
}
