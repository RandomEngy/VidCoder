using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using HandBrake.Interop.Interop;
using HandBrake.Interop.Interop.Json.Presets;
using Microsoft.AnyContainer;
using ReactiveUI;
using VidCoder;
using VidCoder.Model;
using VidCoder.Resources;
using VidCoder.Services;
using VidCoder.Services.Windows;
using VidCoder.ViewModel;
using VidCoder.ViewModel.DataModels;
using VidCoderCommon.Model;

namespace VidCoder.Services
{
	/// <summary>
	/// Controls creation/modification/deletion/import/export of presets.
	/// </summary>
	public class PresetsService : ReactiveObject
	{
		private const string CustomFolderKey = "custom";
		private const string BuiltInFolderKey = "builtIn";

		private OutputPathService outputPathService;

		private PresetViewModel selectedPreset;

		private PresetFolderViewModel customPresetFolder;
		private PresetFolderViewModel builtInFolder;

		private PreviewUpdateService previewUpdateService = StaticResolver.Resolve<PreviewUpdateService>();

		private bool tryChangePresetDialogOpen;

		/// <summary>
		/// List of folders from the database. Only used when first building the tree.
		/// </summary>
		private IList<PresetFolder> presetFolders;

		private HashSet<string> collapsedBuiltInFolders;

		public event EventHandler PresetChanged;

		public event EventHandler<PresetFolderViewModel> PresetFolderManuallyExpanded;

		private bool isAutomaticExpansion = false;

		public PresetsService()
		{
			List<Preset> userPresets = PresetStorage.UserPresets;
			this.presetFolders = PresetFolderStorage.PresetFolders;
			this.collapsedBuiltInFolders = CustomConfig.CollapsedBuiltInFolders;

			var unmodifiedPresets = userPresets.Where(preset => !preset.IsModified);
			Preset modifiedPreset = userPresets.FirstOrDefault(preset => preset.IsModified);

			this.allPresets = new ObservableCollection<PresetViewModel>();
			int modifiedPresetIndex = -1;
			int defaultPresetIndex = 0;

			foreach (Preset userPreset in unmodifiedPresets)
			{
				PresetViewModel presetVM;
				if (modifiedPreset != null && modifiedPreset.Name == userPreset.Name)
				{
					modifiedPresetIndex = this.allPresets.Count;
					presetVM = new PresetViewModel(modifiedPreset);
					presetVM.OriginalProfile = userPreset.EncodingProfile;
				}
				else
				{
					presetVM = new PresetViewModel(userPreset);
				}

				this.allPresets.Add(presetVM);
			}

			// Populate the custom preset folder before built-in presets are added to AllPresets collection.
			this.customPresetFolder = new PresetFolderViewModel(this, !this.collapsedBuiltInFolders.Contains(CustomFolderKey), isBuiltIn: false, id: 0) { Name = EncodingRes.PresetFolder_Custom };
			this.PopulateCustomFolder(this.customPresetFolder);

			// Populate built-in folder from HandBrake presets
			IList<PresetCategory> handBrakePresets = HandBrakePresetService.GetBuiltInPresets();
			this.builtInFolder = new PresetFolderViewModel(this, !this.collapsedBuiltInFolders.Contains(BuiltInFolderKey), isBuiltIn: true) { Name = EncodingRes.PresetFolder_BuiltIn };
			foreach (PresetCategory handbrakePresetCategory in handBrakePresets)
			{
				var builtInSubfolder = new PresetFolderViewModel(this, !this.collapsedBuiltInFolders.Contains(handbrakePresetCategory.PresetName), isBuiltIn: true)
				{
					Name = handbrakePresetCategory.PresetName,
				};

				this.builtInFolder.AddSubfolder(builtInSubfolder);

				foreach (HBPreset handbrakePreset in handbrakePresetCategory.ChildrenArray)
				{
					if (handbrakePreset.Default)
					{
						defaultPresetIndex = this.allPresets.Count;
					}

					Preset builtInPreset = PresetConverter.ConvertHandBrakePresetToVC(handbrakePreset);
					PresetViewModel builtInPresetViewModel = new PresetViewModel(builtInPreset);

					this.allPresets.Add(builtInPresetViewModel);
					builtInSubfolder.AddItem(builtInPresetViewModel);
				}
			}

			this.allPresetsTree = new ObservableCollection<PresetFolderViewModel>();

			if (this.customPresetFolder.Items.Count > 0 || this.customPresetFolder.SubFolders.Count > 0)
			{
				this.AllPresetsTree.Add(this.customPresetFolder);
			}

			this.AllPresetsTree.Add(this.builtInFolder);



			// Always select the modified preset if it exists.
			// Otherwise, choose the last selected preset.
			int presetIndex;
			if (modifiedPresetIndex >= 0)
			{
				presetIndex = modifiedPresetIndex;
			}
			else
			{
				int lastPresetIndex = Config.LastPresetIndex;
				if (lastPresetIndex >= 0)
				{
					presetIndex = lastPresetIndex;
				}
				else
				{
					presetIndex = defaultPresetIndex;
				}
			}

			if (presetIndex >= this.allPresets.Count)
			{
				presetIndex = 0;
			}

			this.SelectedPreset = this.allPresets[presetIndex];
		}

