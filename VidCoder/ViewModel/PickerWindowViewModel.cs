using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reactive.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using Omu.ValueInjecter;
using ReactiveUI;
using VidCoder.Model;
using VidCoder.Resources;
using VidCoder.Services;
using VidCoder.Services.Windows;
using VidCoder.ViewModel.DataModels;
using VidCoderCommon.Utilities.Injection;

namespace VidCoder.ViewModel
{
	public class PickerWindowViewModel : ReactiveObject
	{
		private const string NameTokenList = "{source} {title} {range} {preset} {date} {time} {quality} {parent} {titleduration}";

		private readonly PickersService pickersService = Ioc.Get<PickersService>();
		private readonly PresetsService presetsService = Ioc.Get<PresetsService>();
		private readonly OutputPathService outputPathService = Ioc.Get<OutputPathService>();

		private AutoChangeTracker autoChangeTracker = new AutoChangeTracker();

		private Dictionary<string, Action> pickerProperties;
		private bool userModifyingOutputDirectory;
		private bool userModifyingNameFormat;
		private bool userModifyingEncodingPreset;

		public PickerWindowViewModel()
		{
			using (this.autoChangeTracker.TrackAutoChange())
			{
				this.RegisterPickerProperties();

				this.pickersService.WhenAnyValue(x => x.SelectedPicker.Picker)
					.Subscribe(x =>
					{
						using (this.autoChangeTracker.TrackAutoChange())
						{
							this.RaiseAllChanged();

							// When we are swapping active pickers, update the local properties.
							if (!this.userModifyingOutputDirectory)
							{
								this.OutputDirectoryOverride = this.Picker.OutputDirectoryOverride;
							}

							if (!this.userModifyingNameFormat)
							{
								this.NameFormatOverride = this.Picker.NameFormatOverride;
							}

							if (!this.userModifyingEncodingPreset)
							{
								this.PopulateEncodingPreset(this.Picker.UseEncodingPreset);
							}
						}
					});

				this.pickersService.WhenAnyValue(x => x.SelectedPicker.Picker.AudioLanguageCodes).Subscribe(audioLanguageCodes =>
				{
					using (this.autoChangeTracker.TrackAutoChange())
					{
						using (this.AudioLanguages.SuppressChangeNotifications())
						{
							this.AudioLanguages.Clear();

							if (audioLanguageCodes == null)
							{
								this.AudioLanguages.Add(new LanguageViewModel(this) { Code = LanguageUtilities.GetDefaultLanguageCode() });
								return;
							}

							this.AudioLanguages.AddRange(audioLanguageCodes.Select(l => new LanguageViewModel(this) { Code = l }));
						}
					}
				});

				this.AudioLanguages.ChangeTrackingEnabled = true;
				this.AudioLanguages.ItemChanged
					.Subscribe(_ =>
					{
						if (!this.autoChangeTracker.OperationInProgress)
						{
							this.HandleAudioLanguageUpdate();
						}
					});

				this.pickersService.WhenAnyValue(x => x.SelectedPicker.Picker.SubtitleLanguageCodes).Subscribe(subtitleLanguageCodes =>
				{
					using (this.autoChangeTracker.TrackAutoChange())
					using (this.SubtitleLanguages.SuppressChangeNotifications())
					{
						this.SubtitleLanguages.Clear();

						if (subtitleLanguageCodes == null)
						{
							this.SubtitleLanguages.Add(new LanguageViewModel(this) { Code = LanguageUtilities.GetDefaultLanguageCode() });
							return;
						}

						this.SubtitleLanguages.AddRange(subtitleLanguageCodes.Select(l => new LanguageViewModel(this) { Code = l }));
					}
				});

				this.SubtitleLanguages.ChangeTrackingEnabled = true;
				this.SubtitleLanguages.ItemChanged
					.Subscribe(_ =>
					{
						if (!this.autoChangeTracker.OperationInProgress)
						{
							this.HandleSubtitleLanguageUpdate();
						}
					});

				// HasMultipleAudioLanguages
				IObservable<int> audioLanguageCountObservable = this.AudioLanguages.CountChanged
					.StartWith(this.AudioLanguages.Count);
				IObservable<bool> hasMultipleAudioLanguagesObservable = audioLanguageCountObservable
					.Select(count => count > 1);
				hasMultipleAudioLanguagesObservable.ToProperty(this, x => x.HasMultipleAudioLanguages, out this.hasMultipleAudioLanguages);

				// AudioFirstTrackLabel
				hasMultipleAudioLanguagesObservable
					.Select(hasMultipleAudioLanguages =>
					{
						return hasMultipleAudioLanguages ? PickerRes.FirstTrackOfEachLanguageRadioButton : PickerRes.FirstTrackRadioButton;
					})
					.ToProperty(this, x => x.AudioFirstTrackLabel, out this.audioFirstTrackLabel);

				// AudioAllTracksLabel
				hasMultipleAudioLanguagesObservable
					.Select(hasMultipleAudioLanguages =>
					{
						return hasMultipleAudioLanguages ? PickerRes.AllTracksForTheseLanguagesRadioButton : PickerRes.AllTracksForThisLanguageRadioButton;
					})
					.ToProperty(this, x => x.AudioAllTracksLabel, out this.audioAllTracksLabel);

				// HasNoAudioLanguages
				audioLanguageCountObservable
					.Select(count => count == 0)
					.ToProperty(this, x => x.HasNoAudioLanguages, out this.hasNoAudioLanguages);

				// HasMultipleSubtitleLanguages
				IObservable<int> subtitleLanguageCountObservable = this.SubtitleLanguages.CountChanged
					.StartWith(this.SubtitleLanguages.Count);
				IObservable<bool> hasMultipleSubtitleLanguagesObservable = subtitleLanguageCountObservable
					.Select(count => count > 1);
				hasMultipleSubtitleLanguagesObservable.ToProperty(this, x => x.HasMultipleSubtitleLanguages, out this.hasMultipleSubtitleLanguages);

				// SubtitleFirstTrackLabel
				hasMultipleSubtitleLanguagesObservable
					.Select(hasMultipleSubtitleLanguages =>
					{
						return hasMultipleSubtitleLanguages ? PickerRes.FirstTrackOfEachLanguageRadioButton : PickerRes.FirstTrackRadioButton;
					})
					.ToProperty(this, x => x.SubtitleFirstTrackLabel, out this.subtitleFirstTrackLabel);

				// SubtitleAllTracksLabel
				hasMultipleSubtitleLanguagesObservable
					.Select(hasMultipleSubtitleLanguages =>
					{
						return hasMultipleSubtitleLanguages ? PickerRes.AllTracksForTheseLanguagesRadioButton : PickerRes.AllTracksForThisLanguageRadioButton;
					})
					.ToProperty(this, x => x.SubtitleAllTracksLabel, out this.subtitleAllTracksLabel);

				// HasNoSubtitleLanguages
				subtitleLanguageCountObservable
					.Select(count => count == 0)
					.ToProperty(this, x => x.HasNoSubtitleLanguages, out this.hasNoSubtitleLanguages);

				// SubtitleModChoices
				this.WhenAnyValue(x => x.SubtitleSelectionMode, x => x.HasMultipleSubtitleLanguages, x => x.SubtitleLanguageAll, (selectionMode, hasMultipleLanguages, subtitleLanguageAll) =>
				{
					switch (selectionMode)
					{
						case SubtitleSelectionMode.Disabled:
						case SubtitleSelectionMode.None:
							return SubtitleModifierChoices.None;
						case SubtitleSelectionMode.First:
						case SubtitleSelectionMode.ForeignAudioSearch:
							return SubtitleModifierChoices.Single;
						case SubtitleSelectionMode.Language:
							if (hasMultipleLanguages)
							{
								return SubtitleModifierChoices.Multiple;
							}

							return subtitleLanguageAll ? SubtitleModifierChoices.Multiple : SubtitleModifierChoices.Single;
						case SubtitleSelectionMode.All:
							return SubtitleModifierChoices.Multiple;
						default:
							throw new ArgumentOutOfRangeException(nameof(selectionMode), selectionMode, null);
					}
				}).ToProperty(this, x => x.SubtitleModChoices, out this.subtitleModChoices);

				this.pickersService.WhenAnyValue(x => x.SelectedPicker.Picker.IsDefault, x => x.SelectedPicker.Picker.IsModified, (isDefault, isModified) => !isDefault && !isModified).ToProperty(this, x => x.DeleteButtonVisible, out this.deleteButtonVisible);

				this.pickersService.WhenAnyValue(x => x.SelectedPicker.Picker.DisplayName, x => x.SelectedPicker.Picker.IsModified, (displayName, isModified) =>
				{
					string windowTitle2 = string.Format(PickerRes.WindowTitle, displayName);
					if (isModified)
					{
						windowTitle2 += " *";
					}

					return windowTitle2;
				}).ToProperty(this, x => x.WindowTitle, out this.windowTitle);

				// Whenever the output directory override is disabled, used the config.
				this.WhenAnyValue(x => x.OutputDirectoryOverrideEnabled).Subscribe(directoryOverrideEnabled =>
				{
					if (!directoryOverrideEnabled)
					{
						this.OutputDirectoryOverride = Config.AutoNameOutputFolder;
					}
					else
					{
						this.OutputDirectoryOverride = this.Picker.OutputDirectoryOverride;
					}
				});

				// Whenever the name format override is disabled, used the config.
				this.WhenAnyValue(x => x.NameFormatOverrideEnabled).Subscribe(nameFormatOverrideEnabled =>
				{
					if (!nameFormatOverrideEnabled)
					{
						this.NameFormatOverride = AutoNameCustomFormat;
					}
					else
					{
						this.NameFormatOverride = this.Picker.NameFormatOverride;
					}
				});

				// Whenever UseEncodingPreset is false, set the selected VM to null.
				this.WhenAnyValue(x => x.UseEncodingPreset).Subscribe(useEncodingPreset => { this.PopulateEncodingPreset(useEncodingPreset); });

				// Update the underlying picker when our local properties change.
				// Don't need to raise another changed event as our local property setter already raises it.
				this.WhenAnyValue(x => x.OutputDirectoryOverride).Skip(1).Subscribe(directoryOverride =>
				{
					this.userModifyingOutputDirectory = true;
					this.UpdatePickerProperty(nameof(this.Picker.OutputDirectoryOverride), directoryOverride, raisePropertyChanged: false);
					this.outputPathService.GenerateOutputFileName();
					this.userModifyingOutputDirectory = false;
				});

				this.WhenAnyValue(x => x.NameFormatOverride).Skip(1).Subscribe(nameFormatOverride =>
				{
					this.userModifyingNameFormat = true;
					this.UpdatePickerProperty(nameof(this.Picker.NameFormatOverride), nameFormatOverride, raisePropertyChanged: false);
					this.outputPathService.GenerateOutputFileName();
					this.userModifyingNameFormat = false;
				});

				this.WhenAnyValue(x => x.SelectedPreset).Subscribe(selectedPreset =>
				{
					this.userModifyingEncodingPreset = true;
					string presetName = selectedPreset == null ? null : selectedPreset.Preset.Name;
					this.UpdatePickerProperty(nameof(this.Picker.EncodingPreset), presetName, raisePropertyChanged: false);
					if (this.UseEncodingPreset && selectedPreset != null)
					{
						if (!this.PresetsService.TryUpdateSelectedPreset(selectedPreset))
						{
							DispatchUtilities.BeginInvoke(() =>
							{
								this.SelectedPreset = this.PresetsService.SelectedPreset;
							});
						}
					}

					this.userModifyingEncodingPreset = false;
				});
			}
		}

