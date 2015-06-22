using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;
using HandBrake.ApplicationServices.Interop;
using HandBrake.ApplicationServices.Interop.Json.Scan;
using HandBrake.ApplicationServices.Interop.Model.Encoding;
using VidCoder.Extensions;
using VidCoder.Messages;
using VidCoder.Model;
using VidCoder.Resources;
using VidCoder.Services;
using VidCoderCommon.Model;

namespace VidCoder.ViewModel
{
	public class EncodingViewModel : OkCancelDialogOldViewModel
	{
		public const int VideoTabIndex = 2;
		public const int AdvancedVideoTabIndex = 3;

		private MainViewModel mainViewModel = Ioc.Container.GetInstance<MainViewModel>();
		private OutputPathService outputPathService = Ioc.Container.GetInstance<OutputPathService>();
		private PresetsService presetsService = Ioc.Container.GetInstance<PresetsService>();
		private WindowManagerService windowManagerService = Ioc.Container.GetInstance<WindowManagerService>();
		private ProcessingService processingService = Ioc.Container.GetInstance<ProcessingService>();

		private VCProfile profile;

		private List<ComboChoice> containerChoices;

		private Preset originalPreset;
		private bool isBuiltIn;

		public EncodingViewModel(Preset preset)
		{
			this.containerChoices = new List<ComboChoice>();
			foreach (HBContainer hbContainer in HandBrakeEncoderHelpers.Containers)
			{
				this.containerChoices.Add(new ComboChoice(hbContainer.ShortName, hbContainer.DisplayName));
			}

			this.PicturePanelViewModel = new PicturePanelViewModel(this);
			this.VideoFiltersPanelViewModel = new VideoFiltersPanelViewModel(this);
			this.VideoPanelViewModel = new VideoPanelViewModel(this);
			this.AudioPanelViewModel = new AudioPanelViewModel(this);
			this.AdvancedPanelViewModel = new AdvancedPanelViewModel(this);

			this.presetPanelOpen = Config.EncodingListPaneOpen;
			this.EditingPreset = preset;
			this.mainViewModel.PropertyChanged += this.OnMainPropertyChanged;

			this.selectedTabIndex = Config.EncodingDialogLastTab;
		}

		public MainViewModel MainViewModel
		{
		    get { return this.mainViewModel; }
		}

		public WindowManagerService WindowManagerService
		{
		    get { return this.windowManagerService; }
		}

		public ProcessingService ProcessingService
		{
		    get { return this.processingService; }
		}

		public OutputPathService OutputPathVM
		{
		    get { return this.outputPathService; }
		}