		private void PopulateCustomFolder(PresetFolderViewModel folderViewModel)
		{
			// Add all child folders
			var childFolders = this.presetFolders.Where(f => f.ParentId == folderViewModel.Id);
			foreach (PresetFolder childPresetFolder in childFolders)
			{
				var childFolderViewModel = PresetFolderViewModel.FromPresetFolder(childPresetFolder, this);
				this.PopulateCustomFolder(childFolderViewModel);
				folderViewModel.AddSubfolder(childFolderViewModel);
			}

			// Add all presets directly in folder
			var folderPresets = this.AllPresets.Where(p => p.Preset.FolderId == folderViewModel.Id);
			foreach (PresetViewModel presetViewModel in folderPresets)
			{
				folderViewModel.AddItem(presetViewModel);
			}
		}

		private OutputPathService OutputPathService
		{
			get
			{
				if (this.outputPathService == null)
				{
					this.outputPathService = StaticResolver.Resolve<OutputPathService>();
				}

				return this.outputPathService;
			}
		}

		private ObservableCollection<PresetViewModel> allPresets;
		public ObservableCollection<PresetViewModel> AllPresets => this.allPresets;

		private ObservableCollection<PresetFolderViewModel> allPresetsTree;
		public ObservableCollection<PresetFolderViewModel> AllPresetsTree => this.allPresetsTree;

		public bool AutomaticChange { get; set; }

		public PresetViewModel SelectedPreset
		{
			get
			{
				return this.selectedPreset;
			}

			set
			{
				this.TryUpdateSelectedPreset(value);
			}
		}