		public string NameFormat => string.Format(CultureInfo.CurrentCulture, PickerRes.OverrideNameFormatLabel, NameTokenList);

		private void PopulateEncodingPreset(bool useEncodingPreset)
		{
			if (useEncodingPreset)
			{
				if (this.Picker.EncodingPreset == null)
				{
					this.SelectedPreset = this.presetsService.SelectedPreset;
				}
				else
				{
					PresetViewModel preset = this.presetsService.AllPresets.FirstOrDefault(p => p.Preset.Name == this.Picker.EncodingPreset);
					if (preset == null)
					{
						preset = this.presetsService.AllPresets.First();
					}

					this.SelectedPreset = preset;
				}
			}
			else
			{
				this.SelectedPreset = null;
			}
		}

		private void RegisterPickerProperties()
		{
			this.pickerProperties = new Dictionary<string, Action>();

			// These actions fire when the user changes a property.

			this.RegisterPickerProperty(nameof(this.Picker.OutputDirectoryOverrideEnabled), () =>
			{
				// When enabled, default to the global output folder.
				if (this.OutputDirectoryOverrideEnabled)
				{
					this.OutputDirectoryOverride = Config.AutoNameOutputFolder;
				}

				this.outputPathService.GenerateOutputFileName();
			});
			this.RegisterPickerProperty(nameof(this.Picker.OutputDirectoryOverride), () => { this.outputPathService.GenerateOutputFileName(); });
			this.RegisterPickerProperty(nameof(this.Picker.NameFormatOverrideEnabled), () =>
			{
				// When enabled, default global name format.
				if (this.NameFormatOverrideEnabled)
				{
					this.NameFormatOverride = AutoNameCustomFormat;
				}
			});
			this.RegisterPickerProperty(nameof(this.Picker.NameFormatOverride));
			this.RegisterPickerProperty(nameof(this.Picker.OutputToSourceDirectory));
			this.RegisterPickerProperty(nameof(this.Picker.PreserveFolderStructureInBatch));
			this.RegisterPickerProperty(nameof(this.Picker.TitleRangeSelectEnabled));
			this.RegisterPickerProperty(nameof(this.Picker.TitleRangeSelectStartMinutes));
			this.RegisterPickerProperty(nameof(this.Picker.TitleRangeSelectEndMinutes));
			this.RegisterPickerProperty(nameof(this.Picker.AudioSelectionMode), () =>
			{
				if (this.AudioSelectionMode == AudioSelectionMode.All)
				{
					this.AudioLanguages.Clear();

					this.HandleAudioLanguageUpdate();
				}

				if (this.AudioSelectionMode == AudioSelectionMode.Language)
				{
					this.AudioLanguages.Clear();
					this.AudioLanguages.Add(new LanguageViewModel(this) { Code = LanguageUtilities.GetDefaultLanguageCode() });

					this.HandleAudioLanguageUpdate();
				}
			});
			this.RegisterPickerProperty(nameof(this.Picker.AudioLanguageCodes));
			this.RegisterPickerProperty(nameof(this.Picker.AudioLanguageAll));
			this.RegisterPickerProperty(nameof(this.Picker.SubtitleSelectionMode), () =>
			{
				if (this.SubtitleSelectionMode == SubtitleSelectionMode.All)
				{
					this.SubtitleLanguages.Clear();

					this.HandleSubtitleLanguageUpdate();
				}

				if (this.SubtitleSelectionMode == SubtitleSelectionMode.Language)
				{
					this.SubtitleLanguages.Clear();
					this.SubtitleLanguages.Add(new LanguageViewModel(this) { Code = LanguageUtilities.GetDefaultLanguageCode() });

					this.HandleSubtitleLanguageUpdate();
				}
			});
			this.RegisterPickerProperty(nameof(this.Picker.SubtitleLanguageCodes));
			this.RegisterPickerProperty(nameof(this.Picker.SubtitleLanguageOnlyIfDifferent));
			this.RegisterPickerProperty(nameof(this.Picker.SubtitleLanguageAll));
			this.RegisterPickerProperty(nameof(this.Picker.SubtitleDefault), () =>
			{
				if (!this.HasMultipleSubtitleLanguages && this.SubtitleDefault && this.SubtitleBurnIn)
				{
					this.SubtitleBurnIn = false;
				}
			});
			this.RegisterPickerProperty(nameof(this.Picker.SubtitleBurnIn), () =>
			{
				if (!this.HasMultipleSubtitleLanguages && this.SubtitleBurnIn && this.SubtitleDefault)
				{
					this.SubtitleDefault = false;
				}
			});
			this.RegisterPickerProperty(nameof(this.Picker.SubtitleForcedOnly));
			this.RegisterPickerProperty(nameof(this.Picker.UseEncodingPreset));
			this.RegisterPickerProperty(nameof(this.Picker.EncodingPreset));
			this.RegisterPickerProperty(nameof(this.Picker.AutoQueueOnScan), () =>
			{
				if (!this.AutoQueueOnScan)
				{
					this.AutoEncodeOnScan = false;
				}
			});
			this.RegisterPickerProperty(nameof(this.Picker.AutoEncodeOnScan));
			this.RegisterPickerProperty(nameof(this.Picker.PostEncodeActionEnabled));
			this.RegisterPickerProperty(nameof(this.Picker.PostEncodeExecutable));
			this.RegisterPickerProperty(nameof(this.Picker.PostEncodeArguments));
		}

