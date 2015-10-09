using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Messaging;
using VidCoder.Messages;
using VidCoder.Model;
using VidCoder.Resources;
using VidCoder.Services.Windows;
using VidCoder.ViewModel;
using VidCoderCommon.Model;

namespace VidCoder.Services
{
	/// <summary>
	/// Controls creation/modification/deletion/import/export of presets.
	/// </summary>
	public class PresetsService : ViewModelBase
	{
		private MainViewModel main = Ioc.Get<MainViewModel>();
		private OutputPathService outputPathService;
		private IWindowManager windowManager = Ioc.Get<IWindowManager>();

		private PresetViewModel selectedPreset;

		private IList<Preset> builtInPresets;
		private ObservableCollection<PresetViewModel> allPresets;

		public PresetsService()
		{
			this.builtInPresets = PresetStorage.BuiltInPresets;
			List<Preset> userPresets = PresetStorage.UserPresets;

			this.allPresets = new ObservableCollection<PresetViewModel>();
			var unmodifiedPresets = userPresets.Where(preset => !preset.IsModified);
			Preset modifiedPreset = userPresets.FirstOrDefault(preset => preset.IsModified);
			int modifiedPresetIndex = -1;

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

			foreach (Preset builtInPreset in this.builtInPresets)
			{
				PresetViewModel presetVM;
				if (modifiedPreset != null && modifiedPreset.Name == builtInPreset.Name)
				{
					presetVM = new PresetViewModel(modifiedPreset);
					presetVM.OriginalProfile = builtInPreset.EncodingProfile;
				}
				else
				{
					presetVM = new PresetViewModel(builtInPreset);
				}

				this.allPresets.Add(presetVM);
			}

			// Always select the modified preset if it exists.
			// Otherwise, choose the last selected preset.
			int presetIndex;
			if (modifiedPresetIndex >= 0)
			{
				presetIndex = modifiedPresetIndex;
			}
			else
			{
				presetIndex = Config.LastPresetIndex;
			}

			if (presetIndex >= this.allPresets.Count)
			{
				presetIndex = 0;
			}

			this.SelectedPreset = this.allPresets[presetIndex];
		}

		private OutputPathService OutputPathVM
		{
			get
			{
				if (this.outputPathService == null)
				{
					this.outputPathService = Ioc.Get<OutputPathService>();
				}

				return this.outputPathService;
			}
		}

		public ObservableCollection<PresetViewModel> AllPresets
		{
			get
			{
				return this.allPresets;
			}
		}

		public bool AutomaticChange { get; set; }

		public PresetViewModel SelectedPreset
		{
			get
			{
				return this.selectedPreset;
			}

			set
			{
				if (value == null)
				{
					return;
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
						dialogMessage = string.Format(MainRes.PresetDiscardConfirmMessage, MainRes.PresetWord);
						dialogTitle = MainRes.PresetDiscardConfirmTitle;
						buttons = MessageBoxButton.OKCancel;
					}
					else
					{
						dialogMessage = string.Format(MainRes.SaveConfirmMessage, MainRes.PresetWord);
						dialogTitle = string.Format(MainRes.SaveConfirmTitle, MainRes.PresetWord);
						buttons = MessageBoxButton.YesNoCancel;
					}

					MessageBoxResult dialogResult = Utilities.MessageBox.Show(
						this.main,
						dialogMessage,
						dialogTitle, 
						buttons);
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

				if (changeSelectedPreset)
				{
					this.NotifySelectedPresetChanged();

					// If we're switching away from a temporary queue preset, remove it.
					if (previouslySelectedPreset != null && previouslySelectedPreset.Preset.IsQueue && previouslySelectedPreset != value)
					{
						this.AllPresets.Remove(previouslySelectedPreset);
					}
				}
			}
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

			this.main.StartAnimation("PresetGlowHighlight");
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

			this.main.StartAnimation("PresetGlowHighlight");
			this.SaveUserPresets();
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

				this.main.StartAnimation("PresetGlowHighlight");
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

			if (userInitiated)
			{
				this.main.StartAnimation("PresetGlowHighlight");
			}

			this.SaveUserPresets();

			// Refresh file name.
			this.OutputPathVM.GenerateOutputFileName();

			this.main.RefreshChapterMarkerUI();
		}

		/// <summary>
		/// Deletes the current preset.
		/// </summary>
		public void DeletePreset()
		{
			this.AllPresets.Remove(this.SelectedPreset);
			this.selectedPreset = null;
			this.SelectedPreset = this.AllPresets[0];

			this.main.StartAnimation("PresetGlowHighlight");
			this.SaveUserPresets();

			this.main.RefreshChapterMarkerUI();
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
		}

		public void InsertQueuePreset(PresetViewModel queuePreset)
		{
			// Bring in encoding profile and put in a placeholder preset.
			if (this.AllPresets[0].Preset.IsQueue)
			{
				this.AllPresets.RemoveAt(0);
			}

			this.AllPresets.Insert(0, queuePreset);

			this.selectedPreset = queuePreset;
			this.NotifySelectedPresetChanged();
		}

		private void InsertNewPreset(PresetViewModel presetVM)
		{
			for (int i = 0; i < this.AllPresets.Count; i++)
			{
				if (this.AllPresets[i].Preset.IsBuiltIn)
				{
					this.AllPresets.Insert(i, presetVM);
					return;
				}

				if (string.CompareOrdinal(presetVM.Preset.Name, this.AllPresets[i].Preset.Name) < 0)
				{
					this.AllPresets.Insert(i, presetVM);
					return;
				}
			}

			Trace.Assert(true, "Did not find place to insert new preset.");
		}

		private void NotifySelectedPresetChanged()
		{
			this.OutputPathVM.GenerateOutputFileName();

			this.RaisePropertyChanged(() => this.SelectedPreset);
			this.main.RefreshChapterMarkerUI();

			Config.LastPresetIndex = this.AllPresets.IndexOf(this.selectedPreset);
		}
	}
}