		public bool TryUpdateSelectedPreset(PresetViewModel value)
		{
			if (value == null || this.selectedPreset == value || this.tryChangePresetDialogOpen)
			{
				return false;
			}

			PresetViewModel previouslySelectedPreset = this.selectedPreset;
			bool changeSelectedPreset = true;

			if (this.selectedPreset != null && this.selectedPreset.Preset.IsModified)
			{
				string dialogMessage;
				string dialogTitle;
				MessageBoxButton buttons;

				if (this.selectedPreset.Preset.IsBuiltIn)
				{
					dialogMessage = MainRes.PresetDiscardConfirmMessage;
					dialogTitle = MainRes.PresetDiscardConfirmTitle;
					buttons = MessageBoxButton.OKCancel;
				}
				else
				{
					dialogMessage = MainRes.SaveChangesPresetMessage;
					dialogTitle = MainRes.SaveChangesPresetTitle;
					buttons = MessageBoxButton.YesNoCancel;
				}

				this.tryChangePresetDialogOpen = true;

				MessageBoxResult dialogResult = Utilities.MessageBox.Show(
					StaticResolver.Resolve<MainViewModel>(),
					dialogMessage,
					dialogTitle,
					buttons);

				this.tryChangePresetDialogOpen = false;

				if (dialogResult == MessageBoxResult.Yes)
				{
					// Yes, we wanted to save changes
					this.SavePreset();
				}
				else if (dialogResult == MessageBoxResult.No || dialogResult == MessageBoxResult.OK)
				{
					// No, we didn't want to save changes or OK, we wanted to discard changes.
					this.RevertPreset(userInitiated: false);
				}
				else if (dialogResult == MessageBoxResult.Cancel)
				{
					// Queue up an action to switch back to this preset.
					int currentPresetIndex = this.AllPresets.IndexOf(this.selectedPreset);

					DispatchUtilities.BeginInvoke(() =>
					{
						this.SelectedPreset = this.AllPresets[currentPresetIndex];
					});

					changeSelectedPreset = false;
				}
			}

			this.selectedPreset = value;
			this.selectedPreset.IsSelected = true; // For TreeView

			if (changeSelectedPreset)
			{
				this.NotifySelectedPresetChanged();

				if (previouslySelectedPreset != null)
				{
					Config.LastPresetIndex = this.AllPresets.IndexOf(this.selectedPreset);
				}

				// If we're switching away from a temporary queue preset, remove it.
				if (previouslySelectedPreset != null && previouslySelectedPreset.Preset.IsQueue && previouslySelectedPreset != value)
				{
					this.RemovePresetFromTreeAndList(previouslySelectedPreset);
				}
			}

			return changeSelectedPreset;
		}

		public VCProfile GetProfileByName(string presetName)
		{
			foreach (var preset in this.allPresets)
			{
				if (string.Compare(presetName.Trim(), preset.DisplayName.Trim(), ignoreCase: true, culture: CultureInfo.CurrentUICulture) == 0)
				{
					if (preset.Preset.IsModified)
					{
						return preset.OriginalProfile;
					}
					else
					{
						return preset.Preset.EncodingProfile;
					}
				}
			}

			return null;
		}

		public void SavePreset()
		{
			if (this.SelectedPreset.Preset.IsModified)
			{
				this.SelectedPreset.OriginalProfile = null;
				this.SelectedPreset.Preset.IsModified = false;
			}

			this.SaveUserPresets();
		}

		public void SavePresetAs(string newPresetName)
		{
			var newPreset = new Preset
			{
				IsBuiltIn = false,
				IsModified = false,
				Name = newPresetName,
				EncodingProfile = this.SelectedPreset.Preset.EncodingProfile.Clone()
			};

			var newPresetVM = new PresetViewModel(newPreset);

			this.InsertNewPreset(newPresetVM);

			if (this.SelectedPreset.Preset.IsModified)
			{
				this.RevertPreset(userInitiated: false);
			}

			this.selectedPreset = null;
			this.SelectedPreset = newPresetVM;

			this.SaveUserPresets();
		}

		public void RenamePreset(string newPresetName)
		{
			this.SelectedPreset.Preset.Name = newPresetName;

			// Remove from the folder and add it back again so it goes in the right place.
			PresetFolderViewModel currentFolder = this.FindFolderViewModel(this.SelectedPreset.Preset.FolderId);
			currentFolder.RemoveItem(this.SelectedPreset);
			currentFolder.AddItem(this.SelectedPreset);

			this.SavePreset();
		}

		public void AddPreset(Preset newPreset)
		{
			var newPresetVM = new PresetViewModel(newPreset);

			this.InsertNewPreset(newPresetVM);

			// Switch to the new preset if we can do it cleanly.
			if (!this.SelectedPreset.Preset.IsModified)
			{
				this.selectedPreset = null;
				this.SelectedPreset = newPresetVM;
			}

			this.SaveUserPresets();
		}