		private void RegisterPickerProperty(string propertyName, Action action = null)
		{
			this.pickerProperties.Add(propertyName, action);
		}

		public PickersService PickersService
		{
			get { return this.pickersService; }
		}

		public PresetsService PresetsService
		{
			get { return this.presetsService; }
		}

		public bool CanClose
		{
			get { return true; }
		}

		public Action Closing { get; set; }

		public Picker Picker
		{
			get { return this.pickersService.SelectedPicker.Picker; }
		}

		private ObservableAsPropertyHelper<string> windowTitle;
		public string WindowTitle => this.windowTitle.Value;

		private ObservableAsPropertyHelper<bool> deleteButtonVisible;
		public bool DeleteButtonVisible => this.deleteButtonVisible.Value;

		public bool ShowHelpMessage
		{
			get { return Config.ShowPickerWindowMessage; }

			set
			{
				Config.ShowPickerWindowMessage = value;
				this.RaisePropertyChanged();
			}
		}

		public bool OutputDirectoryOverrideEnabled
		{
			get { return this.Picker.OutputDirectoryOverrideEnabled; }
			set { this.UpdatePickerProperty(nameof(this.Picker.OutputDirectoryOverrideEnabled), value); }
		}

		// Local property that can hold placeholder values when OutputDirectoryOverrideEnabled is false.
		private string outputDirectoryOverride;

