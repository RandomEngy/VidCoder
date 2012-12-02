using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;
using HandBrake.Interop.Model.Encoding;
using HandBrake.Interop.SourceData;
using VidCoder.Messages;
using VidCoder.Model;
using System.Collections.ObjectModel;
using System.Windows.Input;
using VidCoder.Services;
using System.Windows.Media.Imaging;
using System.ComponentModel;
using HandBrake.Interop;
using System.Windows;
using Microsoft.Practices.Unity;
using VidCoder.ViewModel.Components;
using Container = HandBrake.Interop.Model.Encoding.Container;

namespace VidCoder.ViewModel
{
	using LocalResources;

	public class EncodingViewModel : OkCancelDialogViewModel
	{
		private MainViewModel mainViewModel = Unity.Container.Resolve<MainViewModel>();
		private OutputPathViewModel outputPathVM = Unity.Container.Resolve<OutputPathViewModel>();
		private PresetsViewModel presetsViewModel = Unity.Container.Resolve<PresetsViewModel>();
		private WindowManagerViewModel windowManagerVM = Unity.Container.Resolve<WindowManagerViewModel>();
		private ProcessingViewModel processingVM = Unity.Container.Resolve<ProcessingViewModel>();

		private VCProfile profile;

		private List<ComboChoice<Container>> outputFormatChoices; 

		private Preset originalPreset;
		private bool isBuiltIn;

		public EncodingViewModel(Preset preset)
		{
			this.outputFormatChoices =
				new List<ComboChoice<Container>>
				{
					new ComboChoice<Container>(Container.Mp4, "MP4"),
					new ComboChoice<Container>(Container.Mkv, "MKV")
				};

			this.PicturePanelViewModel = new PicturePanelViewModel(this);
			this.VideoFiltersPanelViewModel = new VideoFiltersPanelViewModel(this);
			this.VideoPanelViewModel = new VideoPanelViewModel(this);
			this.AudioPanelViewModel = new AudioPanelViewModel(this);
			this.AdvancedPanelViewModel = new AdvancedPanelViewModel(this);

			this.EditingPreset = preset;
			this.mainViewModel.PropertyChanged += this.OnMainPropertyChanged;
		}

		public MainViewModel MainViewModel
		{
			get
			{
				return this.mainViewModel;
			}
		}

		public WindowManagerViewModel WindowManagerVM
		{
			get
			{
				return this.windowManagerVM;
			}
		}

		public ProcessingViewModel ProcessingVM
		{
			get
			{
				return this.processingVM;
			}
		}