		/// <summary>
		/// Reverts changes to the current preset back to last saved.
		/// </summary>
		public void RevertPreset(bool userInitiated)
		{
			Trace.Assert(this.SelectedPreset.OriginalProfile != null, "Error reverting preset: Original profile cannot be null.");
			Trace.Assert(this.SelectedPreset.OriginalProfile != this.SelectedPreset.Preset.EncodingProfile, "Error reverting preset: Original profile must be different from current profile.");

			if (this.SelectedPreset.OriginalProfile == null || this.SelectedPreset.OriginalProfile == this.SelectedPreset.Preset.EncodingProfile)
			{
				return;
			}

			this.SelectedPreset.Preset.EncodingProfile = this.SelectedPreset.OriginalProfile;
			this.SelectedPreset.OriginalProfile = null;
			this.SelectedPreset.Preset.IsModified = false;

			this.SaveUserPresets();

			// Refresh file name.
			this.OutputPathService.GenerateOutputFileName();
		}

		/// <summary>
		/// Deletes the current preset.
		/// </summary>
		public void DeletePreset()
		{
			PresetViewModel presetToDelete = this.SelectedPreset;

			this.RemovePresetFromTreeAndList(presetToDelete);

			this.selectedPreset = null;
			this.SelectedPreset = this.AllPresets[0];

			this.SaveUserPresets();
		}

		private void RemovePresetFromTreeAndList(PresetViewModel presetToRemove)
		{
			this.RemovePresetFromTree(presetToRemove);
			this.AllPresets.Remove(presetToRemove);
		}

		public void MovePresetToFolder(PresetViewModel presetViewModel, PresetFolderViewModel targetFolder)
		{
			PresetFolderViewModel previousFolder = this.FindFolderViewModel(presetViewModel.Preset.FolderId);
			previousFolder.RemoveItem(presetViewModel);

			targetFolder.AddItem(presetViewModel);
			presetViewModel.Preset.FolderId = targetFolder.Id;

			targetFolder.IsExpanded = true;

			this.SaveUserPresets();
		}

		public void CreateSubFolder(PresetFolderViewModel folderViewModel)
		{
			var dialogVM = new ChooseNameViewModel(EncodingRes.ChooseNameSubfolder, new List<string>());
			dialogVM.Name = EncodingRes.DefaultPresetFolderName;
			var windowManager = StaticResolver.Resolve<IWindowManager>();
			windowManager.OpenDialog(dialogVM, windowManager.Find<EncodingWindowViewModel>());

			if (dialogVM.DialogResult)
			{
				string subfolderName = dialogVM.Name;

				PresetFolder newFolder = PresetFolderStorage.AddFolder(subfolderName, folderViewModel.Id);
				folderViewModel.AddSubfolder(PresetFolderViewModel.FromPresetFolder(newFolder, this));
			}
		}

		public void RenameFolder(PresetFolderViewModel folderViewModel)
		{
			var dialogVM = new ChooseNameViewModel(EncodingRes.ChooseNewFolderName, new List<string>());
			dialogVM.Name = folderViewModel.Name;
			var windowManager = StaticResolver.Resolve<IWindowManager>();
			windowManager.OpenDialog(dialogVM, windowManager.Find<EncodingWindowViewModel>());

			if (dialogVM.DialogResult)
			{
				string newName = dialogVM.Name;
				if (newName != folderViewModel.Name)
				{
					PresetFolderStorage.RenameFolder(folderViewModel.Id, newName);
					folderViewModel.Name = newName;

					// Remove and re-add the folder to get the folder in the right order.
					PresetFolderViewModel parentFolder = folderViewModel.Parent;
					parentFolder.RemoveSubfolder(folderViewModel);
					parentFolder.AddSubfolder(folderViewModel);
				}
			}
		}

		public void RemoveFolder(PresetFolderViewModel folderViewModel)
		{
			PresetFolderStorage.RemoveFolder(folderViewModel.Id);
			this.RemoveFolderFromTree(this.customPresetFolder, folderViewModel);
		}