		public string OutputDirectoryOverride
		{
			get { return this.outputDirectoryOverride; }
			set { this.RaiseAndSetIfChanged(ref this.outputDirectoryOverride, value); }
		}

		public bool NameFormatOverrideEnabled
		{
			get { return this.Picker.NameFormatOverrideEnabled; }
			set { this.UpdatePickerProperty(nameof(this.Picker.NameFormatOverrideEnabled), value); }
		}

		// Local property that can hold placeholder values when NameFormatOverrideEnabled is false.
		private string nameFormatOverride;

		public string NameFormatOverride
		{
			get { return this.nameFormatOverride; }
			set { this.RaiseAndSetIfChanged(ref this.nameFormatOverride, value); }
		}

		public bool? OutputToSourceDirectory
		{
			get { return this.Picker.OutputToSourceDirectory; }
			set { this.UpdatePickerProperty(nameof(this.Picker.OutputToSourceDirectory), value); }
		}

		public bool? PreserveFolderStructureInBatch
		{
			get { return this.Picker.PreserveFolderStructureInBatch; }
			set { this.UpdatePickerProperty(nameof(this.Picker.PreserveFolderStructureInBatch), value); }
		}

		public bool TitleRangeSelectEnabled
		{
			get { return this.Picker.TitleRangeSelectEnabled; }
			set { this.UpdatePickerProperty(nameof(this.Picker.TitleRangeSelectEnabled), value); }
		}

