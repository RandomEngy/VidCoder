using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using GalaSoft.MvvmLight.Messaging;
using HandBrake.ApplicationServices.Interop;
using HandBrake.ApplicationServices.Interop.Json.Scan;
using HandBrake.ApplicationServices.Interop.Model.Encoding;
using ReactiveUI;
using VidCoder.Messages;
using VidCoder.Model;
using VidCoder.Resources;
using VidCoder.Services;
using VidCoder.Services.Windows;
using VidCoderCommon.Model;

namespace VidCoder.ViewModel
{
	public class EncodingWindowViewModel : ProfileViewModelBase
	{
		public const int VideoTabIndex = 2;
		public const int AdvancedVideoTabIndex = 3;

		private OutputPathService outputPathService = Ioc.Get<OutputPathService>();
		private ProcessingService processingService = Ioc.Get<ProcessingService>();

		private List<ComboChoice> containerChoices;

		public EncodingWindowViewModel()
		{
			this.AutomaticChange = true;

			this.RegisterProfileProperties();

			this.containerChoices = new List<ComboChoice>();
			foreach (HBContainer hbContainer in HandBrakeEncoderHelpers.Containers)
			{
				this.containerChoices.Add(new ComboChoice(hbContainer.ShortName, hbContainer.DisplayName));
			}

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

			this.PresetsService.WhenAnyValue(x => x.SelectedPreset.DisplayNameWithStar)
				.ToProperty(this, x => x.WindowTitle, out this.windowTitle);

			this.PresetsService.WhenAnyValue(
				x => x.SelectedPreset.Preset.IsBuiltIn,
				x => x.SelectedPreset.Preset.IsQueue,
				(isBuiltIn, isQueue) =>
				{
					return !isBuiltIn && !isQueue;
				})
				.ToProperty(this, x => x.SaveRenameButtonsVisible, out this.saveRenameButtonsVisible);

			this.PresetsService.WhenAnyValue(
				x => x.SelectedPreset.Preset.IsBuiltIn,
				x => x.SelectedPreset.Preset.IsModified,
				x => x.SelectedPreset.Preset.IsQueue,
				(isBuiltIn, isModified, isQueue) =>
				{
					return !isBuiltIn && !isModified && !isQueue;
				})
				.ToProperty(this, x => x.DeleteButtonVisible, out this.deleteButtonVisible);

			this.PresetsService.WhenAnyValue(x => x.SelectedPreset.Preset.IsBuiltIn)
				.ToProperty(this, x => x.IsBuiltIn, out this.isBuiltIn);


			this.PresetsService.WhenAnyValue(x => x.SelectedPreset.Preset.EncodingProfile)
				.Subscribe(encodingProfile =>
				{
					if (!encodingProfile.UseAdvancedTab && this.SelectedTabIndex == AdvancedVideoTabIndex)
					{
						this.SelectedTabIndex = VideoTabIndex;
					}
				});


			this.TogglePresetPanel = ReactiveCommand.Create();
			this.TogglePresetPanel.Subscribe(_ => this.TogglePresetPanelImpl());

			this.Save = ReactiveCommand.Create(this.WhenAnyValue(x => x.IsBuiltIn, isBuiltIn => !isBuiltIn));
			this.Save.Subscribe(_ => this.SaveImpl());

			this.SaveAs = ReactiveCommand.Create();
			this.SaveAs.Subscribe(_ => this.SaveAsImpl());

			this.Rename = ReactiveCommand.Create(this.WhenAnyValue(x => x.IsBuiltIn, isBuiltIn => !isBuiltIn));
			this.Rename.Subscribe(_ => this.RenameImpl());

			this.DeletePreset = ReactiveCommand.Create(this.PresetsService.WhenAnyValue(
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

			this.selectedTabIndex = Config.EncodingDialogLastTab;

			this.AutomaticChange = false;
		}

		private void RegisterProfileProperties()
		{
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

			this.RegisterProfileProperty(() => this.IncludeChapterMarkers);
		}

		public ProcessingService ProcessingService
		{
		    get { return this.processingService; }
		}

		public OutputPathService OutputPathVM
		{
		    get { return this.outputPathService; }
		}

		public PicturePanelViewModel PicturePanelViewModel { get; set; }
		public VideoFiltersPanelViewModel VideoFiltersPanelViewModel { get; set; }
		public VideoPanelViewModel VideoPanelViewModel { get; set; }
		public AudioPanelViewModel AudioPanelViewModel { get; set; }
		public AdvancedPanelViewModel AdvancedPanelViewModel { get; set; }

		private ObservableAsPropertyHelper<string> windowTitle;
		public string WindowTitle
		{
			get { return this.windowTitle.Value; }
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

		public ReactiveCommand<object> TogglePresetPanel { get; }
		private void TogglePresetPanelImpl()
		{
			this.PresetPanelOpen = !this.PresetPanelOpen;
			Config.EncodingListPaneOpen = this.PresetPanelOpen;
		}

		public ReactiveCommand<object> Save { get; }
		private void SaveImpl()
		{
			this.PresetsService.SavePreset();
		}

		public ReactiveCommand<object> SaveAs { get; }

		private void SaveAsImpl()
		{
			var dialogVM = new ChooseNameViewModel(MainRes.PresetWord, this.PresetsService.AllPresets.Where(preset => !preset.Preset.IsBuiltIn).Select(preset => preset.Preset.Name));
			dialogVM.Name = this.PresetsService.SelectedPreset.DisplayName;
			Ioc.Get<IWindowManager>().OpenDialog(dialogVM, this);

			if (dialogVM.DialogResult)
			{
				string newPresetName = dialogVM.Name;

				this.PresetsService.SavePresetAs(newPresetName);
			}
		}

		public ReactiveCommand<object> Rename { get; }

		private void RenameImpl()
		{
			var dialogVM = new ChooseNameViewModel(MainRes.PresetWord, this.PresetsService.AllPresets.Where(preset => !preset.Preset.IsBuiltIn).Select(preset => preset.Preset.Name));
			dialogVM.Name = this.PresetsService.SelectedPreset.DisplayName;
			Ioc.Get<IWindowManager>().OpenDialog(dialogVM, this);

			if (dialogVM.DialogResult)
			{
				string newPresetName = dialogVM.Name;
				this.PresetsService.SelectedPreset.Preset.Name = newPresetName;

				this.PresetsService.SavePreset();
			}
		}

		public ReactiveCommand<object> DeletePreset { get; }

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
					this.PresetsService.RevertPreset(true);
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
					this.PresetsService.DeletePreset();
				}
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
