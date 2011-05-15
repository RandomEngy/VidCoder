using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VidCoder.Model;
using HandBrake.SourceData;
using System.Collections.ObjectModel;
using System.Windows.Input;
using VidCoder.Services;
using System.Windows.Media.Imaging;
using System.ComponentModel;
using HandBrake.Interop;
using System.Windows;
using Microsoft.Practices.Unity;

namespace VidCoder.ViewModel
{
	public class EncodingViewModel : OkCancelDialogViewModel
	{
		private MainViewModel mainViewModel = Unity.Container.Resolve<MainViewModel>();

		private EncodingProfile profile;

		private bool mp4ExtensionEnabled;

		private Preset originalPreset;
		private bool isBuiltIn;

		private ICommand saveCommand;
		private ICommand saveAsCommand;
		private ICommand renameCommand;
		private ICommand deletePresetCommand;
		private ICommand previewCommand;

		public EncodingViewModel(Preset preset)
		{
			this.PicturePanelViewModel = new PicturePanelViewModel(this);
			this.VideoFiltersPanelViewModel = new VideoFiltersPanelViewModel(this);
			this.VideoPanelViewModel = new VideoPanelViewModel(this);
			this.AudioPanelViewModel = new AudioPanelViewModel(this);
			this.AdvancedPanelViewModel = new AdvancedPanelViewModel(this);

			this.EditingPreset = preset;
			this.mainViewModel.PropertyChanged += this.OnMainPropertyChanged;
			this.mainViewModel.AudioChoices.CollectionChanged += this.AudioChoicesCollectionChanged;
		}

		public MainViewModel MainViewModel
		{
			get
			{
				return this.mainViewModel;
			}
		}

		public PicturePanelViewModel PicturePanelViewModel { get; set; }
		public VideoFiltersPanelViewModel VideoFiltersPanelViewModel { get; set; }
		public VideoPanelViewModel VideoPanelViewModel { get; set; }
		public AudioPanelViewModel AudioPanelViewModel { get; set; }
		public AdvancedPanelViewModel AdvancedPanelViewModel { get; set; }

		public Preset EditingPreset
		{
			get
			{
				return this.originalPreset;
			}

			set
			{
				if (value.IsModified)
				{
					// If already modified, use the existing profile.
					this.profile = value.EncodingProfile;
				}
				else
				{
					// If not modified, clone the profile
					this.profile = value.EncodingProfile.Clone();
				}

				this.originalPreset = value;

				this.IsBuiltIn = value.IsBuiltIn;

				this.VideoPanelViewModel.NotifyProfileChanged();
				this.AudioPanelViewModel.NotifyProfileChanged();

				this.PicturePanelViewModel.RefreshOutputSize();
				
				this.AdvancedPanelViewModel.UpdateUIFromAdvancedOptions();

				this.NotifyAllChanged();
			}
		}

		public EncodingProfile EncodingProfile
		{
			get
			{
				return this.profile;
			}
		}

		public string WindowTitle
		{
			get
			{
				string windowTitle = "Preset: " + this.ProfileName;
				if (this.IsModified)
				{
					windowTitle += " *";
				}

				return windowTitle;
			}
		}

		public Title SelectedTitle
		{
			get
			{
				return this.mainViewModel.SelectedTitle;
			}
		}

		public string ProfileName
		{
			get
			{
				return this.originalPreset.Name;
			}
		}

		public bool IsBuiltIn
		{
			get
			{
				return this.isBuiltIn;
			}

			set
			{
				this.isBuiltIn = value;
				this.NotifyPropertyChanged("IsBuiltIn");
				this.NotifyPropertyChanged("DeleteButtonVisible");
			}
		}

		public bool DeleteButtonVisible
		{
			get
			{
				return !this.IsBuiltIn && !this.IsModified;
			}
		}

		public bool AutomaticChange { get; set; }

		public bool IsModified
		{
			get
			{
				return this.originalPreset.IsModified;
			}

			set
			{
				// Don't mark as modified if this is an automatic change.
				if (!this.AutomaticChange)
				{
					if (this.originalPreset.IsModified != value)
					{
						if (value)
						{
							this.mainViewModel.ModifyPreset(this.profile);
						}
					}

					this.NotifyPropertyChanged("IsModified");
					this.NotifyPropertyChanged("WindowTitle");
					this.NotifyPropertyChanged("DeleteButtonVisible");

					// If we've made a modification, we need to save the user presets.
					if (value)
					{
						this.mainViewModel.SaveUserPresets();
					}
				}
			}
		}

		public bool HasSourceData
		{
			get
			{
				return this.mainViewModel.HasVideoSource;
			}
		}