		public PresetsService PresetVM
		{
			get { return this.presetsService; }
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

				if (!value.EncodingProfile.UseAdvancedTab && this.SelectedTabIndex == AdvancedVideoTabIndex)
				{
					this.SelectedTabIndex = VideoTabIndex;
				}

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

		public SourceTitle SelectedTitle
		{
			get
			{
				return this.mainViewModel.SelectedTitle;
			}
		}

		private int selectedTabIndex;
		public int SelectedTabIndex
		{
			get
			{
				return this.selectedTabIndex;
			}

			set
			{
				this.selectedTabIndex = value;
				this.RaisePropertyChanged(() => this.SelectedTabIndex);
			}
		}

		public string ProfileName
		{
			get
			{
				return this.originalPreset.GetDisplayName();
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

		private bool presetPanelOpen;
		public bool PresetPanelOpen
		{
			get
			{
				return this.presetPanelOpen;
			}

			set
			{
				this.presetPanelOpen = value;
				this.RaisePropertyChanged(() => this.PresetPanelOpen);
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
				if (!this.AutomaticChange)
				{
					Messenger.Default.Send(new EncodingProfileChangedMessage());
				}

				// Don't mark as modified if this is an automatic change or if it's a temporary queue preset.
				if (!this.AutomaticChange && !this.originalPreset.IsQueue)
				{
					if (this.originalPreset.IsModified != value)
					{
						if (value)
						{
							this.presetsService.ModifyPreset(this.profile);
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
						this.presetsService.SaveUserPresets();
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

		public string ContainerName
		{
			get
			{
				return this.profile.ContainerName;
			}

			set
			{
				this.profile.ContainerName = value;
				this.RaisePropertyChanged(() => this.ContainerName);
				this.RaisePropertyChanged(() => this.ShowMp4Choices);
				this.RaisePropertyChanged(() => this.ShowOldMp4Choices);
				this.IsModified = true;
				this.outputPathService.GenerateOutputFileName();

				Messenger.Default.Send(new ContainerChangedMessage(value));
			}
		}

		public List<ComboChoice> ContainerChoices
		{
			get
			{
				return this.containerChoices;
			}
		}

		public VCOutputExtension PreferredExtension
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
				this.outputPathService.GenerateOutputFileName();
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
				HBContainer container = HandBrakeEncoderHelpers.GetContainer(this.ContainerName);
				return container.DefaultExtension == "mp4";
			}
		}

		public bool ShowOldMp4Choices
		{
			get
			{
				return this.ContainerName == "mp4v2";
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

		private RelayCommand togglePresetPanelCommand;
		public RelayCommand TogglePresetPanelCommand
		{
			get
			{
				return this.togglePresetPanelCommand ?? (this.togglePresetPanelCommand = new RelayCommand(() =>
				{
					this.PresetPanelOpen = !this.PresetPanelOpen;
					Config.EncodingListPaneOpen = this.PresetPanelOpen;
				}));
			}
		}

		private RelayCommand saveCommand;
		public RelayCommand SaveCommand
		{
			get
			{
				return this.saveCommand ?? (this.saveCommand = new RelayCommand(() =>
					{
						this.presetsService.SavePreset();
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
						var dialogVM = new ChooseNameViewModel(MainRes.PresetWord, this.presetsService.AllPresets.Where(preset => !preset.IsBuiltIn).Select(preset => preset.PresetName));
						dialogVM.Name = this.originalPreset.GetDisplayName();
						WindowManager.OpenDialog(dialogVM, this);

						if (dialogVM.DialogResult)
						{
							string newPresetName = dialogVM.Name;

							this.presetsService.SavePresetAs(newPresetName);

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
						var dialogVM = new ChooseNameViewModel(MainRes.PresetWord, this.presetsService.AllPresets.Where(preset => !preset.IsBuiltIn).Select(preset => preset.PresetName));
						dialogVM.Name = this.originalPreset.GetDisplayName();
						WindowManager.OpenDialog(dialogVM, this);

						if (dialogVM.DialogResult)
						{
							string newPresetName = dialogVM.Name;
							this.originalPreset.Name = newPresetName;

							this.presetsService.SavePreset();

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
							MessageBoxResult dialogResult = Utilities.MessageBox.Show(
                                this, 
                                string.Format(MainRes.RevertConfirmMessage, MainRes.PresetWord),
                                string.Format(MainRes.RevertConfirmTitle, MainRes.PresetWord),
                                MessageBoxButton.YesNo);
							if (dialogResult == MessageBoxResult.Yes)
							{
								this.presetsService.RevertPreset(true);
							}

							this.EditingPreset = this.originalPreset;
						}
						else
						{
							MessageBoxResult dialogResult = Utilities.MessageBox.Show(
                                this, 
                                string.Format(MainRes.RemoveConfirmMessage, MainRes.PresetWord), 
                                string.Format(MainRes.RemoveConfirmTitle, MainRes.PresetWord), 
                                MessageBoxButton.YesNo);
							if (dialogResult == MessageBoxResult.Yes)
							{
								this.presetsService.DeletePreset();
							}
						}
					}, () =>
					{
						// We can delete or revert if it's a user preset or if there have been modifications.
						return !this.IsBuiltIn || this.IsModified;
					}));
			}
		}

		private void NotifyAllChanged()
		{
			this.AutomaticChange = true;

			this.RaisePropertyChanged(() => this.WindowTitle);
			this.RaisePropertyChanged(() => this.ProfileName);
			this.RaisePropertyChanged(() => this.IsBuiltIn);
			this.RaisePropertyChanged(() => this.SaveRenameButtonsVisible);
			this.RaisePropertyChanged(() => this.DeleteButtonVisible);
			this.RaisePropertyChanged(() => this.IsModified);
			this.RaisePropertyChanged(() => this.ContainerName);
			this.RaisePropertyChanged(() => this.PreferredExtension);
			this.RaisePropertyChanged(() => this.LargeFile);
			this.RaisePropertyChanged(() => this.Optimize);
			this.RaisePropertyChanged(() => this.IPod5GSupport);
			this.RaisePropertyChanged(() => this.ShowMp4Choices);
			this.RaisePropertyChanged(() => this.ShowOldMp4Choices);
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
