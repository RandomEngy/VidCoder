using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Windows;
using FastMember;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;
using HandBrake.ApplicationServices.Interop;
using HandBrake.ApplicationServices.Interop.Json.Scan;
using HandBrake.ApplicationServices.Interop.Model.Encoding;
using Omu.ValueInjecter;
using ReactiveUI;
using VidCoder.Extensions;
using VidCoder.Messages;
using VidCoder.Model;
using VidCoder.Resources;
using VidCoder.Services;
using VidCoder.Services.Windows;
using VidCoderCommon.Model;

namespace VidCoder.ViewModel
{
	public class EncodingWindowViewModel : OkCancelDialogViewModel
	{
		public const int VideoTabIndex = 2;
		public const int AdvancedVideoTabIndex = 3;

		private static TypeAccessor typeAccessor = TypeAccessor.Create(typeof(VCProfile));

		private MainViewModel mainViewModel = Ioc.Get<MainViewModel>();
		private OutputPathService outputPathService = Ioc.Get<OutputPathService>();
		private PresetsService presetsService = Ioc.Get<PresetsService>();
		private ProcessingService processingService = Ioc.Get<ProcessingService>();

		private Dictionary<string, Action> profileProperties;

		//private VCProfile profile;

		private List<ComboChoice> containerChoices;

		//private Preset originalPreset;
		//private bool isBuiltIn;

		public EncodingWindowViewModel()
		{
			this.AutomaticChange = true;

			this.RegisterProfileProperties();

			this.containerChoices = new List<ComboChoice>();
			foreach (HBContainer hbContainer in HandBrakeEncoderHelpers.Containers)
			{
				this.containerChoices.Add(new ComboChoice(hbContainer.ShortName, hbContainer.DisplayName));
			}

			this.presetsService.WhenAnyValue(x => x.SelectedPreset.Preset.EncodingProfile)
				.Subscribe(x =>
				{
					bool automaticChangePreviousValue = this.AutomaticChange;
					this.AutomaticChange = true;
					this.RaiseAllChanged();
					this.AutomaticChange = automaticChangePreviousValue;
				});

			this.WhenAnyValue(
				x => x.ContainerName,
				containerName =>
				{
					HBContainer container = HandBrakeEncoderHelpers.GetContainer(containerName);
					return container.DefaultExtension == "mp4";
				})
				.ToProperty(this, x => x.ShowMp4Choices, out this.showMp4Choices);

			this.WhenAnyValue(
				x => x.ContainerName,
				containerName =>
				{
					return containerName == "mp4v2";
				})
				.ToProperty(this, x => x.ShowOldMp4Choices, out this.showOldMp4Choices);

			this.presetsService.WhenAnyValue(x => x.SelectedPreset.DisplayNameWithStar)
				.ToProperty(this, x => x.WindowTitle, out this.windowTitle);

			this.presetsService.WhenAnyValue(
				x => x.SelectedPreset.Preset.IsBuiltIn,
				x => x.SelectedPreset.Preset.IsQueue,
				(isBuiltIn, isQueue) =>
				{
					return !isBuiltIn && !isQueue;
				})
				.ToProperty(this, x => x.SaveRenameButtonsVisible, out this.saveRenameButtonsVisible);

			this.presetsService.WhenAnyValue(
				x => x.SelectedPreset.Preset.IsBuiltIn,
				x => x.SelectedPreset.Preset.IsModified,
				x => x.SelectedPreset.Preset.IsQueue,
				(isBuiltIn, isModified, isQueue) =>
				{
					return !isBuiltIn && !isModified && !isQueue;
				})
				.ToProperty(this, x => x.DeleteButtonVisible, out this.deleteButtonVisible);

			this.presetsService.WhenAnyValue(x => x.SelectedPreset.Preset.IsBuiltIn)
				.ToProperty(this, x => x.IsBuiltIn, out this.isBuiltIn);

			this.mainViewModel.WhenAnyValue(x => x.HasVideoSource)
				.ToProperty(this, x => x.HasSourceData, out this.hasSourceData);

			this.TogglePresetPanel = ReactiveCommand.Create();
			this.TogglePresetPanel.Subscribe(_ => this.TogglePresetPanelImpl());

			this.Save = ReactiveCommand.Create(this.WhenAnyValue(x => x.IsBuiltIn, isBuiltIn => !isBuiltIn));
			this.Save.Subscribe(_ => this.SaveImpl());

			this.SaveAs = ReactiveCommand.Create();
			this.SaveAs.Subscribe(_ => this.SaveAsImpl());

			this.Rename = ReactiveCommand.Create(this.WhenAnyValue(x => x.IsBuiltIn, isBuiltIn => !isBuiltIn));
			this.Rename.Subscribe(_ => this.RenameImpl());

			this.DeletePreset = ReactiveCommand.Create(this.presetsService.WhenAnyValue(
				x => x.SelectedPreset.Preset.IsBuiltIn,
				x => x.SelectedPreset.Preset.IsModified,
				(isBuiltIn, isModified) =>
				{
					return !isBuiltIn || isModified;
				}));
			this.DeletePreset.Subscribe(_ => this.DeletePresetImpl());

			this.PicturePanelViewModel = new PicturePanelViewModel(this);
			this.VideoFiltersPanelViewModel = new VideoFiltersPanelViewModel(this);
			this.VideoPanelViewModel = new VideoPanelViewModel(this);
			this.AudioPanelViewModel = new AudioPanelViewModel(this);
			this.AdvancedPanelViewModel = new AdvancedPanelViewModel(this);

			this.presetPanelOpen = Config.EncodingListPaneOpen;
			//this.EditingPreset = preset;
			//this.mainViewModel.PropertyChanged += this.OnMainPropertyChanged;

			this.selectedTabIndex = Config.EncodingDialogLastTab;

			this.AutomaticChange = false;
		}