		public int TitleRangeSelectStartMinutes
		{
			get { return this.Picker.TitleRangeSelectStartMinutes; }
			set { this.UpdatePickerProperty(nameof(this.Picker.TitleRangeSelectStartMinutes), value); }
		}

		public int TitleRangeSelectEndMinutes
		{
			get { return this.Picker.TitleRangeSelectEndMinutes; }
			set { this.UpdatePickerProperty(nameof(this.Picker.TitleRangeSelectEndMinutes), value); }
		}

		public AudioSelectionMode AudioSelectionMode
		{
			get { return this.Picker.AudioSelectionMode; }
			set { this.UpdatePickerProperty(nameof(this.Picker.AudioSelectionMode), value); }
		}

		public ReactiveList<LanguageViewModel> AudioLanguages { get; } = new ReactiveList<LanguageViewModel>();

		private ObservableAsPropertyHelper<bool> hasMultipleAudioLanguages;
		public bool HasMultipleAudioLanguages => this.hasMultipleAudioLanguages.Value;

		private ObservableAsPropertyHelper<bool> hasNoAudioLanguages;
		public bool HasNoAudioLanguages => this.hasNoAudioLanguages.Value;

		public bool AudioLanguageAll
		{
			get { return this.Picker.AudioLanguageAll; }
			set { this.UpdatePickerProperty(nameof(this.Picker.AudioLanguageAll), value); }
		}

		private ObservableAsPropertyHelper<string> audioFirstTrackLabel;
		public string AudioFirstTrackLabel => this.audioFirstTrackLabel.Value;

		private ObservableAsPropertyHelper<string> audioAllTracksLabel;
		public string AudioAllTracksLabel => this.audioAllTracksLabel.Value;

