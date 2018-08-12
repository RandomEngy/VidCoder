using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reactive.Linq;
using System.Windows;
using Microsoft.AnyContainer;
using ReactiveUI;
using VidCoder.Model;
using VidCoder.Resources;
using VidCoder.Services;
using VidCoder.Services.Windows;
using VidCoder.ViewModel.DataModels;
using VidCoder.ViewModel.Panels;

namespace VidCoder.ViewModel
{
	public class EncodingWindowViewModel : ProfileViewModelBase
	{
		public const int VideoTabIndex = 3;
		public const int AdvancedVideoTabIndex = 4;


		public EncodingWindowViewModel()
		{
			this.AutomaticChange = true;

			this.PresetsService
				.WhenAnyValue(x => x.SelectedPreset.DisplayNameWithStar)
				.Select(presetNameWithStar => string.Format(CultureInfo.CurrentCulture, EncodingRes.WindowTitle, presetNameWithStar))
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

			this.ContainerPanelViewModel = new ContainerPanelViewModel(this);
			this.SizingPanelViewModel = new SizingPanelViewModel(this);
			this.VideoFiltersPanelViewModel = new VideoFiltersPanelViewModel(this);
			this.VideoPanelViewModel = new VideoPanelViewModel(this);
			this.AudioPanelViewModel = new AudioPanelViewModel(this);
			this.AdvancedPanelViewModel = new AdvancedPanelViewModel(this);

			this.presetPanelOpen = Config.EncodingListPaneOpen;

			this.selectedTabIndex = Config.EncodingDialogLastTab;

			this.AutomaticChange = false;
		}

		public ProcessingService ProcessingService { get; } = Resolver.Resolve<ProcessingService>();
		public OutputPathService OutputPathService { get; } = Resolver.Resolve<OutputPathService>();
		public PickersService PickersService { get; } = Resolver.Resolve<PickersService>();

		public ContainerPanelViewModel ContainerPanelViewModel { get; set; }
		public SizingPanelViewModel SizingPanelViewModel { get; set; }
		public VideoFiltersPanelViewModel VideoFiltersPanelViewModel { get; set; }
		public VideoPanelViewModel VideoPanelViewModel { get; set; }
		public AudioPanelViewModel AudioPanelViewModel { get; set; }
		public AdvancedPanelViewModel AdvancedPanelViewModel { get; set; }

		private ObservableAsPropertyHelper<string> windowTitle;
		public string WindowTitle => this.windowTitle.Value;

		private int selectedTabIndex;
		public int SelectedTabIndex
		{
			get { return this.selectedTabIndex; }
			set { this.RaiseAndSetIfChanged(ref this.selectedTabIndex, value); }
		}

		private ObservableAsPropertyHelper<bool> saveRenameButtonsVisible;
		public bool SaveRenameButtonsVisible => this.saveRenameButtonsVisible.Value;

		private ObservableAsPropertyHelper<bool> deleteButtonVisible;
		public bool DeleteButtonVisible => this.deleteButtonVisible.Value;

		private ObservableAsPropertyHelper<bool> isBuiltIn;
		public bool IsBuiltIn => this.isBuiltIn.Value;

		private bool presetPanelOpen;
		public bool PresetPanelOpen
		{
			get { return this.presetPanelOpen; }
			set { this.RaiseAndSetIfChanged(ref this.presetPanelOpen, value); }
		}

		private ReactiveCommand togglePresetPanel;
		public ReactiveCommand TogglePresetPanel
		{
			get
			{
				return this.togglePresetPanel ?? (this.togglePresetPanel = ReactiveCommand.Create(() =>
				{
					this.PresetPanelOpen = !this.PresetPanelOpen;
					Config.EncodingListPaneOpen = this.PresetPanelOpen;
				}));
			}
		}

		private ReactiveCommand save;
		public ReactiveCommand Save
		{
			get
			{
				return this.save ?? (this.save = ReactiveCommand.Create(
					() =>
					{
						this.PresetsService.SavePreset();
					},
					this.WhenAnyValue(x => x.IsBuiltIn, isBuiltIn => !isBuiltIn)));
			}
		}

		private ReactiveCommand saveAs;
		public ReactiveCommand SaveAs
		{
			get
			{
				return this.saveAs ?? (this.saveAs = ReactiveCommand.Create(() =>
				{
					var dialogVM = new ChooseNameViewModel(MiscRes.ChooseNamePreset, this.PresetsService.AllPresets.Where(preset => !preset.Preset.IsBuiltIn).Select(preset => preset.Preset.Name));
					dialogVM.Name = this.PresetsService.SelectedPreset.DisplayName;
					Resolver.Resolve<IWindowManager>().OpenDialog(dialogVM, this);

					if (dialogVM.DialogResult)
					{
						string newPresetName = dialogVM.Name;

						this.PresetsService.SavePresetAs(newPresetName);
					}
				}));
			}
		}

		private ReactiveCommand rename;
		public ReactiveCommand Rename
		{
			get
			{
				return this.rename ?? (this.rename = ReactiveCommand.Create(
					() =>
					{
						var dialogVM = new ChooseNameViewModel(MiscRes.ChooseNamePreset, this.PresetsService.AllPresets.Where(preset => !preset.Preset.IsBuiltIn).Select(preset => preset.Preset.Name));
						dialogVM.Name = this.PresetsService.SelectedPreset.DisplayName;
						Resolver.Resolve<IWindowManager>().OpenDialog(dialogVM, this);

						if (dialogVM.DialogResult)
						{
							string newPresetName = dialogVM.Name;
							this.PresetsService.RenamePreset(newPresetName);
						}
					},
					this.WhenAnyValue(x => x.IsBuiltIn, isBuiltIn => !isBuiltIn)));
			}
		}

		private ReactiveCommand deletePreset;
		public ReactiveCommand DeletePreset
		{
			get
			{
				return this.deletePreset ?? (this.deletePreset = ReactiveCommand.Create(
					() =>
					{
						if (this.Preset.IsModified)
						{
							MessageBoxResult dialogResult = Utilities.MessageBox.Show(
								this,
								MainRes.RevertConfirmMessage,
								MainRes.RevertConfirmTitle,
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
								MainRes.RemoveConfirmMessage,
								MainRes.RemoveConfirmTitle,
								MessageBoxButton.YesNo);
							if (dialogResult == MessageBoxResult.Yes)
							{
								this.PresetsService.DeletePreset();
							}
						}
					},
					this.PresetsService.WhenAnyValue(
						x => x.SelectedPreset.Preset.IsBuiltIn,
						x => x.SelectedPreset.Preset.IsModified,
						(isBuiltIn, isModified) =>
						{
							return !isBuiltIn || isModified;
						})));
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