		private void RegisterProfileProperties()
		{
			this.profileProperties = new Dictionary<string, Action>();

			// These actions fire when the user changes a property.

			this.RegisterProfileProperty(() => this.Profile.ContainerName, () =>
			{
				this.outputPathService.GenerateOutputFileName();

				Messenger.Default.Send(new ContainerChangedMessage(this.ContainerName));
			});

			this.RegisterProfileProperty(() => this.Profile.PreferredExtension, () =>
			{
				this.outputPathService.GenerateOutputFileName();
			});

			this.RegisterProfileProperty(() => this.Profile.LargeFile);
			this.RegisterProfileProperty(() => this.Profile.Optimize);
			this.RegisterProfileProperty(() => this.Profile.IPod5GSupport);

			this.RegisterProfileProperty(() => this.IncludeChapterMarkers, () =>
			{
				this.mainViewModel.RefreshChapterMarkerUI();
			});
		}

		private void RegisterProfileProperty<T>(Expression<Func<T>> propertyExpression, Action action = null)
		{
			string propertyName = MvvmUtilities.GetPropertyName(propertyExpression);
			this.profileProperties.Add(propertyName, action);
		}

		public MainViewModel MainViewModel
		{
		    get { return this.mainViewModel; }
		}

		public ProcessingService ProcessingService
		{
		    get { return this.processingService; }
		}

		public OutputPathService OutputPathVM
		{
		    get { return this.outputPathService; }
		}

		public PresetsService PresetsService
		{
			get { return this.presetsService; }
		}

		public PicturePanelViewModel PicturePanelViewModel { get; set; }
		public VideoFiltersPanelViewModel VideoFiltersPanelViewModel { get; set; }
		public VideoPanelViewModel VideoPanelViewModel { get; set; }
		public AudioPanelViewModel AudioPanelViewModel { get; set; }
		public AdvancedPanelViewModel AdvancedPanelViewModel { get; set; }

		//public Preset EditingPreset
		//{
		//	get
		//	{
		//		return this.originalPreset;
		//	}

		//	set
		//	{
		//		if (value.IsModified || value.IsQueue)
		//		{
		//			// If already modified or this is the scrap queue preset, use existing profile.
		//			this.profile = value.EncodingProfile;
		//		}
		//		else
		//		{
		//			// If not modified regular preset, clone the profile
		//			this.profile = value.EncodingProfile.Clone();
		//		}

		//		this.originalPreset = value;

		//		this.IsBuiltIn = value.IsBuiltIn;

		//		if (!value.EncodingProfile.UseAdvancedTab && this.SelectedTabIndex == AdvancedVideoTabIndex)
		//		{
		//			this.SelectedTabIndex = VideoTabIndex;
		//		}

		//		this.VideoPanelViewModel.NotifyProfileChanged();
		//		this.AudioPanelViewModel.NotifyProfileChanged();

		//		this.PicturePanelViewModel.RefreshOutputSize();

		//		this.AdvancedPanelViewModel.UpdateUIFromAdvancedOptions();

		//		this.NotifyAllChanged();
		//	}
		//}

		public Preset Preset
		{
			get { return this.presetsService.SelectedPreset.Preset; }
		}

		//public VCProfile EncodingProfile
		//{
		//	get
		//	{
		//		return this.profile;
		//	}
		//}

		public VCProfile Profile
		{
			get { return this.presetsService.SelectedPreset.Preset.EncodingProfile; }
		}

