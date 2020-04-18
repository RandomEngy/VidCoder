using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using DynamicData;
using DynamicData.Aggregation;
using DynamicData.Binding;
using ReactiveUI;
using VidCoder.Model;
using VidCoder.Services;

namespace VidCoder.ViewModel.DataModels
{
	public class PresetFolderViewModel : ReactiveObject
	{
		private readonly PresetsService presetsService;

		public static PresetFolderViewModel FromPresetFolder(PresetFolder folder, PresetsService passedPresetsService)
		{
			return new PresetFolderViewModel(passedPresetsService, folder.IsExpanded, isBuiltIn: false, id: folder.Id)
			{
				Name = folder.Name,
				ParentId = folder.ParentId
			};
		}

		public PresetFolderViewModel(PresetsService presetsService, bool isExpanded, bool isBuiltIn, long id = 0)
		{
			this.presetsService = presetsService;
			this.isExpanded = isExpanded;
			this.IsBuiltIn = isBuiltIn;
			this.Id = id;

			this.AllItems
				.Connect()
				.Bind(this.AllItemsBindable)
				.Subscribe();

			this.WhenAnyValue(x => x.IsExpanded)
				.Skip(1)
				.Subscribe(isExp =>
				{
					this.presetsService.SaveFolderIsExpanded(this);
				});

			this.WhenAnyValue(x => x.IsExpanded)
				.Subscribe(isExp =>
				{
					this.ReselectPresetOnExpanded(this);
				});
		}

		private string name;
		public string Name
		{
			get { return this.name; }
			set { this.RaiseAndSetIfChanged(ref this.name, value); }
		}

		public long Id { get; }

		public long ParentId { get; set; }

		public PresetFolderViewModel Parent { get; set; }

		public bool IsBuiltIn { get; }

		public bool IsNotRoot => this.Id != 0;

		/// <summary>
		/// The flat list of both subfolders and items.
		/// </summary>
		/// <remarks>Subfolders are before items, and everything is alphabetical.</remarks>
		public SourceList<object> AllItems { get; } = new SourceList<object>();
		public ObservableCollectionExtended<object> AllItemsBindable { get; } = new ObservableCollectionExtended<object>();

		public SourceList<PresetFolderViewModel> SubFolders { get; } = new SourceList<PresetFolderViewModel>();

		//private readonly IObservable<int> subFolderCountObservable;

		public SourceList<PresetViewModel> Items { get; } = new SourceList<PresetViewModel>();

		//private readonly IObservable<int> itemsCountObservable;

		private bool isExpanded;
		public bool IsExpanded
		{
			get { return this.isExpanded; }
			set
			{
				this.RaiseAndSetIfChanged(ref this.isExpanded, value);
				if (value)
				{
					this.presetsService.ReportFolderExpanded(this);
				}
			}
		}

		public void AddSubfolder(PresetFolderViewModel subfolderViewModel)
		{
			this.SubFolders.Add(subfolderViewModel);
			subfolderViewModel.Parent = this;

			int insertionIndex;

			// Add in the right place.
			for (insertionIndex = 0; insertionIndex <= this.AllItems.Count; insertionIndex++)
			{
				// If at the end, add there.
				if (insertionIndex == this.AllItems.Count)
				{
					break;
				}

				// If we made it to the presets, add there at end of folder list.
				object item = this.AllItems.Items.ElementAt(insertionIndex);
				var preset = item as PresetViewModel;
				if (preset != null)
				{
					break;
				}

				// If the name compares to less than this folder name, (and we are not built-in) add here.
				var folder = (PresetFolderViewModel)item;
				if (!this.IsBuiltIn && string.Compare(subfolderViewModel.Name, folder.Name, StringComparison.CurrentCultureIgnoreCase) < 0)
				{
					break;
				}
			}

			this.AllItems.Insert(insertionIndex, subfolderViewModel);

			this.IsExpanded = true;
		}

		public void RemoveSubfolder(PresetFolderViewModel subFolderViewModel)
		{
			this.SubFolders.Remove(subFolderViewModel);
			this.AllItems.Remove(subFolderViewModel);
		}

		public void AddItem(PresetViewModel presetViewModel)
		{
			this.Items.Add(presetViewModel);
			presetViewModel.Parent = this;

			// For built in folders we use the given ordering.
			if (this.IsBuiltIn)
			{
				this.AllItems.Add(presetViewModel);
				return;
			}

			// Add in the right place.
			int insertionIndex;

			for (insertionIndex = 0; insertionIndex <= this.AllItems.Count; insertionIndex++)
			{
				// If at the end, add there.
				if (insertionIndex == this.AllItems.Count)
				{
					break;
				}

				// If we are still on folders, keep going
				object item = this.AllItems.Items.ElementAt(insertionIndex);
				var folder = item as PresetFolderViewModel;
				if (folder != null)
				{
					continue;
				}

				// If the name compares to less than this preset name, add here.
				var preset = (PresetViewModel)item;
				if (string.Compare(presetViewModel.DisplayName, preset.DisplayName, StringComparison.CurrentCultureIgnoreCase) < 0)
				{
					break;
				}
			}

			this.AllItems.Insert(insertionIndex, presetViewModel);
		}

		public void RemoveItem(PresetViewModel presetViewModel)
		{
			this.Items.Remove(presetViewModel);
			this.AllItems.Remove(presetViewModel);
		}

		private ReactiveCommand<Unit, Unit> createSubfolder;
		public ICommand CreateSubfolder
		{
			get
			{
				return this.createSubfolder ?? (this.createSubfolder = ReactiveCommand.Create(() =>
				{
					this.presetsService.CreateSubFolder(this);
				}));
			}
		}

		private ReactiveCommand<Unit, Unit> renameFolder;
		public ICommand RenameFolder
		{
			get
			{
				return this.renameFolder ?? (this.renameFolder = ReactiveCommand.Create(() =>
				{
					this.presetsService.RenameFolder(this);
				}));
			}
		}

		private IObservable<bool> canRemoveFolderObservable;
		private ReactiveCommand<Unit, Unit> removeFolder;
		public ICommand RemoveFolder
		{
			get
			{
				if (this.removeFolder == null)
				{
					this.canRemoveFolderObservable = Observable.CombineLatest(
						this.SubFolders.Connect().Count().StartWith(this.SubFolders.Count),
						this.Items.Connect().Count().StartWith(this.Items.Count),
						(numSubfolders, numItems) =>
						{
							return numSubfolders == 0 && numItems == 0 && this.Id != 0;
						});

					this.removeFolder = ReactiveCommand.Create(
						() =>
						{
							this.presetsService.RemoveFolder(this);
						},
						this.canRemoveFolderObservable);
				}

				return this.removeFolder;
			}
		}

		private bool ReselectPresetOnExpanded(PresetFolderViewModel presetFolderViewModel)
		{
			if (!presetFolderViewModel.IsExpanded)
			{
				return false;
			}

			if (presetFolderViewModel.Items.Items.Contains(this.presetsService.SelectedPreset))
			{
				this.presetsService.SelectedPreset.IsSelected = true;
				return true;
			}

			foreach (var subFolder in presetFolderViewModel.SubFolders.Items)
			{
				if (this.ReselectPresetOnExpanded(subFolder))
				{
					return true;
				}
			}

			return false;
		}
	}
}