		public void SaveFolderIsExpanded(PresetFolderViewModel folderViewModel)
		{
			if (folderViewModel.Id > 0)
			{
				// If ID > 0 then this is a custom preset folder.
				PresetFolderStorage.SetFolderIsExpanded(folderViewModel.Id, folderViewModel.IsExpanded);
			}
			else
			{
				// If not stored in preset folder table, save in config.
				string key;
				if (folderViewModel == this.customPresetFolder)
				{
					key = CustomFolderKey;
				}
				else if (folderViewModel == this.builtInFolder)
				{
					key = BuiltInFolderKey;
				}
				else
				{
					key = folderViewModel.Name;
				}

				if (folderViewModel.IsExpanded)
				{
					if (this.collapsedBuiltInFolders.Contains(key))
					{
						this.collapsedBuiltInFolders.Remove(key);
					}
				}
				else
				{
					if (!this.collapsedBuiltInFolders.Contains(key))
					{
						this.collapsedBuiltInFolders.Add(key);
					}
				}

				CustomConfig.CollapsedBuiltInFolders = this.collapsedBuiltInFolders;
			}
		}

		public void ReportFolderExpanded(PresetFolderViewModel presetFolderViewModel)
		{
			if (!this.isAutomaticExpansion)
			{
				this.PresetFolderManuallyExpanded?.Invoke(this, presetFolderViewModel);
			}
		}

		public void HandleKey(KeyEventArgs args)
		{
			if (args.Key == Key.Up)
			{
				int selectedIndex = this.AllPresets.IndexOf(this.SelectedPreset);
				if (selectedIndex > 0)
				{
					this.SelectedPreset = this.AllPresets[selectedIndex - 1];
					this.EnsurePresetVisible(this.SelectedPreset);

					args.Handled = true;
				}
			}
			else if (args.Key == Key.Down)
			{
				int selectedIndex = this.AllPresets.IndexOf(this.SelectedPreset);
				if (selectedIndex < this.AllPresets.Count - 1)
				{
					this.SelectedPreset = this.AllPresets[selectedIndex + 1];
					this.EnsurePresetVisible(this.SelectedPreset);

					args.Handled = true;
				}
			}
		}

		private void EnsurePresetVisible(PresetViewModel presetViewModel)
		{
			this.EnsurePresetFolderExpanded(presetViewModel.Parent);
		}

		private void EnsurePresetFolderExpanded(PresetFolderViewModel presetFolderViewModel)
		{
			if (presetFolderViewModel == null)
			{
				return;
			}

			this.isAutomaticExpansion = true;
			presetFolderViewModel.IsExpanded = true;
			this.isAutomaticExpansion = false;

			this.EnsurePresetFolderExpanded(presetFolderViewModel.Parent);
		}

		private PresetFolderViewModel FindFolderViewModel(long folderId)
		{
			return this.FindFolderRecursive(this.customPresetFolder, folderId);
		}

		private PresetFolderViewModel FindFolderRecursive(PresetFolderViewModel currentFolder, long folderId)
		{
			if (currentFolder.Id == folderId)
			{
				return currentFolder;
			}

			foreach (PresetFolderViewModel subFolder in currentFolder.SubFolders.Items)
			{
				PresetFolderViewModel childResult = this.FindFolderRecursive(subFolder, folderId);
				if (childResult != null)
				{
					return childResult;
				}
			}

			return null;
		}

		private bool RemoveFolderFromTree(PresetFolderViewModel folderToSearchIn, PresetFolderViewModel folderToRemove)
		{
			if (folderToSearchIn.SubFolders.Items.Contains(folderToRemove))
			{
				folderToSearchIn.RemoveSubfolder(folderToRemove);
				return true;
			}

			foreach (PresetFolderViewModel folder in folderToSearchIn.SubFolders.Items)
			{
				if (this.RemoveFolderFromTree(folder, folderToRemove))
				{
					return true;
				}
			}

			return false;
		}

		private void RemovePresetFromTree(PresetViewModel presetViewModel)
		{
			foreach (PresetFolderViewModel folder in this.AllPresetsTree)
			{
				if (this.RemovePresetFromFolder(folder, presetViewModel))
				{
					return;
				}
			}
		}

		private bool RemovePresetFromFolder(PresetFolderViewModel folder, PresetViewModel presetViewModel)
		{
			if (folder.Items.Items.Contains(presetViewModel))
			{
				folder.RemoveItem(presetViewModel);
				return true;
			}

			foreach (var subFolder in folder.SubFolders.Items)
			{
				if (this.RemovePresetFromFolder(subFolder, presetViewModel))
				{
					return true;
				}
			}

			return false;
		}

