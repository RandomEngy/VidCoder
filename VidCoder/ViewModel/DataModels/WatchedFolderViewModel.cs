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

namespace VidCoder.ViewModel.DataModels
{
	public class WatchedFolderViewModel : ReactiveObject
	{
		public PickersService PickersService { get; } = StaticResolver.Resolve<PickersService>();
		public PresetsService PresetsService { get; } = StaticResolver.Resolve<PresetsService>();

		private readonly WatcherConfigDialogViewModel dialogViewModel;

		private List<PresetFolderViewModel> allPresetsTree;

		/// <summary>
		/// The PresetTreeViewContainer binds to this to get the ViewModel tree of presets.
		/// </summary>
		public List<PresetFolderViewModel> AllPresetsTree
		{
			get
			{
				return this.allPresetsTree;
			}
		}

		public WatchedFolder WatchedFolder { get; }

		public WatchedFolderViewModel(WatcherConfigDialogViewModel dialogViewModel, WatchedFolder watchedFolder)
		{
			this.dialogViewModel = dialogViewModel;

			// Clone the tree so the IsSelected doesn't get shared with other UI.
			//this.allPresetsTree = new ObservableCollection<PresetFolderViewModel>(this.PresetsService.AllPresetsTree.Select(folder => folder.Clone(null)));
			this.allPresetsTree = new List<PresetFolderViewModel>(this.PresetsService.AllPresetsTree.Select(folder => folder.Clone(null)));

			this.WatchedFolder = watchedFolder;
			this.selectedPreset = this.PresetsService.AllPresets.FirstOrDefault(preset => preset.Preset.Name == watchedFolder.Preset);
			if (this.selectedPreset == null)
			{
				this.selectedPreset = this.PresetsService.AllPresets[0];
				this.WatchedFolder.Preset = this.selectedPreset.Preset.Name;
			}
		}

		public string Path
		{
			get => this.WatchedFolder.Path;
			set
			{
				this.WatchedFolder.Path = value;
				this.RaisePropertyChanged();
			}
		}

		private PresetViewModel selectedPreset;
		public PresetViewModel SelectedPreset
		{
			get => this.selectedPreset;
			set
			{
				if (value != null)
				{
					this.WatchedFolder.Preset = value.Preset.Name;
					this.RaiseAndSetIfChanged(ref this.selectedPreset, value);
				}
			}
		}

		private ReactiveCommand<Unit, Unit> presetUp;
		public ICommand PresetUp
		{
			get
			{
				return this.presetUp ?? (this.presetUp = ReactiveCommand.Create(
					() =>
					{
						if (this.SelectedPreset == null)
						{
							return;
						}

						int currentIndex = this.PresetsService.AllPresets.IndexOf(this.SelectedPreset);
						if (currentIndex > 0)
						{
							this.SelectedPreset = this.PresetsService.AllPresets[currentIndex - 1];

							//keyEventArgs.Handled = true;
						}
					}));
			}
		}

		private ReactiveCommand<Unit, Unit> presetDown;
		public ICommand PresetDown
		{
			get
			{
				return this.presetDown ?? (this.presetDown = ReactiveCommand.Create(
					() =>
					{
						if (this.SelectedPreset == null)
						{
							return;
						}

						int currentIndex = this.PresetsService.AllPresets.IndexOf(this.SelectedPreset);
						if (currentIndex < this.PresetsService.AllPresets.Count - 1)
						{
							this.SelectedPreset = this.PresetsService.AllPresets[currentIndex + 1];

							//keyEventArgs.Handled = true;
						}
					}));
			}
		}

		private ReactiveCommand<Unit, Unit> pickFolder;
		public ICommand PickFolder
		{
			get
			{
				return this.pickFolder ?? (this.pickFolder = ReactiveCommand.Create(
					() =>
					{
						string newFolder = FileService.Instance.GetFolderName(null, WatcherRes.PickWatchedFolderDialogDescription);
						if (newFolder != null)
						{
							this.Path = newFolder;
						}
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
						this.dialogViewModel.RemoveFolder(this);
					}));
			}
		}

		public void HandlePresetComboKey(KeyEventArgs keyEventArgs)
		{
			if (this.SelectedPreset == null)
			{
				return;
			}

			int currentIndex = this.PresetsService.AllPresets.IndexOf(this.SelectedPreset);

			if (keyEventArgs.Key == Key.Up)
			{
				if (currentIndex > 0)
				{
					this.SelectedPreset = this.PresetsService.AllPresets[currentIndex - 1];

					keyEventArgs.Handled = true;
				}
			}
			else if (keyEventArgs.Key == Key.Down)
			{
				if (currentIndex < this.PresetsService.AllPresets.Count - 1)
				{
					this.SelectedPreset = this.PresetsService.AllPresets[currentIndex + 1];

					keyEventArgs.Handled = true;
				}
			}
		}
	}
}