		public OutputFormat OutputFormat
		{
			get
			{
				return this.profile.OutputFormat;
			}

			set
			{
				this.profile.OutputFormat = value;
				this.NotifyPropertyChanged("OutputFormat");
				this.NotifyPropertyChanged("ShowMp4Choices");
				this.IsModified = true;
				this.mainViewModel.RefreshDestination();

				this.VideoPanelViewModel.NotifyOutputFormatChanged(value);
				this.AudioPanelViewModel.NotifyOutputFormatChanged(value);
			}
		}

		public OutputExtension PreferredExtension
		{
			get
			{
				return this.profile.PreferredExtension;
			}

			set
			{
				this.profile.PreferredExtension = value;
				this.NotifyPropertyChanged("PreferredExtension");
				this.IsModified = true;
				this.mainViewModel.RefreshDestination();
			}
		}

		public void RefreshExtensionChoice()
		{
			if (this.OutputFormat != OutputFormat.Mp4)
			{
				return;
			}

			bool enableMp4 = true;
			foreach (AudioEncodingViewModel audioVM in this.AudioPanelViewModel.AudioEncodings)
			{
				if (audioVM.SelectedAudioEncoder.Encoder == AudioEncoder.Ac3Passthrough)
				{
					enableMp4 = false;
					break;
				}
			}

			this.Mp4ExtensionEnabled = enableMp4;

			if (!enableMp4 && this.PreferredExtension == OutputExtension.Mp4)
			{
				this.PreferredExtension = OutputExtension.M4v;
			}
		}

		public bool Mp4ExtensionEnabled
		{
			get
			{
				return this.mp4ExtensionEnabled;
			}

			set
			{
				if (this.mp4ExtensionEnabled != value)
				{
					this.mp4ExtensionEnabled = value;
					this.NotifyPropertyChanged("Mp4ExtensionEnabled");
					this.NotifyPropertyChanged("Mp4ExtensionToolTip");
				}
			}
		}

		public string Mp4ExtensionToolTip
		{
			get
			{
				if (!this.Mp4ExtensionEnabled)
				{
					return "The .mp4 extension is disabled for compatibility with AC3 Passthrough.";
				}

				return null;
			}
		}

		public bool LargeFile
		{
			get
			{
				return this.profile.LargeFile;
			}

			set
			{
				this.profile.LargeFile = value;
				this.NotifyPropertyChanged("LargeFile");
				this.IsModified = true;
			}
		}

		public bool Optimize
		{
			get
			{
				return this.profile.Optimize;
			}

			set
			{
				this.profile.Optimize = value;
				this.NotifyPropertyChanged("Optimize");
				this.IsModified = true;
			}
		}

		public bool IPod5GSupport
		{
			get
			{
				return this.profile.IPod5GSupport;
			}

			set
			{
				this.profile.IPod5GSupport = value;
				this.NotifyPropertyChanged("IPod5GSupport");
				this.IsModified = true;
			}
		}

		public bool ShowMp4Choices
		{
			get
			{
				return this.OutputFormat == OutputFormat.Mp4;
			}
		}

		public bool IncludeChapterMarkers
		{
			get
			{
				return this.profile.IncludeChapterMarkers;
			}

			set
			{
				this.profile.IncludeChapterMarkers = value;
				this.NotifyPropertyChanged("IncludeChapterMarkers");
				this.IsModified = true;
				this.mainViewModel.RefreshChapterMarkerUI();
			}
		}

		public ICommand SaveCommand
		{
			get
			{
				if (this.saveCommand == null)
				{
					this.saveCommand = new RelayCommand(param =>
					{
						this.mainViewModel.SavePreset();
						this.IsModified = false;

						// Clone the profile so that on modifications, we're working on a new copy.
						this.profile = this.profile.Clone();
					}, param =>
					{
						return !this.IsBuiltIn;
					});
				}

				return this.saveCommand;
			}
		}

		public ICommand SaveAsCommand
		{
			get
			{
				if (this.saveAsCommand == null)
				{
					this.saveAsCommand = new RelayCommand(param =>
					{
						var dialogVM = new ChoosePresetNameViewModel(this.mainViewModel.AllPresets);
						dialogVM.PresetName = this.originalPreset.Name;
						WindowManager.OpenDialog(dialogVM, this);

						if (dialogVM.DialogResult)
						{
							string newPresetName = dialogVM.PresetName;

							this.mainViewModel.SavePresetAs(newPresetName);

							this.NotifyPropertyChanged("ProfileName");
							this.NotifyPropertyChanged("WindowTitle");

							this.IsModified = false;
							this.IsBuiltIn = false;
						}
					});
				}

				return this.saveAsCommand;
			}
		}

