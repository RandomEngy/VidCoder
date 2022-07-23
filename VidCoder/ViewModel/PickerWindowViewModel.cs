using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reactive;
using System.Reactive.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using DynamicData;
using DynamicData.Aggregation;
using DynamicData.Binding;
using Microsoft.AnyContainer;
using Omu.ValueInjecter;
using ReactiveUI;
using VidCoder.Extensions;
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
		private readonly OutputPathService outputPathService = StaticResolver.Resolve<OutputPathService>();

		private AutoChangeTracker autoChangeTracker = new AutoChangeTracker();

		private Dictionary<string, Action> pickerProperties;
		private bool userModifyingOutputDirectory;
		private bool userModifyingEncodingPreset;

		// True if we are in the middle of modifying a picker.
		private bool modifyingPicker;

		public PickerWindowViewModel()
		{
			using (this.autoChangeTracker.TrackAutoChange())
			{
				this.outputDirectory = this.Picker.OutputDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);

				this.RegisterPickerProperties();

				this.PickersService.WhenAnyValue(x => x.SelectedPicker.Picker)
					.Subscribe(x =>
					{
						using (this.autoChangeTracker.TrackAutoChange())
						{
							this.RaiseAllChanged();
							this.RefreshSourceNameCleanupPreview();

							// When we are swapping active pickers, update the local properties.
							if (!this.userModifyingOutputDirectory)
							{
								this.OutputDirectory = this.Picker.OutputDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
							}

							// When we are swapping active pickers, update the local properties.
							if (!this.userModifyingEncodingPreset)
							{
								this.PopulateEncodingPreset(this.Picker.UseEncodingPreset);
							}
						}
					});

				this.PickersService.WhenAnyValue(x => x.SelectedPicker.Picker.WordBreakCharacters).Subscribe(wordBreakCharacters =>
				{
					using (this.autoChangeTracker.TrackAutoChange())
					{
						this.WordBreakCharacterChoices = new List<WordBreakCharacterChoice>
						{
							new WordBreakCharacterChoice(this, wordBreakCharacters.Contains(" ")) { Character = " ", CharacterWord = PickerRes.WordBreakCharacter_Space, DisplayUsingWord = true  },
							new WordBreakCharacterChoice(this, wordBreakCharacters.Contains("_")) { Character = "_", CharacterWord = PickerRes.WordBreakCharacter_Underscore },
							new WordBreakCharacterChoice(this, wordBreakCharacters.Contains(".")) { Character = ".", CharacterWord = PickerRes.WordBreakCharacter_Dot }
						};
					}
				});

				this.MainViewModel.WhenAnyValue(x => x.SourceName)
					.Subscribe(x =>
					{
						this.RefreshSourceNameCleanupPreview();
					});

				this.PickersService.WhenAnyValue(x => x.SelectedPicker.Picker.AudioLanguageCodes).Subscribe(audioLanguageCodes =>
				{
					using (this.autoChangeTracker.TrackAutoChange())
					{
						this.audioLanguages.Edit(audioLanguagesInnerList =>
						{
							audioLanguagesInnerList.Clear();

							if (audioLanguageCodes == null)
							{
								audioLanguagesInnerList.Add(new LanguageViewModel(this) { Code = LanguageUtilities.GetDefaultLanguageCode() });
								return;
							}

							audioLanguagesInnerList.AddRange(audioLanguageCodes.Select(l => new LanguageViewModel(this) { Code = l }));
						});
					}
				});

				var audioLanguagesObservable = this.audioLanguages.Connect();
				audioLanguagesObservable.Bind(this.AudioLanguagesBindable).Subscribe();
				audioLanguagesObservable.WhenAnyPropertyChanged().Subscribe(_ =>
				{
					if (!this.autoChangeTracker.OperationInProgress)
					{
						this.HandleAudioLanguageUpdate();
					}
				});

				this.PickersService.WhenAnyValue(x => x.SelectedPicker.Picker).Subscribe(picker =>
				{
					if (!this.modifyingPicker)
					{
						using (this.autoChangeTracker.TrackAutoChange())
						{
							this.audioTrackNames.Edit(audioTrackNamesInnerList =>
							{
								audioTrackNamesInnerList.Clear();

								if (picker.AudioTrackNames == null)
								{
									audioTrackNamesInnerList.Add(new TrackNameViewModel(this, 1, string.Empty));
									return;
								}

								int trackNumber = 1;
								audioTrackNamesInnerList.AddRange(picker.AudioTrackNames.Select(n => new TrackNameViewModel(this, trackNumber++, n)));
							});
						}
					}
				});

				var audioTrackNamesObservable = this.audioTrackNames.Connect();
				audioTrackNamesObservable.Bind(this.AudioTrackNamesBindable).Subscribe();

				this.PickersService.WhenAnyValue(x => x.SelectedPicker.Picker.SubtitleLanguageCodes).Subscribe(subtitleLanguageCodes =>
				{
					using (this.autoChangeTracker.TrackAutoChange())
					{
						this.subtitleLanguages.Edit(subtitleLanguagesInnerList =>
						{
							subtitleLanguagesInnerList.Clear();

							if (subtitleLanguageCodes == null)
							{
								subtitleLanguagesInnerList.Add(new LanguageViewModel(this) { Code = LanguageUtilities.GetDefaultLanguageCode() });
								return;
							}

							subtitleLanguagesInnerList.AddRange(subtitleLanguageCodes.Select(l => new LanguageViewModel(this) { Code = l }));
						});
					}
				});

				var subtitleLanguagesObservable = this.subtitleLanguages.Connect();
				subtitleLanguagesObservable.Bind(this.SubtitleLanguagesBindable).Subscribe();
				subtitleLanguagesObservable.WhenAnyPropertyChanged().Subscribe(_ =>
				{
					if (!this.autoChangeTracker.OperationInProgress)
					{
						this.HandleSubtitleLanguageUpdate();
					}
				});

				this.PickersService.WhenAnyValue(x => x.SelectedPicker.Picker).Subscribe(picker =>
				{
					if (!this.modifyingPicker)
					{
						using (this.autoChangeTracker.TrackAutoChange())
						{
							this.subtitleTrackNames.Edit(subtitleTrackNamesInnerList =>
							{
								subtitleTrackNamesInnerList.Clear();

								if (picker.SubtitleTrackNames == null)
								{
									subtitleTrackNamesInnerList.Add(new TrackNameViewModel(this, 1, string.Empty));
									return;
								}

								int trackNumber = 1;
								subtitleTrackNamesInnerList.AddRange(picker.SubtitleTrackNames.Select(n => new TrackNameViewModel(this, trackNumber++, n)));
							});
						}
					}
				});

				var subtitleTrackNamesObservable = this.subtitleTrackNames.Connect();
				subtitleTrackNamesObservable.Bind(this.SubtitleTrackNamesBindable).Subscribe();

				// HasMultipleAudioLanguages
				IObservable<int> audioLanguageCountObservable = audioLanguagesObservable
					.Count()
					.StartWith(this.audioLanguages.Count);
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

				// BurnInVisible
				this.WhenAnyValue(x => x.SubtitleSelectionMode, x => x.SubtitleAddForeignAudioScan, (subtitleSelectionMode, subtitleForeignAudioScan) =>
				{
					return subtitleSelectionMode != SubtitleSelectionMode.None || subtitleForeignAudioScan;
				}).ToProperty(this, x => x.BurnInVisible, out this.burnInVisible);

				// HasMultipleSubtitleLanguages
				IObservable<int> subtitleLanguageCountObservable = subtitleLanguagesObservable
					.Count()
					.StartWith(this.subtitleLanguages.Count);
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

				// SubtitleQuantityClass
				this.WhenAnyValue(x => x.SubtitleSelectionMode, x => x.SubtitleIndices, x => x.HasMultipleSubtitleLanguages, x => x.SubtitleLanguageAll, (selectionMode, subtitleIndices, hasMultipleLanguages, subtitleLanguageAll) =>
				{
					switch (selectionMode)
					{
						case SubtitleSelectionMode.Disabled:
						case SubtitleSelectionMode.None:
							return SubtitleQuantityClass.None;
						case SubtitleSelectionMode.First:
						case SubtitleSelectionMode.ForeignAudioSearch:
							return SubtitleQuantityClass.Single;
						case SubtitleSelectionMode.ByIndex:
							return ParseUtilities.ParseCommaSeparatedListToPositiveIntegers(subtitleIndices).Count > 1 ? SubtitleQuantityClass.Multiple : SubtitleQuantityClass.Single;
						case SubtitleSelectionMode.Language:
							if (hasMultipleLanguages)
							{
								return SubtitleQuantityClass.Multiple;
							}

							return subtitleLanguageAll ? SubtitleQuantityClass.Multiple : SubtitleQuantityClass.Single;
						case SubtitleSelectionMode.All:
							return SubtitleQuantityClass.Multiple;
						default:
							throw new ArgumentOutOfRangeException(nameof(selectionMode), selectionMode, null);
					}
				}).ToProperty(this, x => x.SubtitleQuantityClass, out this.subtitleQuantityClass);

				// ShowDefaultCheckBox
				this.WhenAnyValue(x => x.SubtitleQuantityClass, x => x.SubtitleSelectionMode, x => x.SubtitleBurnInSelection, (subtitleQuantityClass, selectionMode, subtitleBurnInSelection) =>
				{
					return subtitleQuantityClass == SubtitleQuantityClass.Single && selectionMode != SubtitleSelectionMode.ByIndex && !subtitleBurnInSelection.FirstTrackIncluded();
				}).ToProperty(this, x => x.ShowDefaultCheckBox, out this.showDefaultCheckBox);

				// ShowMarkFirstAsDefaultCheckBox
				this.WhenAnyValue(x => x.SubtitleQuantityClass, x => x.SubtitleSelectionMode, x => x.SubtitleBurnInSelection, (subtitleQuantityClass, selectionMode, subtitleBurnInSelection) =>
				{
					return subtitleQuantityClass == SubtitleQuantityClass.Multiple && selectionMode != SubtitleSelectionMode.ByIndex && !subtitleBurnInSelection.FirstTrackIncluded();
				}).ToProperty(this, x => x.ShowMarkFirstAsDefaultCheckBox, out this.showMarkFirstAsDefaultCheckBox);

				// SourceFileRemovalConfirmationVisible
				this.WhenAnyValue(x => x.SourceFileRemoval, x => x.SourceFileRemovalTiming, (sourceFileRemoval, sourceFileRemovalTiming) =>
				{
					return sourceFileRemoval != SourceFileRemoval.Disabled && sourceFileRemovalTiming == SourceFileRemovalTiming.AfterClearingCompletedItems;
				}).ToProperty(this, x => x.SourceFileRemovalConfirmationVisible, out this.sourceFileRemovalConfirmationVisible);

				this.PickersService.WhenAnyValue(x => x.SelectedPicker.Picker.IsDefault, x => x.SelectedPicker.Picker.IsModified, (isDefault, isModified) => !isDefault && !isModified).ToProperty(this, x => x.DeleteButtonVisible, out this.deleteButtonVisible);

				this.PickersService.WhenAnyValue(x => x.SelectedPicker.Picker.DisplayName, x => x.SelectedPicker.Picker.IsModified, (displayName, isModified) =>
				{
					string windowTitle2 = string.Format(PickerRes.WindowTitle, displayName);
					if (isModified)
					{
						windowTitle2 += " *";
					}

					return windowTitle2;
				}).ToProperty(this, x => x.WindowTitle, out this.windowTitle);

				// Whenever UseEncodingPreset is false, set the selected VM to null.
				this.WhenAnyValue(x => x.UseEncodingPreset).Subscribe(useEncodingPreset => { this.PopulateEncodingPreset(useEncodingPreset); });

				// Update the underlying picker when our local properties change.
				// Don't need to raise another changed event as our local property setter already raises it.
				this.WhenAnyValue(x => x.OutputDirectory).Skip(1).Subscribe(directoryOverride =>
				{
					this.userModifyingOutputDirectory = true;
					this.UpdatePickerProperty(nameof(this.Picker.OutputDirectory), directoryOverride, raisePropertyChanged: false);
					this.outputPathService.GenerateOutputFileName();
					this.userModifyingOutputDirectory = false;
				});

				this.WhenAnyValue(x => x.SelectedPreset).Skip(1).Subscribe(selectedPreset =>
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

				this.RefreshBurnInChoices();
			}
		}

		public IPickerWindowView View { get; set; }

		public MainViewModel MainViewModel { get; } = StaticResolver.Resolve<MainViewModel>();

		public string NameFormat => string.Format(CultureInfo.CurrentCulture, PickerRes.OverrideNameFormatLabel, NameTokenList);

		private void PopulateEncodingPreset(bool useEncodingPreset)
		{
			if (useEncodingPreset)
			{
				if (this.Picker.EncodingPreset == null)
				{
					this.SelectedPreset = this.PresetsService.SelectedPreset;
				}
				else
				{
					PresetViewModel preset = this.PresetsService.AllPresets.FirstOrDefault(p => p.Preset.Name == this.Picker.EncodingPreset);
					if (preset == null)
					{
						preset = this.PresetsService.AllPresets.First();
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

			this.RegisterPickerProperty(nameof(this.Picker.OutputDirectory), () => this.outputPathService.GenerateOutputFileName());
			this.RegisterPickerProperty(nameof(this.Picker.OutputFileNameFormat), () => this.outputPathService.GenerateOutputFileName());
			this.RegisterPickerProperty(nameof(this.Picker.UseCustomFileNameFormat), () => this.outputPathService.GenerateOutputFileName());
			this.RegisterPickerProperty(nameof(this.Picker.OutputToSourceDirectory), () => this.outputPathService.GenerateOutputFileName());
			this.RegisterPickerProperty(nameof(this.Picker.PreserveFolderStructureInBatch));
			this.RegisterPickerProperty(nameof(this.Picker.WhenFileExistsSingle));
			this.RegisterPickerProperty(nameof(this.Picker.WhenFileExistsBatch));
			this.RegisterPickerProperty(nameof(this.Picker.ChangeWordSeparator), () => this.RefreshOutputPathAndSourceNameCleanupPreview());
			this.RegisterPickerProperty(nameof(this.Picker.WordSeparator), () => this.RefreshOutputPathAndSourceNameCleanupPreview());
			this.RegisterPickerProperty(nameof(this.Picker.ChangeTitleCaptialization), () => this.RefreshOutputPathAndSourceNameCleanupPreview());
			this.RegisterPickerProperty(nameof(this.Picker.TitleCapitalization), () => this.RefreshOutputPathAndSourceNameCleanupPreview());
			this.RegisterPickerProperty(nameof(this.Picker.OnlyChangeTitleCapitalizationWhenAllSame), () => this.RefreshOutputPathAndSourceNameCleanupPreview());
			this.RegisterPickerProperty(nameof(this.Picker.WordBreakCharacters), () => this.RefreshOutputPathAndSourceNameCleanupPreview());
			this.RegisterPickerProperty(nameof(this.Picker.VideoFileExtensions));
			this.RegisterPickerProperty(nameof(this.Picker.IgnoreFilesBelowMbEnabled));
			this.RegisterPickerProperty(nameof(this.Picker.IgnoreFilesBelowMb));
			this.RegisterPickerProperty(nameof(this.Picker.TitleRangeSelectEnabled));
			this.RegisterPickerProperty(nameof(this.Picker.TitleRangeSelectStartMinutes));
			this.RegisterPickerProperty(nameof(this.Picker.TitleRangeSelectEndMinutes));
			this.RegisterPickerProperty(nameof(this.Picker.PickerTimeRangeMode));
			this.RegisterPickerProperty(nameof(this.Picker.ChapterRangeStart));
			this.RegisterPickerProperty(nameof(this.Picker.ChapterRangeEnd));
			this.RegisterPickerProperty(nameof(this.Picker.TimeRangeStart));
			this.RegisterPickerProperty(nameof(this.Picker.TimeRangeEnd));
			this.RegisterPickerProperty(nameof(this.Picker.AudioSelectionMode), () =>
			{
				if (this.AudioSelectionMode == AudioSelectionMode.All)
				{
					this.audioLanguages.Clear();

					this.HandleAudioLanguageUpdate();
				}

				if (this.AudioSelectionMode == AudioSelectionMode.Language)
				{
					this.audioLanguages.Clear();
					this.audioLanguages.Add(new LanguageViewModel(this) { Code = LanguageUtilities.GetDefaultLanguageCode() });

					this.HandleAudioLanguageUpdate();
				}
			});
			this.RegisterPickerProperty(nameof(this.Picker.AudioLanguageCodes));
			this.RegisterPickerProperty(nameof(this.Picker.AudioIndices));
			this.RegisterPickerProperty(nameof(this.Picker.AudioLanguageAll));
			this.RegisterPickerProperty(nameof(this.Picker.UseCustomAudioTrackNames), () =>
			{
				if (this.audioTrackNames.Count == 0)
				{
					this.audioTrackNames.Add(new TrackNameViewModel(this, 1, string.Empty));
				}

				this.View.FocusAudioTrackName(0);
			});
			this.RegisterPickerProperty(nameof(this.Picker.AudioTrackNames));
			this.RegisterPickerProperty(nameof(this.Picker.SubtitleSelectionMode), () =>
			{
				if (this.SubtitleSelectionMode == SubtitleSelectionMode.All)
				{
					this.subtitleLanguages.Clear();

					this.HandleSubtitleLanguageUpdate();
				}

				if (this.SubtitleSelectionMode == SubtitleSelectionMode.Language)
				{
					this.subtitleLanguages.Clear();
					this.subtitleLanguages.Add(new LanguageViewModel(this) { Code = LanguageUtilities.GetDefaultLanguageCode() });

					this.HandleSubtitleLanguageUpdate();
				}

				this.RefreshBurnInChoicesAndUpdateSelection();
			});
			this.RegisterPickerProperty(nameof(this.Picker.SubtitleIndices));
			this.RegisterPickerProperty(nameof(this.Picker.SubtitleDefaultIndex));
			this.RegisterPickerProperty(nameof(this.Picker.SubtitleLanguageCodes));
			this.RegisterPickerProperty(nameof(this.Picker.SubtitleLanguageOnlyIfDifferent));
			this.RegisterPickerProperty(nameof(this.Picker.SubtitleLanguageAll));
			this.RegisterPickerProperty(nameof(this.Picker.SubtitleDefault), () =>
			{
				if (!this.HasMultipleSubtitleLanguages && this.SubtitleDefault)
				{
					if (this.SubtitleBurnInSelection == SubtitleBurnInSelection.First)
					{
						this.SubtitleBurnInSelection = SubtitleBurnInSelection.None;
					}
					else if (this.SubtitleBurnInSelection == SubtitleBurnInSelection.ForeignAudioTrackElseFirst)
					{
						this.SubtitleBurnInSelection = SubtitleBurnInSelection.ForeignAudioTrack;
					}
				}
			});
			this.RegisterPickerProperty(nameof(this.Picker.SubtitleAddForeignAudioScan), () =>
			{
				this.RefreshBurnInChoicesAndUpdateSelection();
			});
			this.RegisterPickerProperty(nameof(this.Picker.SubtitleBurnInSelection), () =>
			{
				// If we've selected an 
				if (!this.HasMultipleSubtitleLanguages
					&& (this.SubtitleBurnInSelection == SubtitleBurnInSelection.First || this.SubtitleBurnInSelection == SubtitleBurnInSelection.ForeignAudioTrackElseFirst)
					&& this.SubtitleDefault)
				{
					this.SubtitleDefault = false;
				}
			});
			this.RegisterPickerProperty(nameof(this.Picker.SubtitleForcedOnly));
			this.RegisterPickerProperty(nameof(this.Picker.UseCustomSubtitleTrackNames), () =>
			{
				if (this.subtitleTrackNames.Count == 0)
				{
					this.subtitleTrackNames.Add(new TrackNameViewModel(this, 1, string.Empty));
				}

				this.View.FocusSubtitleTrackName(0);
			});
			this.RegisterPickerProperty(nameof(this.Picker.SubtitleTrackNames));
			this.RegisterPickerProperty(nameof(this.Picker.EnableExternalSubtitleImport));
			this.RegisterPickerProperty(nameof(this.Picker.ExternalSubtitleImportLanguage));
			this.RegisterPickerProperty(nameof(this.Picker.ExternalSubtitleImportDefault));
			this.RegisterPickerProperty(nameof(this.Picker.ExternalSubtitleImportBurnIn), () =>
			{
				if (this.ExternalSubtitleImportBurnIn)
				{
					this.ExternalSubtitleImportDefault = false;
				}
			});
			this.RegisterPickerProperty(nameof(this.Picker.PassThroughMetadata));
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
			this.RegisterPickerProperty(nameof(this.Picker.SourceFileRemoval));
			this.RegisterPickerProperty(nameof(this.Picker.SourceFileRemovalTiming));
			this.RegisterPickerProperty(nameof(this.Picker.SourceFileRemovalConfirmation));
		}

		private void RefreshBurnInChoicesAndUpdateSelection()
		{
			var oldSubtitleBurnInSelection = this.SubtitleBurnInSelection;

			this.RefreshBurnInChoices();

			if (this.SubtitleBurnInChoices.Any(c => c.Value == oldSubtitleBurnInSelection))
			{
				this.SubtitleBurnInSelection = oldSubtitleBurnInSelection;
			}
			else
			{
				this.SubtitleBurnInSelection = this.SubtitleAddForeignAudioScan ? SubtitleBurnInSelection.ForeignAudioTrack : SubtitleBurnInSelection.None;
			}
		}

		private void RegisterPickerProperty(string propertyName, Action action = null)
		{
			this.pickerProperties.Add(propertyName, action);
		}

		public PickersService PickersService { get; } = StaticResolver.Resolve<PickersService>();

		public PresetsService PresetsService { get; } = StaticResolver.Resolve<PresetsService>();

		/// <summary>
		/// The PresetTreeViewContainer binds to this to get the ViewModel tree of presets.
		/// </summary>
		public ObservableCollection<PresetFolderViewModel> AllPresetsTree => this.PresetsService.AllPresetsTree;

		public bool CanClose
		{
			get { return true; }
		}

		public Action Closing { get; set; }

		public Picker Picker
		{
			get { return this.PickersService.SelectedPicker.Picker; }
		}

		private ObservableAsPropertyHelper<string> windowTitle;
		public string WindowTitle => this.windowTitle.Value;

		private ObservableAsPropertyHelper<bool> deleteButtonVisible;
		public bool DeleteButtonVisible => this.deleteButtonVisible.Value;

		// Local property that can hold placeholder values when OutputDirectory is null on the picker.
		private string outputDirectory;

		public string OutputDirectory
		{
			get => this.outputDirectory;
			set => this.RaiseAndSetIfChanged(ref this.outputDirectory, value);
		}

		public bool UseCustomFileNameFormat
		{
			get => this.Picker.UseCustomFileNameFormat;
			set => this.UpdatePickerProperty(nameof(this.Picker.UseCustomFileNameFormat), value);
		}

		public string OutputFileNameFormat
		{
			get => this.Picker.OutputFileNameFormat;
			set => this.UpdatePickerProperty(nameof(this.Picker.OutputFileNameFormat), value);
		}

		public string AvailableOptionsText => string.Format(OptionsRes.FileNameFormatOptions, "{source} {title} {range} {preset} {date} {time} {quality} {parent} {titleduration}");

		public List<ComboChoice<WhenFileExists>> WhenFileExistsSingleChoices { get; } = new List<ComboChoice<WhenFileExists>>
		{
			new ComboChoice<WhenFileExists>(WhenFileExists.Prompt, EnumsRes.WhenFileExists_Prompt),
			new ComboChoice<WhenFileExists>(WhenFileExists.Overwrite, EnumsRes.WhenFileExists_Overwrite),
			new ComboChoice<WhenFileExists>(WhenFileExists.AutoRename, EnumsRes.WhenFileExists_AutoRename),
		};

		public WhenFileExists WhenFileExistsSingle
		{
			get => this.Picker.WhenFileExistsSingle;
			set => this.UpdatePickerProperty(nameof(this.Picker.WhenFileExistsSingle), value);
		}

		public List<ComboChoice<WhenFileExists>> WhenFileExistsBatchChoices { get; } = new List<ComboChoice<WhenFileExists>>
		{
			new ComboChoice<WhenFileExists>(WhenFileExists.Overwrite, EnumsRes.WhenFileExists_Overwrite),
			new ComboChoice<WhenFileExists>(WhenFileExists.AutoRename, EnumsRes.WhenFileExists_AutoRename),
		};

		public WhenFileExists WhenFileExistsBatch
		{
			get => this.Picker.WhenFileExistsBatch;
			set => this.UpdatePickerProperty(nameof(this.Picker.WhenFileExistsBatch), value);
		}

		public bool? OutputToSourceDirectory
		{
			get => this.Picker.OutputToSourceDirectory;
			set => this.UpdatePickerProperty(nameof(this.Picker.OutputToSourceDirectory), value);
		}

		public bool? PreserveFolderStructureInBatch
		{
			get => this.Picker.PreserveFolderStructureInBatch;
			set => this.UpdatePickerProperty(nameof(this.Picker.PreserveFolderStructureInBatch), value);
		}

		public bool ChangeWordSeparator
		{
			get => this.Picker.ChangeWordSeparator;
			set => this.UpdatePickerProperty(nameof(this.Picker.ChangeWordSeparator), value);
		}

		public List<ComboChoice> WordSeparatorChoices { get; } = new List<ComboChoice>
		{
			new ComboChoice(" ", PickerRes.WordBreakCharacter_Space),
			new ComboChoice("_", "_"),
			new ComboChoice(".", ".")
		};

		public string WordSeparator
		{
			get => this.Picker.WordSeparator;
			set => this.UpdatePickerProperty(nameof(this.Picker.WordSeparator), value);
		}

		public bool ChangeTitleCaptialization
		{
			get => this.Picker.ChangeTitleCaptialization;
			set => this.UpdatePickerProperty(nameof(this.Picker.ChangeTitleCaptialization), value);
		}

		public List<ComboChoice<TitleCapitalizationChoice>> TitleCaptializationChoices { get; } = new List<ComboChoice<TitleCapitalizationChoice>>
		{
			new ComboChoice<TitleCapitalizationChoice>(TitleCapitalizationChoice.EveryWord, EnumsRes.TitleCapitalizationChoice_EveryWord),
			new ComboChoice<TitleCapitalizationChoice>(TitleCapitalizationChoice.FirstWord, EnumsRes.TitleCapitalizationChoice_FirstWord),
		};

		public TitleCapitalizationChoice TitleCapitalization
		{
			get => this.Picker.TitleCapitalization;
			set => this.UpdatePickerProperty(nameof(this.Picker.TitleCapitalization), value);
		}

		public bool OnlyChangeTitleCapitalizationWhenAllSame
		{
			get => this.Picker.OnlyChangeTitleCapitalizationWhenAllSame;
			set => this.UpdatePickerProperty(nameof(this.Picker.OnlyChangeTitleCapitalizationWhenAllSame), value);
		}

		private List<WordBreakCharacterChoice> wordBreakCharacterChoices;
		public List<WordBreakCharacterChoice> WordBreakCharacterChoices
		{
			get => this.wordBreakCharacterChoices;
			set => this.RaiseAndSetIfChanged(ref this.wordBreakCharacterChoices, value);
		}

		public void HandleWordBreakCharacterUpdate()
		{
			List<string> wordBreakCharacters = this.WordBreakCharacterChoices.Where(choice => choice.IsSelected).Select(choice => choice.Character).ToList();
			this.UpdatePickerProperty(nameof(this.Picker.WordBreakCharacters), wordBreakCharacters, raisePropertyChanged: false);
		}

		private string sourceNameCleanupPreview;
		public string SourceNameCleanupPreview
		{
			get => this.sourceNameCleanupPreview;
			set => this.RaiseAndSetIfChanged(ref this.sourceNameCleanupPreview, value);
		}

		private void RefreshSourceNameCleanupPreview()
		{
			this.SourceNameCleanupPreview = this.outputPathService.CleanUpSourceName(this.Picker);
		}

		private void RefreshOutputPathAndSourceNameCleanupPreview()
		{
			this.RefreshSourceNameCleanupPreview();
			this.outputPathService.GenerateOutputFileName();
		}

		public string VideoFileExtensions
		{
			get => this.Picker.VideoFileExtensions;
			set => this.UpdatePickerProperty(nameof(this.Picker.VideoFileExtensions), value);
		}

		public bool IgnoreFilesBelowMbEnabled
		{
			get => this.Picker.IgnoreFilesBelowMbEnabled;
			set => this.UpdatePickerProperty(nameof(this.Picker.IgnoreFilesBelowMbEnabled), value);
		}

		public int IgnoreFilesBelowMb
		{
			get => this.Picker.IgnoreFilesBelowMb;
			set => this.UpdatePickerProperty(nameof(this.Picker.IgnoreFilesBelowMb), value);
		}

		public List<ComboChoice<PickerTimeRangeMode>> PickerTimeRangeChoices { get; } = new List<ComboChoice<PickerTimeRangeMode>>
		{
			new ComboChoice<PickerTimeRangeMode>(PickerTimeRangeMode.All, CommonRes.All),
			new ComboChoice<PickerTimeRangeMode>(PickerTimeRangeMode.Chapters, EnumsRes.PickerTimeRangeMode_Chapters),
			new ComboChoice<PickerTimeRangeMode>(PickerTimeRangeMode.Time, EnumsRes.PickerTimeRangeMode_Time),
		};

		public PickerTimeRangeMode PickerTimeRangeMode
		{
			get => this.Picker.PickerTimeRangeMode;
			set => this.UpdatePickerProperty(nameof(this.Picker.PickerTimeRangeMode), value);
		}

		public int? ChapterRangeStart
		{
			get => this.Picker.ChapterRangeStart;
			set => this.UpdatePickerProperty(nameof(this.Picker.ChapterRangeStart), value);
		}

		public int? ChapterRangeEnd
		{
			get => this.Picker.ChapterRangeEnd;
			set => this.UpdatePickerProperty(nameof(this.Picker.ChapterRangeEnd), value);
		}

		public bool TitleRangeSelectEnabled
		{
			get => this.Picker.TitleRangeSelectEnabled;
			set => this.UpdatePickerProperty(nameof(this.Picker.TitleRangeSelectEnabled), value);
		}

		public int TitleRangeSelectStartMinutes
		{
			get => this.Picker.TitleRangeSelectStartMinutes;
			set => this.UpdatePickerProperty(nameof(this.Picker.TitleRangeSelectStartMinutes), value);
		}

		public int TitleRangeSelectEndMinutes
		{
			get => this.Picker.TitleRangeSelectEndMinutes;
			set => this.UpdatePickerProperty(nameof(this.Picker.TitleRangeSelectEndMinutes), value);
		}

		public TimeSpan TimeRangeStart
		{
			get => this.Picker.TimeRangeStart;
			set => this.UpdatePickerProperty(nameof(this.Picker.TimeRangeStart), value);
		}

		public TimeSpan TimeRangeEnd
		{
			get => this.Picker.TimeRangeEnd;
			set => this.UpdatePickerProperty(nameof(this.Picker.TimeRangeEnd), value);
		}

		public List<ComboChoice<AudioSelectionMode>> AudioSelectionModeChoices { get; } = new List<ComboChoice<AudioSelectionMode>>
		{
			new ComboChoice<AudioSelectionMode>(AudioSelectionMode.Disabled, PickerRes.LastSelectedRadioButton),
			new ComboChoice<AudioSelectionMode>(AudioSelectionMode.First, CommonRes.First),
			new ComboChoice<AudioSelectionMode>(AudioSelectionMode.ByIndex, PickerRes.ByIndexRadioButton),
			new ComboChoice<AudioSelectionMode>(AudioSelectionMode.Language, PickerRes.LanguagesRadioButton),
			new ComboChoice<AudioSelectionMode>(AudioSelectionMode.All, CommonRes.All)
		};

		public AudioSelectionMode AudioSelectionMode
		{
			get => this.Picker.AudioSelectionMode;
			set => this.UpdatePickerProperty(nameof(this.Picker.AudioSelectionMode), value);
		}

		public string AudioIndices
		{
			get => this.Picker.AudioIndices;
			set => this.UpdatePickerProperty(nameof(this.Picker.AudioIndices), value);
		}

		private readonly SourceList<LanguageViewModel> audioLanguages = new SourceList<LanguageViewModel>();
		public ObservableCollectionExtended<LanguageViewModel> AudioLanguagesBindable { get; } = new ObservableCollectionExtended<LanguageViewModel>();

		private ObservableAsPropertyHelper<bool> hasMultipleAudioLanguages;
		public bool HasMultipleAudioLanguages => this.hasMultipleAudioLanguages.Value;

		private ObservableAsPropertyHelper<bool> hasNoAudioLanguages;
		public bool HasNoAudioLanguages => this.hasNoAudioLanguages.Value;

		public bool AudioLanguageAll
		{
			get => this.Picker.AudioLanguageAll;
			set => this.UpdatePickerProperty(nameof(this.Picker.AudioLanguageAll), value);
		}

		private ObservableAsPropertyHelper<string> audioFirstTrackLabel;
		public string AudioFirstTrackLabel => this.audioFirstTrackLabel.Value;

		private ObservableAsPropertyHelper<string> audioAllTracksLabel;
		public string AudioAllTracksLabel => this.audioAllTracksLabel.Value;

		public bool UseCustomAudioTrackNames
		{
			get => this.Picker.UseCustomAudioTrackNames;
			set => this.UpdatePickerProperty(nameof(this.Picker.UseCustomAudioTrackNames), value);
		}

		private readonly SourceList<TrackNameViewModel> audioTrackNames = new SourceList<TrackNameViewModel>();
		public ObservableCollectionExtended<TrackNameViewModel> AudioTrackNamesBindable { get; } = new ObservableCollectionExtended<TrackNameViewModel>();

		public List<ComboChoice<SubtitleSelectionMode>> SubtitleSelectionModeChoices { get; } = new List<ComboChoice<SubtitleSelectionMode>>
		{
			new ComboChoice<SubtitleSelectionMode>(SubtitleSelectionMode.Disabled, PickerRes.LastSelectedRadioButton),
			new ComboChoice<SubtitleSelectionMode>(SubtitleSelectionMode.None, CommonRes.None),
			new ComboChoice<SubtitleSelectionMode>(SubtitleSelectionMode.First, CommonRes.First),
			new ComboChoice<SubtitleSelectionMode>(SubtitleSelectionMode.ByIndex, PickerRes.ByIndexRadioButton),
			new ComboChoice<SubtitleSelectionMode>(SubtitleSelectionMode.Language, PickerRes.LanguagesRadioButton),
			new ComboChoice<SubtitleSelectionMode>(SubtitleSelectionMode.All, CommonRes.All)
		};

		public SubtitleSelectionMode SubtitleSelectionMode
		{
			get => this.Picker.SubtitleSelectionMode;
			set => this.UpdatePickerProperty(nameof(this.Picker.SubtitleSelectionMode), value);
		}

		public string SubtitleIndices
		{
			get => this.Picker.SubtitleIndices;
			set => this.UpdatePickerProperty(nameof(this.Picker.SubtitleIndices), value);
		}

		public int? SubtitleDefaultIndex
		{
			get => this.Picker.SubtitleDefaultIndex;
			set => this.UpdatePickerProperty(nameof(this.Picker.SubtitleDefaultIndex), value);
		}

		private readonly SourceList<LanguageViewModel> subtitleLanguages = new SourceList<LanguageViewModel>();
		public ObservableCollectionExtended<LanguageViewModel> SubtitleLanguagesBindable { get; } = new ObservableCollectionExtended<LanguageViewModel>();

		private ObservableAsPropertyHelper<bool> hasMultipleSubtitleLanguages;
		public bool HasMultipleSubtitleLanguages => this.hasMultipleSubtitleLanguages.Value;

		private ObservableAsPropertyHelper<bool> hasNoSubtitleLanguages;
		public bool HasNoSubtitleLanguages => this.hasNoSubtitleLanguages.Value;

		public bool SubtitleLanguageOnlyIfDifferent
		{
			get => this.Picker.SubtitleLanguageOnlyIfDifferent;
			set => this.UpdatePickerProperty(nameof(this.Picker.SubtitleLanguageOnlyIfDifferent), value);
		}

		public bool SubtitleLanguageAll
		{
			get => this.Picker.SubtitleLanguageAll;
			set => this.UpdatePickerProperty(nameof(this.Picker.SubtitleLanguageAll), value);
		}

		private ObservableAsPropertyHelper<string> subtitleFirstTrackLabel;
		public string SubtitleFirstTrackLabel => this.subtitleFirstTrackLabel.Value;

		private ObservableAsPropertyHelper<string> subtitleAllTracksLabel;
		public string SubtitleAllTracksLabel => this.subtitleAllTracksLabel.Value;

		private ObservableAsPropertyHelper<SubtitleQuantityClass> subtitleQuantityClass;
		public SubtitleQuantityClass SubtitleQuantityClass => this.subtitleQuantityClass.Value;

		private ObservableAsPropertyHelper<bool> showDefaultCheckBox;
		public bool ShowDefaultCheckBox => this.showDefaultCheckBox.Value;

		private ObservableAsPropertyHelper<bool> showMarkFirstAsDefaultCheckBox;
		public bool ShowMarkFirstAsDefaultCheckBox => this.showMarkFirstAsDefaultCheckBox.Value;

		public bool SubtitleDefault
		{
			get => this.Picker.SubtitleDefault;
			set => this.UpdatePickerProperty(nameof(this.Picker.SubtitleDefault), value);
		}

		public bool SubtitleForcedOnly
		{
			get => this.Picker.SubtitleForcedOnly;
			set => this.UpdatePickerProperty(nameof(this.Picker.SubtitleForcedOnly), value);
		}

		public bool SubtitleAddForeignAudioScan
		{
			get => this.Picker.SubtitleAddForeignAudioScan;
			set => this.UpdatePickerProperty(nameof(this.Picker.SubtitleAddForeignAudioScan), value);
		}

		private ObservableAsPropertyHelper<bool> burnInVisible;
		public bool BurnInVisible => this.burnInVisible.Value;

		private void RefreshBurnInChoices()
		{
			this.subtitleBurnInChoices = new List<ComboChoice<SubtitleBurnInSelection>>();

			this.subtitleBurnInChoices.Add(new ComboChoice<SubtitleBurnInSelection>(SubtitleBurnInSelection.None, EnumsRes.SubtitleBurnInSelection_None));

			if (this.SubtitleSelectionMode != SubtitleSelectionMode.None)
			{
				this.subtitleBurnInChoices.Add(new ComboChoice<SubtitleBurnInSelection>(SubtitleBurnInSelection.First, EnumsRes.SubtitleBurnInSelection_First));
			}

			if (this.SubtitleAddForeignAudioScan)
			{
				this.subtitleBurnInChoices.Add(new ComboChoice<SubtitleBurnInSelection>(SubtitleBurnInSelection.ForeignAudioTrack, EnumsRes.SubtitleBurnInSelection_ForeignAudioTrack));

				if (this.SubtitleSelectionMode != SubtitleSelectionMode.None)
				{
					this.subtitleBurnInChoices.Add(new ComboChoice<SubtitleBurnInSelection>(SubtitleBurnInSelection.ForeignAudioTrackElseFirst, EnumsRes.SubtitleBurnInSelection_ForeignAudioTrackElseFirst));
				}
			}

			this.RaisePropertyChanged(nameof(this.SubtitleBurnInChoices));
		}

		private List<ComboChoice<SubtitleBurnInSelection>> subtitleBurnInChoices = new List<ComboChoice<SubtitleBurnInSelection>>();
		public List<ComboChoice<SubtitleBurnInSelection>> SubtitleBurnInChoices => this.subtitleBurnInChoices;

		public SubtitleBurnInSelection SubtitleBurnInSelection
		{
			get => this.Picker.SubtitleBurnInSelection;
			set => this.UpdatePickerProperty(nameof(this.Picker.SubtitleBurnInSelection), value);
		}

		public bool UseCustomSubtitleTrackNames
		{
			get => this.Picker.UseCustomSubtitleTrackNames;
			set => this.UpdatePickerProperty(nameof(this.Picker.UseCustomSubtitleTrackNames), value);
		}

		private readonly SourceList<TrackNameViewModel> subtitleTrackNames = new SourceList<TrackNameViewModel>();
		public ObservableCollectionExtended<TrackNameViewModel> SubtitleTrackNamesBindable { get; } = new ObservableCollectionExtended<TrackNameViewModel>();

		public bool EnableExternalSubtitleImport
		{
			get => this.Picker.EnableExternalSubtitleImport;
			set => this.UpdatePickerProperty(nameof(this.Picker.EnableExternalSubtitleImport), value);
		}

		public string ExternalSubtitleImportCheckBoxTitle => string.Format(PickerRes.ExternalSubtitleImportCheckBox, string.Join(", ", FileUtilities.SubtitleExtensions));

		public string ExternalSubtitleImportLanguage
		{
			get => this.Picker.ExternalSubtitleImportLanguage;
			set => this.UpdatePickerProperty(nameof(this.Picker.ExternalSubtitleImportLanguage), value);
		}

		public bool ExternalSubtitleImportDefault
		{
			get => this.Picker.ExternalSubtitleImportDefault;
			set => this.UpdatePickerProperty(nameof(this.Picker.ExternalSubtitleImportDefault), value);
		}

		public bool ExternalSubtitleImportBurnIn
		{
			get => this.Picker.ExternalSubtitleImportBurnIn;
			set => this.UpdatePickerProperty(nameof(this.Picker.ExternalSubtitleImportBurnIn), value);
		}

		public bool PassThroughMetadata
		{
			get => this.Picker.PassThroughMetadata;
			set => this.UpdatePickerProperty(nameof(this.Picker.PassThroughMetadata), value);
		}

		public bool UseEncodingPreset
		{
			get => this.Picker.UseEncodingPreset;
			set => this.UpdatePickerProperty(nameof(this.Picker.UseEncodingPreset), value);
		}

		private PresetViewModel selectedPreset;

		public PresetViewModel SelectedPreset
		{
			get => this.selectedPreset;
			set => this.RaiseAndSetIfChanged(ref this.selectedPreset, value);
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
			get => this.Picker.AutoQueueOnScan;
			set => this.UpdatePickerProperty(nameof(this.Picker.AutoQueueOnScan), value);
		}

		public bool AutoEncodeOnScan
		{
			get => this.Picker.AutoEncodeOnScan;
			set => this.UpdatePickerProperty(nameof(this.Picker.AutoEncodeOnScan), value);
		}

		public bool PostEncodeActionEnabled
		{
			get => this.Picker.PostEncodeActionEnabled;
			set => this.UpdatePickerProperty(nameof(this.Picker.PostEncodeActionEnabled), value);
		}

		public string PostEncodeExecutable
		{
			get => this.Picker.PostEncodeExecutable;
			set => this.UpdatePickerProperty(nameof(this.Picker.PostEncodeExecutable), value);
		}

		public string PostEncodeArguments
		{
			get => this.Picker.PostEncodeArguments;
			set => this.UpdatePickerProperty(nameof(this.Picker.PostEncodeArguments), value);
		}

		public List<ComboChoice<SourceFileRemoval>> SourceFileRemovalChoices { get; } = new List<ComboChoice<SourceFileRemoval>>
		{
			new ComboChoice<SourceFileRemoval>(SourceFileRemoval.Disabled, CommonRes.Disabled),
			new ComboChoice<SourceFileRemoval>(SourceFileRemoval.Recycle, EnumsRes.SourceFileRemoval_Recycle),
			new ComboChoice<SourceFileRemoval>(SourceFileRemoval.Delete, EnumsRes.SourceFileRemoval_Delete),
		};

		public SourceFileRemoval SourceFileRemoval
		{
			get => this.Picker.SourceFileRemoval;
			set => this.UpdatePickerProperty(nameof(this.Picker.SourceFileRemoval), value);
		}

		public List<ComboChoice<SourceFileRemovalTiming>> SourceFileRemovalTimingChoices { get; } = new List<ComboChoice<SourceFileRemovalTiming>>
		{
			new ComboChoice<SourceFileRemovalTiming>(SourceFileRemovalTiming.AfterClearingCompletedItems, EnumsRes.SourceFileRemovalTiming_AfterClearingCompletedItems),
			new ComboChoice<SourceFileRemovalTiming>(SourceFileRemovalTiming.Immediately, EnumsRes.SourceFileRemovalTiming_Immediately),
		};

		public SourceFileRemovalTiming SourceFileRemovalTiming
		{
			get => this.Picker.SourceFileRemovalTiming;
			set => this.UpdatePickerProperty(nameof(this.Picker.SourceFileRemovalTiming), value);
		}

		public bool SourceFileRemovalConfirmation
		{
			get => this.Picker.SourceFileRemovalConfirmation;
			set => this.UpdatePickerProperty(nameof(this.Picker.SourceFileRemovalConfirmation), value);
		}

		private ObservableAsPropertyHelper<bool> sourceFileRemovalConfirmationVisible;
		public bool SourceFileRemovalConfirmationVisible => this.sourceFileRemovalConfirmationVisible.Value;

		private ReactiveCommand<Unit, Unit> save;
		public ICommand Save
		{
			get
			{
				return this.save ?? (this.save = ReactiveCommand.Create(
					() =>
					{
						this.PickersService.SavePicker();
					},
					this.PickersService.WhenAnyValue(x => x.SelectedPicker.Picker.IsDefault).Select(isDefault => !isDefault)));
			}
		}


		private ReactiveCommand<Unit, Unit> saveAs;
		public ICommand SaveAs
		{
			get
			{
				return this.saveAs ?? (this.saveAs = ReactiveCommand.Create(() =>
				{
					var dialogVM = new ChooseNameViewModel(MiscRes.ChooseNamePicker, this.PickersService.Pickers.Skip(1).Select(p => p.Picker.Name));
					dialogVM.Name = this.Picker.DisplayName;
					StaticResolver.Resolve<IWindowManager>().OpenDialog(dialogVM, this);

					if (dialogVM.DialogResult)
					{
						string newPickerName = dialogVM.Name;

						this.PickersService.SavePickerAs(newPickerName);
					}
				}));
			}
		}

		private ReactiveCommand<Unit, Unit> rename;
		public ICommand Rename
		{
			get
			{
				return this.rename ?? (this.rename = ReactiveCommand.Create(() =>
				{
					var dialogVM = new ChooseNameViewModel(MiscRes.ChooseNamePicker, this.PickersService.Pickers.Skip(1).Select(p => p.Picker.Name));
					dialogVM.Name = this.Picker.DisplayName;
					StaticResolver.Resolve<IWindowManager>().OpenDialog(dialogVM, this);

					if (dialogVM.DialogResult)
					{
						string newPickerName = dialogVM.Name;
						this.PickersService.SelectedPicker.Picker.Name = newPickerName;

						this.PickersService.SavePicker();
					}
				}));
			}
		}

		private ReactiveCommand<Unit, Unit> delete;
		public ICommand Delete
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
								this.PickersService.RevertPicker();
							}
						}
						else
						{
							// Delete
							MessageBoxResult dialogResult = Utilities.MessageBox.Show(this, MainRes.RemoveConfirmMessage, MainRes.RemoveConfirmTitle, MessageBoxButton.YesNo);
							if (dialogResult == MessageBoxResult.Yes)
							{
								this.PickersService.DeletePicker();
							}
						}
					},
					this.PickersService.WhenAnyValue(
						x => x.SelectedPicker.Picker.IsModified,
						x => x.SelectedPicker.Picker.IsDefault,
						(isModified, isDefault) => isModified || !isDefault)));
			}
		}

		private ReactiveCommand<Unit, Unit> pickOutputDirectory;
		public ICommand PickOutputDirectory
		{
			get
			{
				return this.pickOutputDirectory ?? (this.pickOutputDirectory = ReactiveCommand.Create(() =>
				{
					string overrideFolder = FileService.Instance.GetFolderName(this.OutputDirectory, MainRes.OutputDirectoryPickerText);
					if (overrideFolder != null)
					{
						this.OutputDirectory = overrideFolder;
					}
				}));
			}
		}

		private ReactiveCommand<Unit, Unit> addAudioLanguage;
		public ICommand AddAudioLanguage
		{
			get
			{
				return this.addAudioLanguage ?? (this.addAudioLanguage = ReactiveCommand.Create(() =>
				{
					this.AddLanguage(this.audioLanguages);
					this.HandleAudioLanguageUpdate();
				}));
			}
		}

		private ReactiveCommand<Unit, Unit> addSubtitleLanguage;
		public ICommand AddSubtitleLanguage
		{
			get
			{
				return this.addSubtitleLanguage ?? (this.addSubtitleLanguage = ReactiveCommand.Create(() =>
				{
					this.AddLanguage(this.subtitleLanguages);
					this.HandleSubtitleLanguageUpdate();
				}));
			}
		}

		private void AddLanguage(SourceList<LanguageViewModel> list)
		{
			list.Add(new LanguageViewModel(this) { Code = LanguageUtilities.GetDefaultLanguageCode(list.Items.Select(l => l.Code).ToList()) });
		}

		private ReactiveCommand<Unit, Unit> addAudioTrackName;
		public ICommand AddAudioTrackName
		{
			get
			{
				return this.addAudioTrackName ?? (this.addAudioTrackName = ReactiveCommand.Create(() =>
				{
					int trackNumber = this.audioTrackNames.Count + 1;
					var trackNameViewModel = new TrackNameViewModel(this, trackNumber, string.Empty);
					this.audioTrackNames.Add(trackNameViewModel);
					this.View.FocusAudioTrackName(trackNumber - 1);
					this.HandleAudioTrackNameUpdate();
				}));
			}
		}

		private ReactiveCommand<Unit, Unit> addSubtitleTrackName;
		public ICommand AddSubtitleTrackName
		{
			get
			{
				return this.addSubtitleTrackName ?? (this.addSubtitleTrackName = ReactiveCommand.Create(() =>
				{
					int trackNumber = this.subtitleTrackNames.Count + 1;
					var trackNameViewModel = new TrackNameViewModel(this, trackNumber, string.Empty);
					this.subtitleTrackNames.Add(trackNameViewModel);
					this.View.FocusSubtitleTrackName(trackNumber - 1);
					this.HandleSubtitleTrackNameUpdate();
				}));
			}
		}

		private ReactiveCommand<Unit, Unit> pickPostEncodeExecutable;
		public ICommand PickPostEncodeExecutable
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

		private void HandleAudioLanguageUpdate()
		{
			List<string> languages = GetLanguageList(this.audioLanguages.Items);
			this.UpdatePickerProperty(nameof(this.Picker.AudioLanguageCodes), languages, raisePropertyChanged: false);
		}

		private void HandleAudioTrackNameUpdate()
		{
			List<string> trackNames = GetTrackNameList(this.audioTrackNames.Items);
			this.UpdatePickerProperty(nameof(this.Picker.AudioTrackNames), trackNames, raisePropertyChanged: false);
		}

		private void HandleSubtitleLanguageUpdate()
		{
			List<string> languages = GetLanguageList(this.subtitleLanguages.Items);
			this.UpdatePickerProperty(nameof(this.Picker.SubtitleLanguageCodes), languages, raisePropertyChanged: false);
		}

		private void HandleSubtitleTrackNameUpdate()
		{
			List<string> trackNames = GetTrackNameList(this.subtitleTrackNames.Items);
			this.UpdatePickerProperty(nameof(this.Picker.SubtitleTrackNames), trackNames, raisePropertyChanged: false);
		}

		private static List<string> GetLanguageList(IEnumerable<LanguageViewModel> languageViewModels)
		{
			return languageViewModels.Select(l => l.Code).ToList();
		}

		private static List<string> GetTrackNameList(IEnumerable<TrackNameViewModel> trackNames)
		{
			return trackNames.Select(n => n.Name).ToList();
		}

		public void RemoveAudioLanguage(LanguageViewModel viewModel)
		{
			this.audioLanguages.Remove(viewModel);
			this.HandleAudioLanguageUpdate();
		}

		public void RemoveSubtitleLanguage(LanguageViewModel viewModel)
		{
			this.subtitleLanguages.Remove(viewModel);
			this.HandleSubtitleLanguageUpdate();
		}

		/// <summary>
		/// Remove the given track name entry.
		/// </summary>
		/// <param name="viewModel">The viewmodel to remove</param>
		/// <remarks>Used for both audio tracks and subtitles.</remarks>
		public void RemoveTrackName(TrackNameViewModel viewModel)
		{
			if (this.audioTrackNames.Remove(viewModel))
			{
				UpdateTrackNumbering(this.audioTrackNames.Items);
				this.HandleAudioTrackNameUpdate();
			}
			else
			{
				this.subtitleTrackNames.Remove(viewModel);
				UpdateTrackNumbering(this.subtitleTrackNames.Items);
				this.HandleSubtitleTrackNameUpdate();
			}
		}

		public void HandleTrackNameUpdate(TrackNameViewModel viewModel)
		{
			if (this.audioTrackNames.Items.Contains(viewModel))
			{
				this.HandleAudioTrackNameUpdate();
			}
			else
			{
				this.HandleSubtitleTrackNameUpdate();
			}
		}

		private static void UpdateTrackNumbering(IEnumerable<TrackNameViewModel> tracks)
		{
			int trackNumber = 1;
			foreach (TrackNameViewModel track in tracks)
			{
				track.TrackNumber = trackNumber++;
			}
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
				this.modifyingPicker = true;

				bool createPicker = this.Picker.IsDefault;
				if (createPicker)
				{
					this.PickersService.AutoCreatePicker();
				}
				else if (!this.Picker.IsModified)
				{
					// Clone the picker so we modify a different copy.
					Picker newPicker = new Picker();
					newPicker.InjectFrom<CloneInjection>(this.Picker);

					if (!newPicker.IsModified)
					{
						this.PickersService.ModifyPicker(newPicker);
					}
				}

				this.modifyingPicker = false;
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

				this.PickersService.SavePickersToStorage();
			}
		}
	}

	public enum SubtitleQuantityClass
	{
		None,
		Single,
		Multiple
	}
}
