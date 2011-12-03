using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Messaging;
using HandBrake.Interop.Model.Encoding;
using Microsoft.Practices.Unity;
using VidCoder.Messages;
using VidCoder.Model;
using VidCoder.Properties;
using VidCoder.Services;

namespace VidCoder.ViewModel.Components
{
	/// <summary>
	/// Controls creation/modification/deletion/import/export of presets.
	/// </summary>
	public class PresetsViewModel : ViewModelBase
	{
		private MainViewModel main = Unity.Container.Resolve<MainViewModel>();
		private OutputPathViewModel outputPathVM;

		private PresetViewModel selectedPreset;

		private List<Preset> builtInPresets;
		private ObservableCollection<PresetViewModel> allPresets;

		public PresetsViewModel()
		{
			this.builtInPresets = Presets.BuiltInPresets;
			List<Preset> userPresets = Presets.UserPresets;

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
				presetIndex = Settings.Default.LastPresetIndex;
			}

			if (presetIndex >= this.allPresets.Count)
			{
				presetIndex = 0;
			}

			this.SelectedPreset = this.allPresets[presetIndex];
		}

		private OutputPathViewModel OutputPathVM
		{
			get
			{
				if (this.outputPathVM == null)
				{
					this.outputPathVM = Unity.Container.Resolve<OutputPathViewModel>();
				}

				return this.outputPathVM;
			}
		}

		public ObservableCollection<PresetViewModel> AllPresets
		{
			get
			{
				return this.allPresets;
			}
		}

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
					MessageBoxResult dialogResult = Utilities.MessageBox.Show(this.main, "Do you want to save changes to your current preset?", "Save current preset?", MessageBoxButton.YesNoCancel);
					if (dialogResult == MessageBoxResult.Yes)
					{
						this.SavePreset();
					}
					else if (dialogResult == MessageBoxResult.No)
					{
						this.RevertPreset(userInitiated: false);
					}
					else if (dialogResult == MessageBoxResult.Cancel)
					{
						// Queue up an action to switch back to this preset.
						int currentPresetIndex = this.AllPresets.IndexOf(this.selectedPreset);

						DispatchService.BeginInvoke(() =>
						{
							this.SelectedPreset = this.AllPresets[currentPresetIndex];
						});

						changeSelectedPreset = false;
					}
				}

				this.selectedPreset = value;

				if (changeSelectedPreset)
				{
					NotifySelectedPresetChanged();

					// If we're switching away from a temporary queue preset, remove it.
					if (previouslySelectedPreset != null && previouslySelectedPreset.IsQueue && previouslySelectedPreset != value)
					{
						this.AllPresets.Remove(previouslySelectedPreset);
					}
				}
			}
		}

		public void SavePreset()
		{
			if (this.SelectedPreset.IsModified)
			{
				this.SelectedPreset.OriginalProfile = null;
				this.SelectedPreset.Preset.IsModified = false;
			}

			// Refresh view and save in case of rename.
			this.SelectedPreset.RefreshView();
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

			if (this.SelectedPreset.IsModified)
			{
				this.RevertPreset(userInitiated: false);
				this.SelectedPreset.RefreshView();
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
			if (!this.SelectedPreset.IsModified)
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
			Debug.Assert(this.SelectedPreset.OriginalProfile != null, "Error reverting preset: Original profile cannot be null.");
			Debug.Assert(this.SelectedPreset.OriginalProfile != this.SelectedPreset.Preset.EncodingProfile, "Error reverting preset: Original profile must be different from current profile.");

			this.SelectedPreset.Preset.EncodingProfile = this.SelectedPreset.OriginalProfile;
			this.SelectedPreset.OriginalProfile = null;
			this.SelectedPreset.Preset.IsModified = false;
			this.SelectedPreset.RefreshView();

			if (userInitiated)
			{
				this.main.StartAnimation("PresetGlowHighlight");
			}

			this.SaveUserPresets();

			// Refresh file name.
			this.OutputPathVM.GenerateOutputFileName();

			this.main.RefreshChapterMarkerUI();
			Messenger.Default.Send(new RefreshPreviewMessage());
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
			Messenger.Default.Send(new RefreshPreviewMessage());
		}

		/// <summary>
		/// Modify the current preset. Called when first making a change to a preset.
		/// </summary>
		/// <param name="newProfile">The new encoding profile to use.</param>
		public void ModifyPreset(EncodingProfile newProfile)
		{
			Debug.Assert(this.SelectedPreset.IsModified == false, "Cannot start modification on already modified preset.");
			Debug.Assert(this.SelectedPreset.OriginalProfile == null, "Preset already has OriginalProfile.");

			this.SelectedPreset.OriginalProfile = this.SelectedPreset.Preset.EncodingProfile;
			this.SelectedPreset.Preset.EncodingProfile = newProfile;
			this.SelectedPreset.Preset.IsModified = true;
			this.SelectedPreset.RefreshView();
		}

		public void SaveUserPresets()
		{
			List<Preset> userPresets = new List<Preset>();

			foreach (PresetViewModel presetVM in this.AllPresets)
			{
				if ((!presetVM.Preset.IsBuiltIn || presetVM.Preset.IsModified) && !presetVM.IsQueue)
				{
					userPresets.Add(presetVM.Preset);
				}

				// Add the original version of the preset, if we're working with a more recent version.
				if (!presetVM.Preset.IsBuiltIn && presetVM.Preset.IsModified)
				{
					Debug.Assert(presetVM.OriginalProfile != null, "Error saving user presets: Preset marked as modified but no OriginalProfile could be found.");

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

			Presets.UserPresets = userPresets;
		}

		public void InsertQueuePreset(PresetViewModel queuePreset)
		{
			// Bring in encoding profile and put in a placeholder preset.
			if (this.AllPresets[0].IsQueue)
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
				if (this.AllPresets[i].IsBuiltIn)
				{
					this.AllPresets.Insert(i, presetVM);
					return;
				}

				if (string.CompareOrdinal(presetVM.PresetName, this.AllPresets[i].PresetName) < 0)
				{
					this.AllPresets.Insert(i, presetVM);
					return;
				}
			}

			Debug.Assert(true, "Did not find place to insert new preset.");
		}

		private void NotifySelectedPresetChanged()
		{
			this.OutputPathVM.GenerateOutputFileName();

			var encodingWindow = WindowManager.FindWindow(typeof(EncodingViewModel)) as EncodingViewModel;
			if (encodingWindow != null)
			{
				encodingWindow.EditingPreset = this.selectedPreset.Preset;
			}

			Messenger.Default.Send(new RefreshPreviewMessage());

			this.RaisePropertyChanged(() => this.SelectedPreset);
			this.main.RefreshChapterMarkerUI();

			Settings.Default.LastPresetIndex = this.AllPresets.IndexOf(this.selectedPreset);
		}
	}
}