		public ICommand RenameCommand
		{
			get
			{
				if (this.renameCommand == null)
				{
					this.renameCommand = new RelayCommand(param =>
					{
						var dialogVM = new ChoosePresetNameViewModel(this.mainViewModel.AllPresets);
						dialogVM.PresetName = this.originalPreset.Name;
						WindowManager.OpenDialog(dialogVM, this);

						if (dialogVM.DialogResult)
						{
							string newPresetName = dialogVM.PresetName;
							this.originalPreset.Name = newPresetName;

							this.mainViewModel.SavePreset();

							this.NotifyPropertyChanged("ProfileName");
							this.NotifyPropertyChanged("WindowTitle");

							this.IsModified = false;
						}
					},
					param =>
					{
						return !this.IsBuiltIn;
					});
				}

				return this.renameCommand;
			}
		}

		public ICommand DeletePresetCommand
		{
			get
			{
				if (this.deletePresetCommand == null)
				{
					this.deletePresetCommand = new RelayCommand(param =>
					{
						if (this.IsModified)
						{
							MessageBoxResult dialogResult = Utilities.MessageBox.Show(this, "Are you sure you want to revert all unsaved changes to this preset?", "Revert Preset", MessageBoxButton.YesNo);
							if (dialogResult == MessageBoxResult.Yes)
							{
								this.mainViewModel.RevertPreset(true);
							}

							//this.IsModified = false;
							this.EditingPreset = this.originalPreset;
						}
						else
						{
							MessageBoxResult dialogResult = Utilities.MessageBox.Show(this, "Are you sure you want to remove this preset?", "Remove Preset", MessageBoxButton.YesNo);
							if (dialogResult == MessageBoxResult.Yes)
							{
								this.mainViewModel.DeletePreset();
							}
						}
					},
					param =>
					{
						// We can delete or revert if it's a user preset or if there have been modifications.
						return !this.IsBuiltIn || this.IsModified;
					});
				}

				return this.deletePresetCommand;
			}
		}

		public ICommand PreviewCommand
		{
			get
			{
				if (this.previewCommand == null)
				{
					this.previewCommand = new RelayCommand(param =>
					{
						this.mainViewModel.OpenPreviewWindowCommand.Execute(null);
					});
				}

				return this.previewCommand;
			}
		}

		#region Audio

		#endregion

		private void NotifyAllChanged()
		{
			this.AutomaticChange = true;

			this.NotifyPropertyChanged("WindowTitle");
			this.NotifyPropertyChanged("ProfileName");
			this.NotifyPropertyChanged("IsBuiltIn");
			this.NotifyPropertyChanged("DeleteButtonVisible");
			this.NotifyPropertyChanged("IsModified");
			this.NotifyPropertyChanged("OutputFormat");
			this.NotifyPropertyChanged("PreferredExtension");
			this.NotifyPropertyChanged("LargeFile");
			this.NotifyPropertyChanged("Optimize");
			this.NotifyPropertyChanged("IPod5GSupport");
			this.NotifyPropertyChanged("ShowMp4Choices");
			this.NotifyPropertyChanged("IncludeChapterMarkers");
			
			this.PicturePanelViewModel.NotifyAllChanged();
			this.VideoFiltersPanelViewModel.NotifyAllChanged();
			this.VideoPanelViewModel.NotifyAllChanged();
			this.AudioPanelViewModel.NotifyAllChanged();
			this.AdvancedPanelViewModel.NotifyAllChanged();

			this.AutomaticChange = false;
		}

		private void OnMainPropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			// Refresh output and audio previews when selected title changes.
			if (e.PropertyName == "SelectedTitle")
			{
				this.PicturePanelViewModel.NotifySelectedTitleChanged();
				this.AudioPanelViewModel.NotifySelectedTitleChanged();

				this.NotifyAudioInputChanged();
				this.NotifyPropertyChanged("HasSourceData");

				this.NotifyPropertyChanged("InputType");
				this.NotifyPropertyChanged("InputVideoCodec");
				this.NotifyPropertyChanged("InputFramerate");
			}
		}

		/// <summary>
		/// Notify the encoding window that the length of the selected video changes.
		/// </summary>
		public void NotifyLengthChanged()
		{
			this.VideoPanelViewModel.UpdateVideoBitrate();
			this.VideoPanelViewModel.UpdateTargetSize();
		}

		/// <summary>
		/// Notify the encoding window of changed audio encoding options.
		/// </summary>
		public void NotifyAudioEncodingChanged()
		{
			this.VideoPanelViewModel.UpdateVideoBitrate();
			this.VideoPanelViewModel.UpdateTargetSize();
		}

		/// <summary>
		/// Responds to changed audio input (selected tracks on main UI).
		/// </summary>
		public void NotifyAudioInputChanged()
		{
			this.AudioPanelViewModel.NotifyAudioInputChanged();

			// When audio encoding changes, we need to recalculate the estimated video bitrate or target size.
			this.VideoPanelViewModel.UpdateVideoBitrate();
			this.VideoPanelViewModel.UpdateTargetSize();
		}

		private void AudioChoicesCollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
		{
			this.NotifyAudioInputChanged();
		}
	}
}