		private ObservableAsPropertyHelper<string> windowTitle;
		public string WindowTitle
		{
			get { return this.windowTitle.Value; }
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
			get { return this.selectedTabIndex; }
			set { this.RaiseAndSetIfChanged(ref this.selectedTabIndex, value); }
		}

		private ObservableAsPropertyHelper<bool> saveRenameButtonsVisible;
		public bool SaveRenameButtonsVisible
		{
			get { return this.saveRenameButtonsVisible.Value; }
		}

		private ObservableAsPropertyHelper<bool> deleteButtonVisible;
		public bool DeleteButtonVisible
		{
			get { return this.deleteButtonVisible.Value; }
		}

		private ObservableAsPropertyHelper<bool> isBuiltIn;

		public bool IsBuiltIn
		{
			get { return this.isBuiltIn.Value; }
		}

		private bool presetPanelOpen;
		public bool PresetPanelOpen
		{
			get { return this.presetPanelOpen; }
			set { this.RaiseAndSetIfChanged(ref this.presetPanelOpen, value); }
		}

		public bool AutomaticChange { get; set; }

		//public bool IsModified
		//{
		//	get
		//	{
		//		return this.originalPreset.IsModified;
		//	}

		//	set
		//	{
		//		if (!this.AutomaticChange)
		//		{
		//			Messenger.Default.Send(new EncodingProfileChangedMessage());
		//		}

		//		// Don't mark as modified if this is an automatic change or if it's a temporary queue preset.
		//		if (!this.AutomaticChange && !this.originalPreset.IsQueue)
		//		{
		//			if (this.originalPreset.IsModified != value)
		//			{
		//				if (value)
		//				{
		//					this.presetsService.ModifyPreset(this.profile);
		//				}
		//			}

		//			this.DeletePresetCommand.RaiseCanExecuteChanged();
		//			this.RaisePropertyChanged(() => this.IsModified);
		//			this.RaisePropertyChanged(() => this.WindowTitle);
		//			this.RaisePropertyChanged(() => this.DeleteButtonVisible);

		//			// If we've made a modification, we need to save the user presets. The temporary queue preset will
		//			// not be persisted so we don't need to save user presets when it changes.
		//			if (value)
		//			{
		//				this.presetsService.SaveUserPresets();
		//			}
		//		}
		//	}
		//}

		private ObservableAsPropertyHelper<bool> hasSourceData;
		public bool HasSourceData
		{
			get { return this.hasSourceData.Value; }
		}

		//public bool HasSourceData
		//{
		//	get
		//	{
		//		return this.mainViewModel.HasVideoSource;
		//	}
		//}

		public string ContainerName
		{
			get { return this.Profile.ContainerName; }
			set { this.UpdateProfileProperty(() => this.Profile.ContainerName, value); }
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
			get { return this.Profile.PreferredExtension; }
			set { this.UpdateProfileProperty(() => this.Profile.PreferredExtension, value); }
		}

		public bool LargeFile
		{
			get { return this.Profile.LargeFile; }
			set { this.UpdateProfileProperty(() => this.Profile.LargeFile, value); }
		}

		public bool Optimize
		{
			get { return this.Profile.Optimize; }
			set { this.UpdateProfileProperty(() => this.Profile.Optimize, value); }
		}

		public bool IPod5GSupport
		{
			get { return this.Profile.IPod5GSupport; }
			set { this.UpdateProfileProperty(() => this.Profile.IPod5GSupport, value); }
		}

		private ObservableAsPropertyHelper<bool> showMp4Choices;
		public bool ShowMp4Choices
		{
			get { return this.showMp4Choices.Value; }
		}

		private ObservableAsPropertyHelper<bool> showOldMp4Choices;
		public bool ShowOldMp4Choices
		{
			get { return this.showOldMp4Choices.Value; }
		}

		public bool IncludeChapterMarkers
		{
			get { return this.Profile.IncludeChapterMarkers; }
			set { this.UpdateProfileProperty(() => this.Profile.IncludeChapterMarkers, value); }
		}

		public ReactiveCommand<object> TogglePresetPanel { get; private set; }

		private void TogglePresetPanelImpl()
		{
			this.PresetPanelOpen = !this.PresetPanelOpen;
			Config.EncodingListPaneOpen = this.PresetPanelOpen;
		}

		public ReactiveCommand<object> Save { get; private set; }

		private void SaveImpl()
		{
			this.presetsService.SavePreset();
		}

		public ReactiveCommand<object> SaveAs { get; private set; }

