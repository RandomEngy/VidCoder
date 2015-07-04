using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reactive.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using FastMember;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;
using HandBrake.ApplicationServices.Interop.Model;
using Omu.ValueInjecter;
using ReactiveUI;
using VidCoder.Messages;
using VidCoder.Model;
using VidCoder.Resources;
using VidCoder.Services;
using VidCoder.Services.Windows;

namespace VidCoder.ViewModel
{
	public class PickerWindowViewModel : ReactiveObject
	{
		private static TypeAccessor typeAccessor = TypeAccessor.Create(typeof(Picker));

		private PickersService pickersService = Ioc.Get<PickersService>();
		private PresetsService presetsService = Ioc.Get<PresetsService>();

		private Dictionary<string, Action> pickerProperties;
		private bool automaticChange;

		public PickerWindowViewModel(Picker picker)
		{
			this.automaticChange = true;

			this.RegisterPickerProperties();

			this.picker = picker;

			this.WhenAnyValue(x => x.Picker)
				.Subscribe(x =>
				{
					bool automaticChangePreviousValue = this.automaticChange;
					this.automaticChange = true;
					this.RaiseAllChanged();
					this.automaticChange = automaticChangePreviousValue;
				});

			this.WhenAnyValue(x => x.Picker.IsNone, x => x.Picker.IsModified, (isNone, isModified) => !isNone && !isModified)
				.ToProperty(this, x => x.DeleteButtonVisible, out this.deleteButtonVisible);

			this.WhenAnyValue(
				x => x.Picker,
				x => x.Picker.IsModified, 
				(pickerParam, isModified) =>
					{
						string windowTitle2 = string.Format(PickerRes.WindowTitle, pickerParam.DisplayName);
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
				});

			// Whenever the name format override is disabled, used the config.
			this.WhenAnyValue(x => x.NameFormatOverrideEnabled)
				.Subscribe(nameFormatOverrideEnabled =>
				{
					if (!nameFormatOverrideEnabled)
					{
						this.NameFormatOverride = AutoNameCustomFormat;
					}
				});

			// Whenever UseEncodingPreset is false, set the selected VM to null.
			this.WhenAnyValue(x => x.UseEncodingPreset)
				.Subscribe(useEncodingPreset =>
				{
					if (useEncodingPreset)
					{
						PresetViewModel preset = this.presetsService.AllPresets.FirstOrDefault(p => p.PresetName == this.picker.EncodingPreset);
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
				.Subscribe(directoryOverride =>
				{
					this.UpdatePickerProperty(() => this.picker.OutputDirectoryOverride, directoryOverride, raisePropertyChanged: false);
				});

			this.WhenAnyValue(x => x.NameFormatOverride)
				.Subscribe(nameFormatOverride =>
				{
					this.UpdatePickerProperty(() => this.picker.NameFormatOverride, nameFormatOverride, raisePropertyChanged: false);
				});

			this.WhenAnyValue(x => x.SelectedPreset)
				.Subscribe(selectedPreset =>
				{
					string presetName = selectedPreset == null ? null : selectedPreset.PresetName;
					this.UpdatePickerProperty(() => this.picker.EncodingPreset, presetName, raisePropertyChanged: false);
				});

			this.DismissMessage = ReactiveCommand.Create();
			this.DismissMessage.Subscribe(_ =>
			{
				this.ShowHelpMessage = false;
			});

			this.Save = ReactiveCommand.Create(this.WhenAnyValue(x => x.picker.IsNone).Select(isNone => !isNone));
			this.Save.Subscribe(_ =>
			{
				this.pickersService.SavePicker();
			});

			this.SaveAs = ReactiveCommand.Create();
			this.SaveAs.Subscribe(_ => this.SaveAsImpl());

			this.Rename = ReactiveCommand.Create();
			this.Rename.Subscribe(_ => this.RenameImpl());

			this.Delete = ReactiveCommand.Create(this.WhenAnyValue(x => x.picker.IsModified, x => x.picker.IsNone, (isModified, isNone) => isModified || !isNone));
			this.Delete.Subscribe(_ => this.DeleteImpl());

			this.PickOutputDirectory = ReactiveCommand.Create();
			this.PickOutputDirectory.Subscribe(_ => this.PickOutputDirectoryImpl());

			this.automaticChange = false;
		}

		private void RegisterPickerProperties()
		{
			this.pickerProperties = new Dictionary<string, Action>();

			// These actions fire when the user changes a property.

			this.RegisterPickerProperty(() => this.picker.OutputDirectoryOverrideEnabled, () =>
			{
				// When enabled, default to the global output folder.
				if (this.OutputDirectoryOverrideEnabled)
				{
					this.OutputDirectoryOverride = Config.AutoNameOutputFolder;
				}

				Messenger.Default.Send(new OutputFolderChangedMessage());
			});
			this.RegisterPickerProperty(() => this.picker.OutputDirectoryOverride, () =>
			{
				Messenger.Default.Send(new OutputFolderChangedMessage());
			});
			this.RegisterPickerProperty(() => this.picker.NameFormatOverrideEnabled, () =>
			{
				// When enabled, default global name format.
				if (this.NameFormatOverrideEnabled)
				{
					this.NameFormatOverride = AutoNameCustomFormat;
				}
			});
			this.RegisterPickerProperty(() => this.picker.NameFormatOverride);
			this.RegisterPickerProperty(() => this.picker.OutputToSourceDirectory);
			this.RegisterPickerProperty(() => this.picker.PreserveFolderStructureInBatch);
			this.RegisterPickerProperty(() => this.picker.TitleRangeSelectEnabled, () =>
			{
				this.SendTitleRangeChangeMessage();
			});
			this.RegisterPickerProperty(() => this.picker.TitleRangeSelectStartMinutes, () =>
			{
				if (this.TitleRangeSelectEnabled)
				{
					this.SendTitleRangeChangeMessage();
				}
			});
			this.RegisterPickerProperty(() => this.picker.TitleRangeSelectEndMinutes, () =>
			{
				if (this.TitleRangeSelectEnabled)
				{
					this.SendTitleRangeChangeMessage();
				}
			});
			this.RegisterPickerProperty(() => this.picker.AudioSelectionMode);
			this.RegisterPickerProperty(() => this.picker.AudioLanguageCode);
			this.RegisterPickerProperty(() => this.picker.AudioLanguageAll);
			this.RegisterPickerProperty(() => this.picker.SubtitleSelectionMode);
			this.RegisterPickerProperty(() => this.picker.SubtitleForeignBurnIn);
			this.RegisterPickerProperty(() => this.picker.SubtitleLanguageCode);
			this.RegisterPickerProperty(() => this.picker.SubtitleLanguageOnlyIfDifferent);
			this.RegisterPickerProperty(() => this.picker.SubtitleLanguageAll, () =>
			{
				this.SubtitleForeignBurnIn = false;
				this.SubtitleLanguageDefault = false;
			});
			this.RegisterPickerProperty(() => this.picker.SubtitleLanguageBurnIn, () =>
			{
				this.SubtitleLanguageAll = false;
				this.SubtitleLanguageBurnIn = false;
			});
			this.RegisterPickerProperty(() => this.picker.SubtitleLanguageDefault, () =>
			{
				this.SubtitleLanguageAll = false;
				this.SubtitleLanguageBurnIn = false;
			});
			this.RegisterPickerProperty(() => this.picker.UseEncodingPreset);
			this.RegisterPickerProperty(() => this.picker.EncodingPreset);
			this.RegisterPickerProperty(() => this.picker.AutoQueueOnScan, () =>
			{
				if (!this.AutoQueueOnScan)
				{
					this.AutoEncodeOnScan = false;
				}
			});
			this.RegisterPickerProperty(() => this.picker.AutoEncodeOnScan);
		}

		private void RegisterPickerProperty<T>(Expression<Func<T>> propertyExpression, Action action = null)
		{
			string propertyName = MvvmUtilities.GetPropertyName(propertyExpression);
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

		private Picker picker;
		public Picker Picker
		{
			get { return this.picker; }
			set { this.RaiseAndSetIfChanged(ref this.picker, value); }
		}

		private ObservableAsPropertyHelper<string> windowTitle; 
		public string WindowTitle
		{
			get { return windowTitle.Value; }
		}

		//private ObservableAsPropertyHelper<bool> isNone; 
		//public bool IsNone
		//{
		//	get { return this.isNone.Value; }
		//}

		private ObservableAsPropertyHelper<bool> deleteButtonVisible; 
		public bool DeleteButtonVisible
		{
			get { return this.deleteButtonVisible.Value; }
		}

		//public bool IsModified
		//{
		//	get
		//	{
		//		return this.picker.IsModified;
		//	}

		//	set
		//	{
		//		this.picker.IsModified = value;
		//		this.RaisePropertyChanged();
		//	}
		//}

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
			get { return this.picker.OutputDirectoryOverrideEnabled; }
			set { this.UpdatePickerProperty(() => this.picker.OutputDirectoryOverrideEnabled, value); }
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
			get { return this.picker.NameFormatOverrideEnabled; }
			set { this.UpdatePickerProperty(() => this.picker.NameFormatOverrideEnabled, value); }
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
			get { return this.picker.OutputToSourceDirectory; }
			set { this.UpdatePickerProperty(() => this.picker.OutputToSourceDirectory, value); }
		}

		public bool? PreserveFolderStructureInBatch
		{
			get { return this.picker.PreserveFolderStructureInBatch; }
			set { this.UpdatePickerProperty(() => this.picker.PreserveFolderStructureInBatch, value); }
		}

		public bool TitleRangeSelectEnabled
		{
			get { return this.picker.TitleRangeSelectEnabled; }
			set { this.UpdatePickerProperty(() => this.picker.TitleRangeSelectEnabled, value); }
		}

		public int TitleRangeSelectStartMinutes
		{
			get { return this.picker.TitleRangeSelectStartMinutes; }
			set { this.UpdatePickerProperty(() => this.picker.TitleRangeSelectStartMinutes, value); }
		}

		public int TitleRangeSelectEndMinutes
		{
			get { return this.picker.TitleRangeSelectEndMinutes; }
			set { this.UpdatePickerProperty(() => this.picker.TitleRangeSelectEndMinutes, value); }
		}

		public AudioSelectionMode AudioSelectionMode
		{
			get { return this.picker.AudioSelectionMode; }
			set { this.UpdatePickerProperty(() => this.picker.AudioSelectionMode, value); }
		}

		public string AudioLanguageCode
		{
			get { return this.picker.AudioLanguageCode; }
			set { this.UpdatePickerProperty(() => this.picker.AudioLanguageCode, value); }
		}

		public bool AudioLanguageAll
		{
			get { return this.picker.AudioLanguageAll; }
			set { this.UpdatePickerProperty(() => this.picker.AudioLanguageAll, value); }
		}

		public SubtitleSelectionMode SubtitleSelectionMode
		{
			get { return this.picker.SubtitleSelectionMode; }
			set { this.UpdatePickerProperty(() => this.picker.SubtitleSelectionMode, value); }
		}

		public bool SubtitleForeignBurnIn
		{
			get { return this.picker.SubtitleForeignBurnIn; }
			set { this.UpdatePickerProperty(() => this.picker.SubtitleForeignBurnIn, value); }
		}

		public string SubtitleLanguageCode
		{
			get { return this.picker.SubtitleLanguageCode; }
			set { this.UpdatePickerProperty(() => this.picker.SubtitleLanguageCode, value); }
		}

		public bool SubtitleLanguageOnlyIfDifferent
		{
			get { return this.picker.SubtitleLanguageOnlyIfDifferent; }
			set { this.UpdatePickerProperty(() => this.picker.SubtitleLanguageOnlyIfDifferent, value); }
		}

		public bool SubtitleLanguageAll
		{
			get { return this.picker.SubtitleLanguageAll; }
			set { this.UpdatePickerProperty(() => this.picker.SubtitleLanguageAll, value); }
		}

		public bool SubtitleLanguageDefault
		{
			get { return this.picker.SubtitleLanguageDefault; }
			set { this.UpdatePickerProperty(() => this.picker.SubtitleLanguageDefault, value); }
		}

		public bool SubtitleLanguageBurnIn
		{
			get { return this.picker.SubtitleLanguageBurnIn; }
			set { this.UpdatePickerProperty(() => this.picker.SubtitleLanguageBurnIn, value); }
		}

		public bool UseEncodingPreset
		{
			get { return this.picker.UseEncodingPreset; }
			set { this.UpdatePickerProperty(() => this.picker.UseEncodingPreset, value); }
		}

		private PresetViewModel selectedPreset;
		public PresetViewModel SelectedPreset
		{
			get { return this.selectedPreset; }
			set { this.RaiseAndSetIfChanged(ref this.selectedPreset, value); }
		}

		public bool AutoQueueOnScan
		{
			get { return this.picker.AutoQueueOnScan; }
			set { this.UpdatePickerProperty(() => this.picker.AutoQueueOnScan, value); }
		}

		public bool AutoEncodeOnScan
		{
			get { return this.picker.AutoEncodeOnScan; }
			set { this.UpdatePickerProperty(() => this.picker.AutoEncodeOnScan, value); }
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
			dialogVM.Name = this.picker.DisplayName;
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
			dialogVM.Name = this.picker.DisplayName;
			Ioc.Get<IWindowManager>().OpenDialog(dialogVM, this);

			if (dialogVM.DialogResult)
			{
				string newPickerName = dialogVM.Name;
				this.picker.Name = newPickerName;

				this.pickersService.SavePicker();
			}
		}

		public ReactiveCommand<object> Delete { get; private set; }

		private void DeleteImpl()
		{
			if (this.picker.IsModified)
			{
				// Revert
				MessageBoxResult dialogResult = Utilities.MessageBox.Show(
					this,
					string.Format(MainRes.RevertConfirmMessage, MainRes.PickerWord),
					string.Format(MainRes.RevertConfirmTitle, MainRes.PickerWord),
					MessageBoxButton.YesNo);
				if (dialogResult == MessageBoxResult.Yes)
				{
					this.Picker = this.pickersService.RevertPicker();
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

		private void SendTitleRangeChangeMessage()
		{
			DispatchUtilities.BeginInvoke(() =>
			{
				Messenger.Default.Send(new TitleRangeSelectChangedMessage());
			});
		}

		private void RaiseAllChanged()
		{
			foreach (string key in this.pickerProperties.Keys)
			{
				this.RaisePropertyChanged(key);
			}
		}

		private void UpdatePickerProperty<T>(Expression<Func<T>> propertyExpression, T value, bool raisePropertyChanged = true)
		{
			string propertyName = MvvmUtilities.GetPropertyName(propertyExpression);

			if (!this.pickerProperties.ContainsKey(propertyName))
			{
				throw new ArgumentException("UpdatePresetProperty called on " + propertyName + " without registering.");
			}

			if (!this.automaticChange)
			{
				bool createPicker = this.picker.IsNone;
				if (createPicker)
				{
					this.pickersService.AutoCreatePicker();
				}
				else if (!this.picker.IsModified)
				{
					// Clone the picker so we modify a different copy.
					var newPicker = new Picker();
					newPicker.InjectFrom(this.picker);
					this.Picker = newPicker;
				}
			}

			// Update the value and raise PropertyChanged
			typeAccessor[this.picker, propertyName] = value;

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

				if (!this.picker.IsModified)
				{
					this.pickersService.ModifyPicker(this.picker);
				}

				this.pickersService.SavePickersToStorage();
			}
		}
	}
}
