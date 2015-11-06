using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reactive.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using FastMember;
using HandBrake.ApplicationServices.Interop.Model;
using Omu.ValueInjecter;
using ReactiveUI;
using VidCoder.Model;
using VidCoder.Resources;
using VidCoder.Services;
using VidCoder.Services.Windows;
using VidCoderCommon.Utilities.Injection;

namespace VidCoder.ViewModel
{
	public class PickerWindowViewModel : ReactiveObject
	{
		private static TypeAccessor typeAccessor = TypeAccessor.Create(typeof(Picker));

		private PickersService pickersService = Ioc.Get<PickersService>();
		private PresetsService presetsService = Ioc.Get<PresetsService>();
		private OutputPathService outputPathService = Ioc.Get<OutputPathService>();

		private Dictionary<string, Action> pickerProperties;
		private bool automaticChange;

		public PickerWindowViewModel()
		{
			this.automaticChange = true;

			this.RegisterPickerProperties();

			this.pickersService.WhenAnyValue(x => x.SelectedPicker.Picker)
				.Subscribe(x =>
				{
					bool automaticChangePreviousValue = this.automaticChange;
					this.automaticChange = true;
					this.RaiseAllChanged();
					this.automaticChange = automaticChangePreviousValue;
				});

			this.pickersService.WhenAnyValue(x => x.SelectedPicker.Picker.IsNone, x => x.SelectedPicker.Picker.IsModified, (isNone, isModified) => !isNone && !isModified)
				.ToProperty(this, x => x.DeleteButtonVisible, out this.deleteButtonVisible);

			this.pickersService.WhenAnyValue(
				x => x.SelectedPicker.Picker.DisplayName,
				x => x.SelectedPicker.Picker.IsModified, 
				(displayName, isModified) =>
					{
						string windowTitle2 = string.Format(PickerRes.WindowTitle, displayName);
						if (isModified)
						{
							windowTitle2 += " *";
						}

						return windowTitle2;
					})
				.ToProperty(this, x => x.WindowTitle, out this.windowTitle);

			// Whenever the output directory override is disabled, used the config.
			this.WhenAnyValue(x => x.OutputDirectoryOverrideEnabled)
				.Subscribe(directoryOverrideEnabled =>
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
			this.WhenAnyValue(x => x.NameFormatOverrideEnabled)
				.Subscribe(nameFormatOverrideEnabled =>
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
			this.WhenAnyValue(x => x.UseEncodingPreset)
				.Subscribe(useEncodingPreset =>
				{
					if (useEncodingPreset)
					{
						PresetViewModel preset = this.presetsService.AllPresets.FirstOrDefault(p => p.Preset.Name == this.Picker.EncodingPreset);
						if (preset == null)
						{
							preset = this.presetsService.AllPresets.First();
						}

						this.SelectedPreset = preset;
					}
					else
					{
						this.SelectedPreset = null;
					}
				});

			// Update the underlying picker when our local properties change.
			// Don't need to raise another changed event as our local property setter already raises it.
			this.WhenAnyValue(x => x.OutputDirectoryOverride)
				.Skip(1)
				.Subscribe(directoryOverride =>
				{
					this.UpdatePickerProperty(nameof(this.Picker.OutputDirectoryOverride), directoryOverride, raisePropertyChanged: false);
				});

			this.WhenAnyValue(x => x.NameFormatOverride)
				.Skip(1)
				.Subscribe(nameFormatOverride =>
				{
					this.UpdatePickerProperty(nameof(this.Picker.NameFormatOverride), nameFormatOverride, raisePropertyChanged: false);
				});

			this.WhenAnyValue(x => x.SelectedPreset)
				.Subscribe(selectedPreset =>
				{
					string presetName = selectedPreset == null ? null : selectedPreset.Preset.Name;
					this.UpdatePickerProperty(nameof(this.Picker.EncodingPreset), presetName, raisePropertyChanged: false);
				});

			this.DismissMessage = ReactiveCommand.Create();
			this.DismissMessage.Subscribe(_ =>
			{
				this.ShowHelpMessage = false;
			});

			this.Save = ReactiveCommand.Create(this.pickersService.WhenAnyValue(x => x.SelectedPicker.Picker.IsNone).Select(isNone => !isNone));
			this.Save.Subscribe(_ =>
			{
				this.pickersService.SavePicker();
			});

			this.SaveAs = ReactiveCommand.Create();
			this.SaveAs.Subscribe(_ => this.SaveAsImpl());

			this.Rename = ReactiveCommand.Create();
			this.Rename.Subscribe(_ => this.RenameImpl());

			this.Delete = ReactiveCommand.Create(this.pickersService.WhenAnyValue(x => x.SelectedPicker.Picker.IsModified, x => x.SelectedPicker.Picker.IsNone, (isModified, isNone) => isModified || !isNone));
			this.Delete.Subscribe(_ => this.DeleteImpl());

			this.PickOutputDirectory = ReactiveCommand.Create();
			this.PickOutputDirectory.Subscribe(_ => this.PickOutputDirectoryImpl());

			this.automaticChange = false;
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
			this.RegisterPickerProperty(nameof(this.Picker.OutputDirectoryOverride), () =>
			{
				this.outputPathService.GenerateOutputFileName();
			});
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
			this.RegisterPickerProperty(nameof(this.Picker.AudioSelectionMode));
			this.RegisterPickerProperty(nameof(this.Picker.AudioLanguageCode));
			this.RegisterPickerProperty(nameof(this.Picker.AudioLanguageAll));
			this.RegisterPickerProperty(nameof(this.Picker.SubtitleSelectionMode));
			this.RegisterPickerProperty(nameof(this.Picker.SubtitleForeignBurnIn));
			this.RegisterPickerProperty(nameof(this.Picker.SubtitleLanguageCode));
			this.RegisterPickerProperty(nameof(this.Picker.SubtitleLanguageOnlyIfDifferent));
			this.RegisterPickerProperty(nameof(this.Picker.SubtitleLanguageAll), () =>
			{
				this.SubtitleForeignBurnIn = false;
				this.SubtitleLanguageDefault = false;
			});
			this.RegisterPickerProperty(nameof(this.Picker.SubtitleLanguageBurnIn), () =>
			{
				this.SubtitleLanguageAll = false;
				this.SubtitleLanguageBurnIn = false;
			});
			this.RegisterPickerProperty(nameof(this.Picker.SubtitleLanguageDefault), () =>
			{
				this.SubtitleLanguageAll = false;
				this.SubtitleLanguageBurnIn = false;
			});
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
			get
			{
				return Config.ShowPickerWindowMessage;
			}

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

		public string AudioLanguageCode
		{
			get { return this.Picker.AudioLanguageCode; }
			set { this.UpdatePickerProperty(nameof(this.Picker.AudioLanguageCode), value); }
		}

		public bool AudioLanguageAll
		{
			get { return this.Picker.AudioLanguageAll; }
			set { this.UpdatePickerProperty(nameof(this.Picker.AudioLanguageAll), value); }
		}

		public SubtitleSelectionMode SubtitleSelectionMode
		{
			get { return this.Picker.SubtitleSelectionMode; }
			set { this.UpdatePickerProperty(nameof(this.Picker.SubtitleSelectionMode), value); }
		}

		public bool SubtitleForeignBurnIn
		{
			get { return this.Picker.SubtitleForeignBurnIn; }
			set { this.UpdatePickerProperty(nameof(this.Picker.SubtitleForeignBurnIn), value); }
		}

		public string SubtitleLanguageCode
		{
			get { return this.Picker.SubtitleLanguageCode; }
			set { this.UpdatePickerProperty(nameof(this.Picker.SubtitleLanguageCode), value); }
		}

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

		public bool SubtitleLanguageDefault
		{
			get { return this.Picker.SubtitleLanguageDefault; }
			set { this.UpdatePickerProperty(nameof(this.Picker.SubtitleLanguageDefault), value); }
		}

		public bool SubtitleLanguageBurnIn
		{
			get { return this.Picker.SubtitleLanguageBurnIn; }
			set { this.UpdatePickerProperty(nameof(this.Picker.SubtitleLanguageBurnIn), value); }
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

		public IList<Language> Languages
		{
			get
			{
				return HandBrake.ApplicationServices.Interop.Languages.AllLanguages;
			}
		}

		public ReactiveCommand<object> DismissMessage { get; private set; }

		public ReactiveCommand<object> Save { get; private set; }

		public ReactiveCommand<object> SaveAs { get; private set; }

		private void SaveAsImpl()
		{
			var dialogVM = new ChooseNameViewModel(MainRes.PickerWord, this.pickersService.Pickers.Skip(1).Select(p => p.Picker.Name));
			dialogVM.Name = this.Picker.DisplayName;
			Ioc.Get<IWindowManager>().OpenDialog(dialogVM, this);

			if (dialogVM.DialogResult)
			{
				string newPickerName = dialogVM.Name;

				this.pickersService.SavePickerAs(newPickerName);
			}
		}

		public ReactiveCommand<object> Rename { get; private set; }

		private void RenameImpl()
		{
			var dialogVM = new ChooseNameViewModel(MainRes.PickerWord, this.pickersService.Pickers.Skip(1).Select(p => p.Picker.Name));
			dialogVM.Name = this.Picker.DisplayName;
			Ioc.Get<IWindowManager>().OpenDialog(dialogVM, this);

			if (dialogVM.DialogResult)
			{
				string newPickerName = dialogVM.Name;
				this.pickersService.SelectedPicker.Picker.Name = newPickerName;

				this.pickersService.SavePicker();
			}
		}

		public ReactiveCommand<object> Delete { get; private set; }

		private void DeleteImpl()
		{
			if (this.Picker.IsModified)
			{
				// Revert
				MessageBoxResult dialogResult = Utilities.MessageBox.Show(
					this,
					string.Format(MainRes.RevertConfirmMessage, MainRes.PickerWord),
					string.Format(MainRes.RevertConfirmTitle, MainRes.PickerWord),
					MessageBoxButton.YesNo);
				if (dialogResult == MessageBoxResult.Yes)
				{
					this.pickersService.RevertPicker();
				}
			}
			else
			{
				// Delete
				MessageBoxResult dialogResult = Utilities.MessageBox.Show(
					this,
					string.Format(MainRes.RemoveConfirmMessage, MainRes.PickerWord),
					string.Format(MainRes.RemoveConfirmTitle, MainRes.PickerWord),
					MessageBoxButton.YesNo);
				if (dialogResult == MessageBoxResult.Yes)
				{
					this.pickersService.DeletePicker();
				}
			}
		}

		public ReactiveCommand<object> PickOutputDirectory { get; private set; }

		private void PickOutputDirectoryImpl()
		{
			string overrideFolder = FileService.Instance.GetFolderName(this.OutputDirectoryOverride, MainRes.OutputDirectoryPickerText);
			if (overrideFolder != null)
			{
				this.OutputDirectoryOverride = overrideFolder;
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

			if (!this.automaticChange)
			{
				bool createPicker = this.Picker.IsNone;
				if (createPicker)
				{
					this.pickersService.AutoCreatePicker();
				}
				else if (!this.Picker.IsModified)
				{
					// Clone the picker so we modify a different copy.
					Picker newPicker = new Picker();
					newPicker.InjectFrom<FastDeepCloneInjection>(this.Picker);

					if (!newPicker.IsModified)
					{
						this.pickersService.ModifyPicker(newPicker);
					}
				}
			}

			// Update the value and raise PropertyChanged
			typeAccessor[this.Picker, propertyName] = value;

			if (raisePropertyChanged)
			{
				this.RaisePropertyChanged(propertyName);
			}

			if (!this.automaticChange)
			{
				// If we have an action registered to update dependent properties, do it
				Action action = this.pickerProperties[propertyName];
				if (action != null)
				{
					// Protect against update loops with a flag
					this.automaticChange = true;
					action();
					this.automaticChange = false;
				}

				this.pickersService.SavePickersToStorage();
			}
		}
	}
}