		public SubtitleSelectionMode SubtitleSelectionMode
		{
			get { return this.Picker.SubtitleSelectionMode; }
			set { this.UpdatePickerProperty(nameof(this.Picker.SubtitleSelectionMode), value); }
		}

		public ReactiveList<LanguageViewModel> SubtitleLanguages { get; } = new ReactiveList<LanguageViewModel>();

		private ObservableAsPropertyHelper<bool> hasMultipleSubtitleLanguages;
		public bool HasMultipleSubtitleLanguages => this.hasMultipleSubtitleLanguages.Value;

		private ObservableAsPropertyHelper<bool> hasNoSubtitleLanguages;
		public bool HasNoSubtitleLanguages => this.hasNoSubtitleLanguages.Value;

		public bool SubtitleLanguageOnlyIfDifferent
		{
			get { return this.Picker.SubtitleLanguageOnlyIfDifferent; }
			set { this.UpdatePickerProperty(nameof(this.Picker.SubtitleLanguageOnlyIfDifferent), value); }
		}

		public bool SubtitleLanguageAll
		{
			get { return this.Picker.SubtitleLanguageAll; }
			set { this.UpdatePickerProperty(nameof(this.Picker.SubtitleLanguageAll), value); }
		}

		private ObservableAsPropertyHelper<string> subtitleFirstTrackLabel;
		public string SubtitleFirstTrackLabel => this.subtitleFirstTrackLabel.Value;

		private ObservableAsPropertyHelper<string> subtitleAllTracksLabel;
		public string SubtitleAllTracksLabel => this.subtitleAllTracksLabel.Value;

		private ObservableAsPropertyHelper<SubtitleModifierChoices> subtitleModChoices;
		public SubtitleModifierChoices SubtitleModChoices => this.subtitleModChoices.Value;

		public bool SubtitleDefault
		{
			get { return this.Picker.SubtitleDefault; }
			set { this.UpdatePickerProperty(nameof(this.Picker.SubtitleDefault), value); }
		}

	    public bool SubtitleForcedOnly
	    {
	        get { return this.Picker.SubtitleForcedOnly; }
	        set { this.UpdatePickerProperty(nameof(this.Picker.SubtitleForcedOnly), value); }
	    }

		public bool SubtitleBurnIn
		{
			get { return this.Picker.SubtitleBurnIn; }
			set { this.UpdatePickerProperty(nameof(this.Picker.SubtitleBurnIn), value); }
		}

		public bool UseEncodingPreset
		{
			get { return this.Picker.UseEncodingPreset; }
			set { this.UpdatePickerProperty(nameof(this.Picker.UseEncodingPreset), value); }
		}

		private PresetViewModel selectedPreset;

		public PresetViewModel SelectedPreset
		{
			get { return this.selectedPreset; }
			set { this.RaiseAndSetIfChanged(ref this.selectedPreset, value); }
		}

		public void HandlePresetComboKey(KeyEventArgs keyEventArgs)
		{
			if (this.SelectedPreset == null || !this.UseEncodingPreset)
			{
				return;
			}

			int currentIndex = this.PresetsService.AllPresets.IndexOf(this.SelectedPreset);

			if (keyEventArgs.Key == Key.Up)
			{
				if (currentIndex > 0)
				{
					this.SelectedPreset = this.PresetsService.AllPresets[currentIndex - 1];

					keyEventArgs.Handled = true;
				}
			}
			else if (keyEventArgs.Key == Key.Down)
			{
				if (currentIndex < this.PresetsService.AllPresets.Count - 1)
				{
					this.SelectedPreset = this.PresetsService.AllPresets[currentIndex + 1];

					keyEventArgs.Handled = true;
				}
			}
		}

		public bool AutoQueueOnScan
		{
			get { return this.Picker.AutoQueueOnScan; }
			set { this.UpdatePickerProperty(nameof(this.Picker.AutoQueueOnScan), value); }
		}

		public bool AutoEncodeOnScan
		{
			get { return this.Picker.AutoEncodeOnScan; }
			set { this.UpdatePickerProperty(nameof(this.Picker.AutoEncodeOnScan), value); }
		}

		public bool PostEncodeActionEnabled
		{
			get { return this.Picker.PostEncodeActionEnabled; }
			set { this.UpdatePickerProperty(nameof(this.Picker.PostEncodeActionEnabled), value); }
		}

