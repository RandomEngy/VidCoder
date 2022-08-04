using DynamicData;
using DynamicData.Binding;
using Microsoft.AnyContainer;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using VidCoder.Model;
using VidCoder.Resources;
using VidCoder.Services;
using VidCoder.Services.Windows;
using VidCoder.ViewModel.DataModels;
using VidCoderCommon;
using VidCoderCommon.Model;

namespace VidCoder.ViewModel
{
	public class WatcherWindowViewModel : ReactiveObject
	{
		private IWindowManager windowManager = StaticResolver.Resolve<IWindowManager>();
		private WatcherProcessManager watcherProcessManager = StaticResolver.Resolve<WatcherProcessManager>();
		private IAppLogger logger = StaticResolver.Resolve<IAppLogger>();

		private PresetsService presetsService = StaticResolver.Resolve<PresetsService>();

		public WatcherWindowViewModel()
		{
			this.WatchedFolders.AddRange(WatcherStorage.GetWatchedFolders(Database.Connection).Select(watchedFolder => new WatchedFolderViewModel(this, watchedFolder)));
			this.WatchedFolders.Connect().Bind(this.WatchedFoldersBindable).Subscribe();

			this.WatchedFiles.AddRange(WatcherStorage.GetWatchedFiles(Database.Connection).Select(pair => new WatchedFileViewModel(pair.Value)));
			this.WatchedFiles.Connect().Bind(this.WatchedFilesBindable).Subscribe();

			// WindowTitle
			this.WhenAnyValue(x => x.watcherProcessManager.Status)
				.Select(status =>
				{
					return string.Format(WatcherRes.WatcherWindowTitle, GetStatusString(status));
				})
				.ToProperty(this, x => x.WindowTitle, out this.windowTitle);
		}

		private ObservableAsPropertyHelper<string> windowTitle;
		public string WindowTitle => this.windowTitle.Value;

		public SourceList<WatchedFolderViewModel> WatchedFolders { get; } = new SourceList<WatchedFolderViewModel>();
		public ObservableCollectionExtended<WatchedFolderViewModel> WatchedFoldersBindable { get; } = new ObservableCollectionExtended<WatchedFolderViewModel>();

		public SourceList<WatchedFileViewModel> WatchedFiles { get; } = new SourceList<WatchedFileViewModel>();
		public ObservableCollectionExtended<WatchedFileViewModel> WatchedFilesBindable { get; } = new ObservableCollectionExtended<WatchedFileViewModel>();

		private bool watcherEnabled = Config.WatcherEnabled;
		public bool WatcherEnabled
		{
			get { return this.watcherEnabled; }
			set
			{
				Config.WatcherEnabled = value;
				this.RaiseAndSetIfChanged(ref this.watcherEnabled, value);
				if (value)
				{
					this.watcherProcessManager.Start();
				}
				else
				{
					this.watcherProcessManager.Stop();
				}
			}
		}

		private bool runWhenClosed = RegistryUtilities.IsFileWatcherAutoStart();
		public bool RunWhenClosed
		{
			get { return this.runWhenClosed; }
			set
			{
				this.RaiseAndSetIfChanged(ref this.runWhenClosed, value);
				if (value)
				{
					RegistryUtilities.SetFileWatcherAutoStart(this.logger);
				}
				else
				{
					RegistryUtilities.RemoveFileWatcherAutoStart(this.logger);
				}
			}
		}

		private ReactiveCommand<Unit, Unit> addWatchedFolder;
		public ICommand AddWatchedFolder
		{
			get
			{
				return this.addWatchedFolder ?? (this.addWatchedFolder = ReactiveCommand.Create(
					() =>
					{
						var newWatchedFolder = new WatchedFolder
						{
							Picker = string.Empty, // This is the default picker
							Preset = this.presetsService.AllPresets[0].Preset.Name
						};

						var addWatchedFolderViewModel = new WatcherEditDialogViewModel(newWatchedFolder, isAdd: true);
						this.windowManager.OpenDialog(addWatchedFolderViewModel, this);

						if (addWatchedFolderViewModel.DialogResult)
						{
							this.WatchedFolders.Add(new WatchedFolderViewModel(this, addWatchedFolderViewModel.WatchedFolder));
							this.SaveWatchedFolders();
						}
					}));
			}
		}

		public void EditFolder(WatchedFolderViewModel folderToEdit)
		{
			var editWatchedFolderViewModel = new WatcherEditDialogViewModel(folderToEdit.WatchedFolder.Clone(), isAdd: false);
			this.windowManager.OpenDialog(editWatchedFolderViewModel, this);

			if (editWatchedFolderViewModel.DialogResult)
			{
				this.WatchedFolders.Replace(folderToEdit, new WatchedFolderViewModel(this, editWatchedFolderViewModel.WatchedFolder));
				this.SaveWatchedFolders();
			}
		}

		public void RemoveFolder(WatchedFolderViewModel folderToRemove)
		{
			this.WatchedFolders.Remove(folderToRemove);
			this.SaveWatchedFolders();
		}

		private void SaveWatchedFolders()
		{ 
			WatcherStorage.SaveWatchedFolders(Database.Connection, this.WatchedFolders.Items.Select(f => f.WatchedFolder));
		}

		private static string GetStatusString(WatcherProcessStatus status)
		{
			switch (status)
			{
				case WatcherProcessStatus.Running:
					return EnumsRes.WatcherProcessStatus_Running;
				case WatcherProcessStatus.Starting:
					return EnumsRes.WatcherProcessStatus_Starting;
				case WatcherProcessStatus.Stopped:
					return EnumsRes.WatcherProcessStatus_Stopped;
				case WatcherProcessStatus.Disabled:
					return EnumsRes.WatcherProcessStatus_Disabled;
				default:
					return string.Empty;
			}
		}
	}
}