		private void SaveAsImpl()
		{
			var dialogVM = new ChooseNameViewModel(MainRes.PresetWord, this.presetsService.AllPresets.Where(preset => !preset.Preset.IsBuiltIn).Select(preset => preset.Preset.Name));
			dialogVM.Name = this.presetsService.SelectedPreset.DisplayName;
			Ioc.Get<IWindowManager>().OpenDialog(dialogVM, this);

			if (dialogVM.DialogResult)
			{
				string newPresetName = dialogVM.Name;

				this.presetsService.SavePresetAs(newPresetName);
			}
		}

		public ReactiveCommand<object> Rename { get; private set; }

		private void RenameImpl()
		{
			var dialogVM = new ChooseNameViewModel(MainRes.PresetWord, this.presetsService.AllPresets.Where(preset => !preset.Preset.IsBuiltIn).Select(preset => preset.Preset.Name));
			dialogVM.Name = this.presetsService.SelectedPreset.DisplayName;
			Ioc.Get<IWindowManager>().OpenDialog(dialogVM, this);

			if (dialogVM.DialogResult)
			{
				string newPresetName = dialogVM.Name;
				this.presetsService.SelectedPreset.Preset.Name = newPresetName;

				this.presetsService.SavePreset();
			}
		}

		public ReactiveCommand<object> DeletePreset { get; private set; }

		private void DeletePresetImpl()
		{
			if (this.Preset.IsModified)
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
		}

		//private void NotifyAllChanged()
		//{
		//	this.AutomaticChange = true;

		//	this.RaisePropertyChanged(() => this.WindowTitle);
		//	this.RaisePropertyChanged(() => this.ProfileName);
		//	this.RaisePropertyChanged(() => this.IsBuiltIn);
		//	this.RaisePropertyChanged(() => this.SaveRenameButtonsVisible);
		//	this.RaisePropertyChanged(() => this.DeleteButtonVisible);
		//	this.RaisePropertyChanged(() => this.IsModified);
		//	this.RaisePropertyChanged(() => this.ContainerName);
		//	this.RaisePropertyChanged(() => this.PreferredExtension);
		//	this.RaisePropertyChanged(() => this.LargeFile);
		//	this.RaisePropertyChanged(() => this.Optimize);
		//	this.RaisePropertyChanged(() => this.IPod5GSupport);
		//	this.RaisePropertyChanged(() => this.ShowMp4Choices);
		//	this.RaisePropertyChanged(() => this.ShowOldMp4Choices);
		//	this.RaisePropertyChanged(() => this.IncludeChapterMarkers);
			
		//	this.PicturePanelViewModel.NotifyAllChanged();
		//	this.VideoFiltersPanelViewModel.NotifyAllChanged();
		//	this.VideoPanelViewModel.NotifyAllChanged();
		//	this.AudioPanelViewModel.NotifyAllChanged();
		//	this.AdvancedPanelViewModel.NotifyAllChanged();

		//	this.AutomaticChange = false;
		//}

		//private void OnMainPropertyChanged(object sender, PropertyChangedEventArgs e)
		//{
		//	// Refresh output and audio previews when selected title changes.
		//	if (e.PropertyName == "SelectedTitle")
		//	{
		//		this.PicturePanelViewModel.NotifySelectedTitleChanged();
		//		this.AudioPanelViewModel.NotifySelectedTitleChanged();
		//		this.VideoPanelViewModel.NotifySelectedTitleChanged();
		//	}
		//}

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

		private void RaiseAllChanged()
		{
			foreach (string key in this.profileProperties.Keys)
			{
				this.RaisePropertyChanged(key);
			}
		}

		private void UpdateProfileProperty<T>(Expression<Func<T>> propertyExpression, T value, bool raisePropertyChanged = true)
		{
			string propertyName = MvvmUtilities.GetPropertyName(propertyExpression);

			if (!this.profileProperties.ContainsKey(propertyName))
			{
				throw new ArgumentException("UpdatePresetProperty called on " + propertyName + " without registering.");
			}

			if (!this.AutomaticChange)
			{
				if (!this.Preset.IsModified)
				{
					// Clone the profile so we modify a different copy.
					VCProfile newProfile = new VCProfile();
					newProfile.InjectFrom(this.Profile);

					if (!this.Preset.IsModified)
					{
						this.presetsService.ModifyPreset(newProfile);
					}
				}
			}

			// Update the value and raise PropertyChanged
			typeAccessor[this.Profile, propertyName] = value;

			if (raisePropertyChanged)
			{
				this.RaisePropertyChanged(propertyName);
			}

			if (!this.AutomaticChange)
			{
				// If we have an action registered to update dependent properties, do it
				Action action = this.profileProperties[propertyName];
				if (action != null)
				{
					// Protect against update loops with a flag
					this.AutomaticChange = true;
					action();
					this.AutomaticChange = false;
				}

				this.presetsService.SaveUserPresets();
			}
		}
	}
}