		public string PostEncodeExecutable
		{
			get { return this.Picker.PostEncodeExecutable; }
			set { this.UpdatePickerProperty(nameof(this.Picker.PostEncodeExecutable), value); }
		}

		public string PostEncodeArguments
		{
			get { return this.Picker.PostEncodeArguments; }
			set { this.UpdatePickerProperty(nameof(this.Picker.PostEncodeArguments), value); }
		}

		private ReactiveCommand dismissMessage;
		public ReactiveCommand DismissMessage
		{
			get
			{
				return this.dismissMessage ?? (this.dismissMessage = ReactiveCommand.Create(() =>
				{
					this.ShowHelpMessage = false;
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
						this.pickersService.SavePicker();
					},
					this.pickersService.WhenAnyValue(x => x.SelectedPicker.Picker.IsDefault).Select(isDefault => !isDefault)));
			}
		}


		private ReactiveCommand saveAs;
		public ReactiveCommand SaveAs
		{
			get
			{
				return this.saveAs ?? (this.saveAs = ReactiveCommand.Create(() =>
				{
					var dialogVM = new ChooseNameViewModel(MiscRes.ChooseNamePicker, this.pickersService.Pickers.Skip(1).Select(p => p.Picker.Name));
					dialogVM.Name = this.Picker.DisplayName;
					Ioc.Get<IWindowManager>().OpenDialog(dialogVM, this);

					if (dialogVM.DialogResult)
					{
						string newPickerName = dialogVM.Name;

						this.pickersService.SavePickerAs(newPickerName);
					}
				}));
			}
		}

		private ReactiveCommand rename;
		public ReactiveCommand Rename
		{
			get
			{
				return this.rename ?? (this.rename = ReactiveCommand.Create(() =>
				{
					var dialogVM = new ChooseNameViewModel(MiscRes.ChooseNamePicker, this.pickersService.Pickers.Skip(1).Select(p => p.Picker.Name));
					dialogVM.Name = this.Picker.DisplayName;
					Ioc.Get<IWindowManager>().OpenDialog(dialogVM, this);

					if (dialogVM.DialogResult)
					{
						string newPickerName = dialogVM.Name;
						this.pickersService.SelectedPicker.Picker.Name = newPickerName;

						this.pickersService.SavePicker();
					}
				}));
			}
		}

		private ReactiveCommand delete;
		public ReactiveCommand Delete
		{
			get
			{
				return this.delete ?? (this.delete = ReactiveCommand.Create(
					() =>
					{
						if (this.Picker.IsModified)
						{
							// Revert
							MessageBoxResult dialogResult = Utilities.MessageBox.Show(this, MainRes.RevertConfirmMessage, MainRes.RevertConfirmTitle, MessageBoxButton.YesNo);
							if (dialogResult == MessageBoxResult.Yes)
							{
								this.pickersService.RevertPicker();
							}
						}
						else
						{
							// Delete
							MessageBoxResult dialogResult = Utilities.MessageBox.Show(this, MainRes.RemoveConfirmMessage, MainRes.RemoveConfirmTitle, MessageBoxButton.YesNo);
							if (dialogResult == MessageBoxResult.Yes)
							{
								this.pickersService.DeletePicker();
							}
						}
					},
					this.pickersService.WhenAnyValue(
						x => x.SelectedPicker.Picker.IsModified,
						x => x.SelectedPicker.Picker.IsDefault,
						(isModified, isDefault) => isModified || !isDefault)));
			}
		}

		private ReactiveCommand pickOutputDirectory;
		public ReactiveCommand PickOutputDirectory
		{
			get
			{
				return this.pickOutputDirectory ?? (this.pickOutputDirectory = ReactiveCommand.Create(() =>
				{
					string overrideFolder = FileService.Instance.GetFolderName(this.OutputDirectoryOverride, MainRes.OutputDirectoryPickerText);
					if (overrideFolder != null)
					{
						this.OutputDirectoryOverride = overrideFolder;
					}
				}));
			}
		}

		private ReactiveCommand addAudioLanguage;
		public ReactiveCommand AddAudioLanguage
		{
			get
			{
				return this.addAudioLanguage ?? (this.addAudioLanguage = ReactiveCommand.Create(() =>
				{
					this.AddLanguage(this.AudioLanguages);
					this.HandleAudioLanguageUpdate();
				}));
			}
		}