		/// <summary>
		/// Prepares to modify the current preset. Called when just before making a change to a preset.
		/// </summary>
		/// <param name="newProfile">The new encoding profile to use.</param>
		public void PrepareModifyPreset(VCProfile newProfile)
		{
			Trace.Assert(!this.SelectedPreset.Preset.IsModified, "Cannot start modification on already modified preset.");
			Trace.Assert(this.SelectedPreset.OriginalProfile == null, "Preset already has OriginalProfile.");

			if (this.SelectedPreset.Preset.IsModified || this.SelectedPreset.OriginalProfile != null)
			{
				return;
			}

			this.SelectedPreset.OriginalProfile = this.SelectedPreset.Preset.EncodingProfile;
			this.SelectedPreset.Preset.SetEncodingProfileSilent(newProfile);
			this.SelectedPreset.Preset.IsModified = true;
		}

		/// <summary>
		/// Finalizes the preset modification.
		/// </summary>
		public void FinalizeModifyPreset()
		{
			this.SelectedPreset.Preset.RaiseEncodingProfile();
		}

		public void SaveUserPresets()
		{
			List<Preset> userPresets = new List<Preset>();

			foreach (PresetViewModel presetVM in this.AllPresets)
			{
				if ((!presetVM.Preset.IsBuiltIn || presetVM.Preset.IsModified) && !presetVM.Preset.IsQueue)
				{
					userPresets.Add(presetVM.Preset);
				}

				// Add the original version of the preset, if we're working with a more recent version.
				if (!presetVM.Preset.IsBuiltIn && presetVM.Preset.IsModified)
				{
					Trace.Assert(presetVM.OriginalProfile != null, "Error saving user presets: Preset marked as modified but no OriginalProfile could be found.");
					if (presetVM.OriginalProfile != null)
					{
						var originalPreset = new Preset
						{
							Name = presetVM.Preset.Name,
							IsBuiltIn = presetVM.Preset.IsBuiltIn,
							IsModified = false,
							EncodingProfile = presetVM.OriginalProfile
						};

						userPresets.Add(originalPreset);
					}
				}
			}

			PresetStorage.UserPresets = userPresets;

			this.PresetChanged?.Invoke(this, EventArgs.Empty);
		}

		public void InsertQueuePreset(PresetViewModel queuePreset)
		{
			// Bring in encoding profile and put in a placeholder preset.

			if (this.AllPresets[0].Preset.IsQueue)
			{
				this.RemovePresetFromTreeAndList(this.AllPresets[0]);
			}

			this.InsertNewPreset(queuePreset, insertAtStart: true);

			this.selectedPreset = null;
			this.SelectedPreset = queuePreset;
		}

		private void InsertNewPreset(PresetViewModel presetVM, bool insertAtStart = false)
		{
			this.customPresetFolder.AddItem(presetVM);

			// If the Custom folder hasn't been added to the tree, add it now.
			if (!this.AllPresetsTree.Contains(this.customPresetFolder))
			{
				this.AllPresetsTree.Insert(0, this.customPresetFolder);
			}

			if (insertAtStart)
			{
				this.AllPresets.Insert(0, presetVM);
			}

			for (int i = 0; i < this.AllPresets.Count; i++)
			{
				if (this.AllPresets[i].Preset.IsBuiltIn)
				{
					this.AllPresets.Insert(i, presetVM);
					return;
				}

				if (string.Compare(presetVM.Preset.Name, this.AllPresets[i].Preset.Name, StringComparison.CurrentCultureIgnoreCase) < 0)
				{
					this.AllPresets.Insert(i, presetVM);
					return;
				}
			}

			Trace.Assert(true, "Did not find place to insert new preset.");
		}

		private void NotifySelectedPresetChanged()
		{
			this.OutputPathService.GenerateOutputFileName();
			this.RaisePropertyChanged(nameof(this.SelectedPreset));
			this.previewUpdateService.RefreshPreview();
		}
	}
}