		public OutputPathViewModel OutputPathVM
		{
			get
			{
				return this.outputPathVM;
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
				if (value.IsModified || value.IsQueue)
				{
					// If already modified or this is the scrap queue preset, use existing profile.
					this.profile = value.EncodingProfile;
				}
				else
				{
					// If not modified regular preset, clone the profile
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

		public VCProfile EncodingProfile
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
				string windowTitle = string.Format(EncodingRes.EncodingWindowTitle, this.ProfileName);
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
				return this.originalPreset.DisplayName;
			}
		}

		public bool SaveRenameButtonsVisible
		{
			get
			{
				return !this.IsBuiltIn && !this.originalPreset.IsQueue;
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
				this.SaveCommand.RaiseCanExecuteChanged();
				this.RenameCommand.RaiseCanExecuteChanged();
				this.DeletePresetCommand.RaiseCanExecuteChanged();
				this.RaisePropertyChanged(() => this.IsBuiltIn);
				this.RaisePropertyChanged(() => this.DeleteButtonVisible);
			}
		}

		public bool DeleteButtonVisible
		{
			get
			{
				return !this.IsBuiltIn && !this.IsModified && !this.originalPreset.IsQueue;
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
				// Don't mark as modified if this is an automatic change or if it's a temporary queue preset.
				if (!this.AutomaticChange && !this.originalPreset.IsQueue)
				{
					if (this.originalPreset.IsModified != value)
					{
						if (value)
						{
							this.presetsViewModel.ModifyPreset(this.profile);
						}
					}

					this.DeletePresetCommand.RaiseCanExecuteChanged();
					this.RaisePropertyChanged(() => this.IsModified);
					this.RaisePropertyChanged(() => this.WindowTitle);
					this.RaisePropertyChanged(() => this.DeleteButtonVisible);

					// If we've made a modification, we need to save the user presets. The temporary queue preset will
					// not be persisted so we don't need to save user presets when it changes.
					if (value)
					{
						this.presetsViewModel.SaveUserPresets();
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

		public Container OutputFormat
		{
			get
			{
				return this.profile.OutputFormat;
			}

			set
			{
				this.profile.OutputFormat = value;
				this.RaisePropertyChanged(() => this.OutputFormat);
				this.RaisePropertyChanged(() => this.ShowMp4Choices);
				this.IsModified = true;
				this.outputPathVM.GenerateOutputFileName();

				this.VideoPanelViewModel.NotifyOutputFormatChanged(value);
				this.AudioPanelViewModel.NotifyOutputFormatChanged(value);
			}
		}

		public List<ComboChoice<Container>> OutputFormatChoices
		{
			get
			{
				return this.outputFormatChoices;
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
				this.RaisePropertyChanged(() => this.PreferredExtension);
				this.IsModified = true;
				this.outputPathVM.GenerateOutputFileName();
			}
		}

		//public void RefreshExtensionChoice()
		//{
		//    if (this.OutputFormat != Container.Mp4)
		//    {
		//        return;
		//    }

		//    bool enableMp4 = true;
		//    foreach (AudioEncodingViewModel audioVM in this.AudioPanelViewModel.AudioEncodings)
		//    {
		//        if (audioVM.SelectedAudioEncoder.Encoder.ShortName == "copy:ac3")
		//        {
		//            enableMp4 = false;
		//            break;
		//        }
		//    }

		//    if (!enableMp4 && this.PreferredExtension == OutputExtension.Mp4)
		//    {
		//        this.PreferredExtension = OutputExtension.M4v;
		//    }
		//}

		public bool LargeFile
		{
			get
			{
				return this.profile.LargeFile;
			}

			set
			{
				this.profile.LargeFile = value;
				this.RaisePropertyChanged(() => this.LargeFile);
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
				this.RaisePropertyChanged(() => this.Optimize);
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
				this.RaisePropertyChanged(() => this.IPod5GSupport);
				this.IsModified = true;
			}
		}

		public bool ShowMp4Choices
		{
			get
			{
				return this.OutputFormat == Container.Mp4;
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
				this.RaisePropertyChanged(() => this.IncludeChapterMarkers);
				this.IsModified = true;
				this.mainViewModel.RefreshChapterMarkerUI();
			}
		}

		private RelayCommand saveCommand;
		public RelayCommand SaveCommand
		{
			get
			{
				return this.saveCommand ?? (this.saveCommand = new RelayCommand(() =>
					{
						this.presetsViewModel.SavePreset();
						this.IsModified = false;

						// Clone the profile so that on modifications, we're working on a new copy.
						this.profile = this.profile.Clone();
					}, () =>
					{
						return !this.IsBuiltIn;
					}));
			}
		}

		private RelayCommand saveAsCommand;
		public RelayCommand SaveAsCommand
		{
			get
			{
				return this.saveAsCommand ?? (this.saveAsCommand = new RelayCommand(() =>
					{
						var dialogVM = new ChoosePresetNameViewModel(this.presetsViewModel.AllPresets);
						dialogVM.PresetName = this.originalPreset.Name;
						WindowManager.OpenDialog(dialogVM, this);

						if (dialogVM.DialogResult)
						{
							string newPresetName = dialogVM.PresetName;

							this.presetsViewModel.SavePresetAs(newPresetName);

							this.RaisePropertyChanged(() => this.ProfileName);
							this.RaisePropertyChanged(() => this.WindowTitle);

							this.IsModified = false;
							this.IsBuiltIn = false;
						}
					}));
			}
		}

		private RelayCommand renameCommand;
		public RelayCommand RenameCommand
		{
			get
			{
				return this.renameCommand ?? (this.renameCommand = new RelayCommand(() =>
					{
						var dialogVM = new ChoosePresetNameViewModel(this.presetsViewModel.AllPresets);
						dialogVM.PresetName = this.originalPreset.Name;
						WindowManager.OpenDialog(dialogVM, this);

						if (dialogVM.DialogResult)
						{
							string newPresetName = dialogVM.PresetName;
							this.originalPreset.Name = newPresetName;

							this.presetsViewModel.SavePreset();

							this.RaisePropertyChanged(() => this.ProfileName);
							this.RaisePropertyChanged(() => this.WindowTitle);

							this.IsModified = false;
						}
					}, () =>
					{
						return !this.IsBuiltIn;
					}));
			}
		}

		private RelayCommand deletePresetCommand;
		public RelayCommand DeletePresetCommand
		{
			get
			{
				return this.deletePresetCommand ?? (this.deletePresetCommand = new RelayCommand(() =>
					{
						if (this.IsModified)
						{
							MessageBoxResult dialogResult = Utilities.MessageBox.Show(this, MainRes.PresetRevertConfirmMessage, MainRes.PresetRevertConfirmTitle, MessageBoxButton.YesNo);
							if (dialogResult == MessageBoxResult.Yes)
							{
								this.presetsViewModel.RevertPreset(true);
							}

							//this.IsModified = false;
							this.EditingPreset = this.originalPreset;
						}
						else
						{
							MessageBoxResult dialogResult = Utilities.MessageBox.Show(this, MainRes.PresetRemoveConfirmMessage, MainRes.PresetRemoveConfirmTitle, MessageBoxButton.YesNo);
							if (dialogResult == MessageBoxResult.Yes)
							{
								this.presetsViewModel.DeletePreset();
							}
						}
					}, () =>
					{
						// We can delete or revert if it's a user preset or if there have been modifications.
						return !this.IsBuiltIn || this.IsModified;
					}));
			}
		}

		#region Audio

		#endregion

		private void NotifyAllChanged()
		{
			this.AutomaticChange = true;

			this.RaisePropertyChanged(() => this.WindowTitle);
			this.RaisePropertyChanged(() => this.ProfileName);
			this.RaisePropertyChanged(() => this.IsBuiltIn);
			this.RaisePropertyChanged(() => this.SaveRenameButtonsVisible);
			this.RaisePropertyChanged(() => this.DeleteButtonVisible);
			this.RaisePropertyChanged(() => this.IsModified);
			this.RaisePropertyChanged(() => this.OutputFormat);
			this.RaisePropertyChanged(() => this.PreferredExtension);
			this.RaisePropertyChanged(() => this.LargeFile);
			this.RaisePropertyChanged(() => this.Optimize);
			this.RaisePropertyChanged(() => this.IPod5GSupport);
			this.RaisePropertyChanged(() => this.ShowMp4Choices);
			this.RaisePropertyChanged(() => this.IncludeChapterMarkers);
			
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
				this.VideoPanelViewModel.NotifySelectedTitleChanged();

				this.RaisePropertyChanged(() => this.HasSourceData);
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
	}
}