		private ReactiveCommand addSubtitleLanguage;
		public ReactiveCommand AddSubtitleLanguage
		{
			get
			{
				return this.addSubtitleLanguage ?? (this.addSubtitleLanguage = ReactiveCommand.Create(() =>
				{
					this.AddLanguage(this.SubtitleLanguages);
					this.HandleSubtitleLanguageUpdate();
				}));
			}
		}

		private void AddLanguage(ReactiveList<LanguageViewModel> list)
		{
			list.Add(new LanguageViewModel(this) { Code = LanguageUtilities.GetDefaultLanguageCode(list.Select(l => l.Code).ToList()) });
		}

		private ReactiveCommand pickPostEncodeExecutable;
		public ReactiveCommand PickPostEncodeExecutable
		{
			get
			{
				return this.pickPostEncodeExecutable ?? (this.pickPostEncodeExecutable = ReactiveCommand.Create(() =>
				{
					string initialDirectory = null;
					if (!string.IsNullOrEmpty(this.PostEncodeExecutable))
					{
						try
						{
							initialDirectory = Path.GetDirectoryName(this.PostEncodeExecutable);
						}
						catch (Exception)
						{
							// Ignore and use null
						}
					}

					string executablePath = FileService.Instance.GetFileNameLoad(initialDirectory: initialDirectory);
					if (executablePath != null)
					{
						this.PostEncodeExecutable = executablePath;
					}
				}));
			}
		}

		private static string AutoNameCustomFormat
		{
			get
			{
				if (Config.AutoNameCustomFormat)
				{
					return Config.AutoNameCustomFormatString;
				}

				return string.Empty;
			}
		}

		private void HandleAudioLanguageUpdate()
		{
			IList<string> languages = GetLanguageList(this.AudioLanguages);
			this.UpdatePickerProperty(nameof(this.Picker.AudioLanguageCodes), languages, raisePropertyChanged: false);
		}

		private void HandleSubtitleLanguageUpdate()
		{
			IList<string> languages = GetLanguageList(this.SubtitleLanguages);
			this.UpdatePickerProperty(nameof(this.Picker.SubtitleLanguageCodes), languages, raisePropertyChanged: false);
		}

		private static IList<string> GetLanguageList(IEnumerable<LanguageViewModel> languageViewModels)
		{
			return languageViewModels.Select(l => l.Code).ToList();
		}

		public void RemoveAudioLanguage(LanguageViewModel viewModel)
		{
			this.AudioLanguages.Remove(viewModel);
			this.HandleAudioLanguageUpdate();
		}

		public void RemoveSubtitleLanguage(LanguageViewModel viewModel)
		{
			this.SubtitleLanguages.Remove(viewModel);
			this.HandleSubtitleLanguageUpdate();
		}

		private void RaiseAllChanged()
		{
			foreach (string key in this.pickerProperties.Keys)
			{
				this.RaisePropertyChanged(key);
			}
		}

		private void UpdatePickerProperty<T>(string propertyName, T value, bool raisePropertyChanged = true)
		{
			if (!this.pickerProperties.ContainsKey(propertyName))
			{
				throw new ArgumentException("UpdatePickerProperty called on " + propertyName + " without registering.");
			}

			if (!this.autoChangeTracker.OperationInProgress)
			{
				bool createPicker = this.Picker.IsDefault;
				if (createPicker)
				{
					this.pickersService.AutoCreatePicker();
				}
				else if (!this.Picker.IsModified)
				{
					// Clone the picker so we modify a different copy.
					Picker newPicker = new Picker();
					newPicker.InjectFrom<CloneInjection>(this.Picker);

					if (!newPicker.IsModified)
					{
						this.pickersService.ModifyPicker(newPicker);
					}
				}
			}

			// Update the value and raise PropertyChanged
			typeof(Picker).GetProperty(propertyName).SetValue(this.Picker, value);

			if (raisePropertyChanged)
			{
				this.RaisePropertyChanged(propertyName);
			}

			if (!this.autoChangeTracker.OperationInProgress)
			{
				// If we have an action registered to update dependent properties, do it
				Action action = this.pickerProperties[propertyName];
				if (action != null)
				{
					// Protect against update loops
					using (this.autoChangeTracker.TrackAutoChange())
					{
						action();
					}
				}

				this.pickersService.SavePickersToStorage();
			}
		}
	}

	public enum SubtitleModifierChoices
	{
		None,
		Single,
		Multiple
	}
}
