using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;
using HandBrake.ApplicationServices.Interop.Model;
using Omu.ValueInjecter;
using VidCoder.Messages;
using VidCoder.Model;
using VidCoder.Resources;
using VidCoder.Services;

namespace VidCoder.ViewModel
{
	public class PickerWindowViewModel : OkCancelDialogViewModel
	{
		private PickersService pickersService = Ioc.Container.GetInstance<PickersService>();
		private PresetsService presetsService = Ioc.Container.GetInstance<PresetsService>();

		private Picker picker;
		private bool isNone;

		public PickerWindowViewModel(Picker picker)
		{
			this.EditingPicker = picker;
		}


		public PickersService PickersService
		{
			get { return this.pickersService; }
		}

		public PresetsService PresetsService
		{
			get { return this.presetsService; }
		}

		// Should only fire when user manually closes the window, not when closed through WindowManager.
		public override void OnClosing()
		{
			Config.PickerWindowOpen = false;
			base.OnClosing();
		}

		public Picker EditingPicker
		{
			get { return this.picker; }

			set
			{
				this.picker = value;

				this.IsNone = value.IsNone;

				this.NotifyAllChanged();
			}
		}

		public string WindowTitle
		{
			get
			{
				string windowTitle = string.Format(PickerRes.WindowTitle, this.PickerName);
				if (this.IsModified)
				{
					windowTitle += " *";
				}

				return windowTitle;
			}
		}

		public string PickerName
		{
			get
			{
				return this.picker.DisplayName;
			}
		}

		public bool IsNone
		{
			get { return this.isNone; }
			set
			{
				this.isNone = value;
				this.SaveCommand.RaiseCanExecuteChanged();
				this.RenameCommand.RaiseCanExecuteChanged();
				this.DeleteCommand.RaiseCanExecuteChanged();
				this.RaisePropertyChanged(() => this.IsNone);
			}
		}

		public bool DeleteButtonVisible
		{
			get { return !this.IsNone && !this.IsModified; }
		}

		public bool AutomaticChange { get; set; }

		public bool IsModified
		{
			get
			{
				return this.picker.IsModified;
			}

			set
			{
				// Don't mark as modified if this is an automatic change.
				if (!this.AutomaticChange)
				{
					this.DeleteCommand.RaiseCanExecuteChanged();
					this.RaisePropertyChanged(() => this.IsModified);
					this.RaisePropertyChanged(() => this.WindowTitle);
					this.RaisePropertyChanged(() => this.DeleteButtonVisible);

					// If we've made a modification, we need to save the pickers.
					if (value)
					{
						this.pickersService.SavePickersToStorage();
					}
				}
			}
		}

		public bool ShowHelpMessage
		{
			get
			{
				return Config.ShowPickerWindowMessage;
			}

			set
			{
				Config.ShowPickerWindowMessage = value;
				this.RaisePropertyChanged(() => this.ShowHelpMessage);
			}
		}

		public bool OutputDirectoryOverrideEnabled
		{
			get
			{
				return this.picker.OutputDirectoryOverrideEnabled;
			}

			set
			{
				UpdateProperty(() =>
				{
					this.picker.OutputDirectoryOverrideEnabled = value;
					if (value)
					{
						this.picker.OutputDirectoryOverride = Config.AutoNameOutputFolder;
					}

					this.RaisePropertyChanged(() => this.OutputDirectoryOverrideEnabled);
					this.RaisePropertyChanged(() => this.OutputDirectoryOverride);

					Messenger.Default.Send(new OutputFolderChangedMessage());
				});
			}
		}

		public string OutputDirectoryOverride
		{
			get
			{
				if (!this.OutputDirectoryOverrideEnabled)
				{
					return Config.AutoNameOutputFolder;
				}

				return this.picker.OutputDirectoryOverride;
			}

			set
			{
				UpdateProperty(() =>
				{
					this.picker.OutputDirectoryOverride = value;
					this.RaisePropertyChanged(() => this.OutputDirectoryOverride);

					Messenger.Default.Send(new OutputFolderChangedMessage());
				});
			}
		}

		public bool NameFormatOverrideEnabled
		{
			get { return this.picker.NameFormatOverrideEnabled; }

			set
			{
				UpdateProperty(() =>
				{
					this.picker.NameFormatOverrideEnabled = value;
					this.RaisePropertyChanged(() => this.NameFormatOverrideEnabled);
					this.RaisePropertyChanged(() => this.NameFormatOverride);
				});
			}
		}

		public string NameFormatOverride
		{
			get
			{
				if (!this.NameFormatOverrideEnabled)
				{
					if (Config.AutoNameCustomFormat)
					{
						return Config.AutoNameCustomFormatString;
					}

					return string.Empty;
				}

				return this.picker.NameFormatOverride;
			}

			set
			{
				UpdateProperty(() =>
				{
					this.picker.NameFormatOverride = value;
					this.RaisePropertyChanged(() => this.NameFormatOverride);
				});
			}
		}

		public bool? OutputToSourceDirectory
		{
			get { return this.picker.OutputToSourceDirectory; }

			set
			{
				UpdateProperty(() =>
				{
					this.picker.OutputToSourceDirectory = value;
					this.RaisePropertyChanged(() => this.OutputToSourceDirectory);
				});
			}
		}

		public bool? PreserveFolderStructureInBatch
		{
			get { return this.picker.PreserveFolderStructureInBatch; }

			set
			{
				UpdateProperty(() =>
				{
					this.picker.PreserveFolderStructureInBatch = value;
					this.RaisePropertyChanged(() => this.PreserveFolderStructureInBatch);
				});
			}
		}

		public bool TitleRangeSelectEnabled
		{
			get
			{
				return this.picker.TitleRangeSelectEnabled;
			}

			set
			{
				UpdateProperty(() =>
				{
					this.picker.TitleRangeSelectEnabled = value;
					this.SendTitleRangeChangeMessage();
					this.RaisePropertyChanged(() => this.TitleRangeSelectEnabled);
				});
			}
		}

		public int TitleRangeSelectStartMinutes
		{
			get
			{
				return this.picker.TitleRangeSelectStartMinutes;
			}

			set
			{
				UpdateProperty(() =>
				{
					this.picker.TitleRangeSelectStartMinutes = value;
					if (this.TitleRangeSelectEnabled)
					{
						this.SendTitleRangeChangeMessage();
					}

					this.RaisePropertyChanged(() => this.TitleRangeSelectStartMinutes);
				});
			}
		}

		public int TitleRangeSelectEndMinutes
		{
			get
			{
				return this.picker.TitleRangeSelectEndMinutes;
			}

			set
			{
				UpdateProperty(() =>
				{
					this.picker.TitleRangeSelectEndMinutes = value;
					if (this.TitleRangeSelectEnabled)
					{
						this.SendTitleRangeChangeMessage();
					}

					this.RaisePropertyChanged(() => this.TitleRangeSelectStartMinutes);
				});
			}
		}

		public AudioSelectionMode AudioSelectionMode
		{
			get { return this.picker.AudioSelectionMode; }
			set
			{
				UpdateProperty(() =>
				{
					this.picker.AudioSelectionMode = value;
					this.RaisePropertyChanged(() => this.AudioSelectionMode);
				});
			}
		}

		public string AudioLanguageCode
		{
			get { return this.picker.AudioLanguageCode; }
			set
			{
				UpdateProperty(() =>
				{
					this.picker.AudioLanguageCode = value;
					this.RaisePropertyChanged(() => this.AudioLanguageCode);
				});
			}
		}

		public bool AudioLanguageAll
		{
			get { return this.picker.AudioLanguageAll; }
			set
			{
				UpdateProperty(() =>
				{
					this.picker.AudioLanguageAll = value;
					this.RaisePropertyChanged(() => this.AudioLanguageAll);
				});
			}
		}

		public SubtitleSelectionMode SubtitleSelectionMode
		{
			get { return this.picker.SubtitleSelectionMode; }
			set
			{
				UpdateProperty(() =>
				{
					this.picker.SubtitleSelectionMode = value;
					this.RaisePropertyChanged(() => this.SubtitleSelectionMode);
				});
			}
		}

		public bool SubtitleForeignBurnIn
		{
			get { return this.picker.SubtitleForeignBurnIn; }
			set
			{
				UpdateProperty(() =>
				{
					this.picker.SubtitleForeignBurnIn = value;
					this.RaisePropertyChanged(() => this.SubtitleForeignBurnIn);
				});
			}
		}

		public string SubtitleLanguageCode
		{
			get { return this.picker.SubtitleLanguageCode; }
			set
			{
				UpdateProperty(() =>
				{
					this.picker.SubtitleLanguageCode = value;
					this.RaisePropertyChanged(() => this.SubtitleLanguageCode);
				});
			}
		}

		public bool SubtitleLanguageOnlyIfDifferent
		{
			get { return this.picker.SubtitleLanguageOnlyIfDifferent; }
			set
			{
				UpdateProperty(() =>
				{
					this.picker.SubtitleLanguageOnlyIfDifferent = value;
					this.RaisePropertyChanged(() => this.SubtitleLanguageOnlyIfDifferent);
				});
			}
		}

		public bool SubtitleLanguageAll
		{
			get { return this.picker.SubtitleLanguageAll; }
			set
			{
				UpdateProperty(() =>
				{
					this.picker.SubtitleLanguageAll = value;
					this.RaisePropertyChanged(() => this.SubtitleLanguageAll);

					this.picker.SubtitleLanguageBurnIn = false;
					this.RaisePropertyChanged(() => this.SubtitleLanguageBurnIn);

					this.picker.SubtitleLanguageDefault = false;
					this.RaisePropertyChanged(() => this.SubtitleLanguageDefault);
				});
			}
		}

		public bool SubtitleLanguageDefault
		{
			get { return this.picker.SubtitleLanguageDefault; }
			set
			{
				UpdateProperty(() =>
				{
					this.picker.SubtitleLanguageDefault = value;
					this.RaisePropertyChanged(() => this.SubtitleLanguageDefault);

					this.picker.SubtitleLanguageBurnIn = false;
					this.RaisePropertyChanged(() => this.SubtitleLanguageBurnIn);

					this.picker.SubtitleLanguageAll = false;
					this.RaisePropertyChanged(() => this.SubtitleLanguageAll);
				});
			}
		}

		public bool SubtitleLanguageBurnIn
		{
			get { return this.picker.SubtitleLanguageBurnIn; }
			set
			{
				UpdateProperty(() =>
				{
					this.picker.SubtitleLanguageBurnIn = value;
					this.RaisePropertyChanged(() => this.SubtitleLanguageBurnIn);

					this.picker.SubtitleLanguageDefault = false;
					this.RaisePropertyChanged(() => this.SubtitleLanguageDefault);

					this.picker.SubtitleLanguageAll = false;
					this.RaisePropertyChanged(() => this.SubtitleLanguageAll);
				});
			}
		}

		public bool UseEncodingPreset
		{
			get { return this.picker.UseEncodingPreset; }
			set
			{
				UpdateProperty(() =>
				{
					this.picker.UseEncodingPreset = value;
					this.RaisePropertyChanged(() => this.UseEncodingPreset);
					this.RaisePropertyChanged(() => this.SelectedPreset);
				});
			}
		}

		public PresetViewModel SelectedPreset
		{
			get
			{
				if (!this.UseEncodingPreset)
				{
					return null;
				}

				return this.presetsService.AllPresets.FirstOrDefault(p => p.PresetName == this.picker.EncodingPreset);
			}

			set
			{

				if (value != null)
				{
					UpdateProperty(() =>
					{
						this.picker.EncodingPreset = value.PresetName;
						this.RaisePropertyChanged(() => this.SelectedPreset);
					});
				}
			}
		}

		public bool AutoQueueOnScan
		{
			get { return this.picker.AutoQueueOnScan; }
			set
			{
				UpdateProperty(() =>
				{
					this.picker.AutoQueueOnScan = value;
					if (!value)
					{
						this.picker.AutoEncodeOnScan = false;
					}

					this.RaisePropertyChanged(() => this.AutoQueueOnScan);
					this.RaisePropertyChanged(() => this.AutoEncodeOnScan);
				});
			}
		}

		public bool AutoEncodeOnScan
		{
			get
			{
				if (!this.AutoQueueOnScan)
				{
					return false;
				}

				return this.picker.AutoEncodeOnScan;
			}

			set
			{
				UpdateProperty(() =>
				{
					this.picker.AutoEncodeOnScan = value;
					this.RaisePropertyChanged(() => this.AutoEncodeOnScan);
				});
			}
		}

		private void UpdateProperty(Action action)
		{
			bool createPicker = this.picker.IsNone;
			if (createPicker)
			{
				this.pickersService.AutoCreatePicker();
			}
			else if (!this.IsModified)
			{
				// Clone the picker so we modify a different copy.
				var newPicker = new Picker();
				newPicker.InjectFrom(this.picker);
				this.picker = newPicker;
			}

			action();

			if (createPicker)
			{
				this.pickersService.SavePickersToStorage();
			}
			else
			{
				if (!this.IsModified)
				{
					this.pickersService.ModifyPicker(this.picker);
				}

				this.IsModified = true;
			}
		}

		public IList<Language> Languages
		{
			get
			{
				return HandBrake.ApplicationServices.Interop.Languages.AllLanguages;
			}
		}

		private RelayCommand dismissMessageCommand;
		public RelayCommand DismissMessageCommand
		{
			get
			{
				return this.dismissMessageCommand ?? (this.dismissMessageCommand = new RelayCommand(() =>
				{
					this.ShowHelpMessage = false;
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
					this.pickersService.SavePicker();
					this.IsModified = false;
				}, () =>
				{
					return !this.IsNone;
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
					var dialogVM = new ChooseNameViewModel(MainRes.PickerWord, this.pickersService.Pickers.Skip(1).Select(p => p.Name));
					dialogVM.Name = this.picker.DisplayName;
					WindowManager.OpenDialog(dialogVM, this);

					if (dialogVM.DialogResult)
					{
						string newPickerName = dialogVM.Name;

						this.pickersService.SavePickerAs(newPickerName);

						this.RaisePropertyChanged(() => this.PickerName);
						this.RaisePropertyChanged(() => this.WindowTitle);

						this.IsModified = false;
						this.IsNone = false;
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
					var dialogVM = new ChooseNameViewModel(MainRes.PickerWord, this.pickersService.Pickers.Skip(1).Select(p => p.Name));
					dialogVM.Name = this.picker.DisplayName;
					WindowManager.OpenDialog(dialogVM, this);

					if (dialogVM.DialogResult)
					{
						string newPickerName = dialogVM.Name;
						this.picker.Name = newPickerName;

						this.pickersService.SavePicker();

						this.RaisePropertyChanged(() => this.PickerName);
						this.RaisePropertyChanged(() => this.WindowTitle);

						this.IsModified = false;
					}
				}, () =>
				{
					return !this.IsNone;
				}));
			}
		}

		private RelayCommand deleteCommand;
		public RelayCommand DeleteCommand
		{
			get
			{
				return this.deleteCommand ?? (this.deleteCommand = new RelayCommand(() =>
				{
					if (this.IsModified)
					{
						// Revert
						MessageBoxResult dialogResult = Utilities.MessageBox.Show(
							this,
							string.Format(MainRes.RevertConfirmMessage, MainRes.PickerWord),
							string.Format(MainRes.RevertConfirmTitle, MainRes.PickerWord),
							MessageBoxButton.YesNo);
						if (dialogResult == MessageBoxResult.Yes)
						{
							this.EditingPicker = this.pickersService.RevertPicker();
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
				}, () =>
				{
					// We can delete or revert if it's not the "None" picker or if there have been modifications.
					return !this.IsNone || this.IsModified;
				}));
			}
		}

		private RelayCommand pickOutputDirectoryCommand;
		public RelayCommand PickOutputDirectoryCommand
		{
			get
			{
				return this.pickOutputDirectoryCommand ?? (this.pickOutputDirectoryCommand = new RelayCommand(() =>
				{
					string overrideFolder = FileService.Instance.GetFolderName(this.OutputDirectoryOverride, MainRes.OutputDirectoryPickerText);
					if (overrideFolder != null)
					{
						this.OutputDirectoryOverride = overrideFolder;
					}
				}));
			}
		}

		private void SendTitleRangeChangeMessage()
		{
			DispatchUtilities.BeginInvoke(() =>
			{
				Messenger.Default.Send(new TitleRangeSelectChangedMessage());
			});
		}

		private void NotifyAllChanged()
		{
			this.AutomaticChange = true;

			this.RaisePropertyChanged(() => this.WindowTitle);
			this.RaisePropertyChanged(() => this.PickerName);
			this.RaisePropertyChanged(() => this.IsNone);
			this.RaisePropertyChanged(() => this.DeleteButtonVisible);
			this.RaisePropertyChanged(() => this.IsModified);

			this.RaisePropertyChanged(() => this.OutputDirectoryOverrideEnabled);
			this.RaisePropertyChanged(() => this.OutputDirectoryOverride);
			this.RaisePropertyChanged(() => this.NameFormatOverrideEnabled);
			this.RaisePropertyChanged(() => this.NameFormatOverride);
			this.RaisePropertyChanged(() => this.OutputToSourceDirectory);
			this.RaisePropertyChanged(() => this.PreserveFolderStructureInBatch);
			this.RaisePropertyChanged(() => this.TitleRangeSelectEnabled);
			this.RaisePropertyChanged(() => this.TitleRangeSelectStartMinutes);
			this.RaisePropertyChanged(() => this.TitleRangeSelectEndMinutes);
			this.RaisePropertyChanged(() => this.AudioSelectionMode);
			this.RaisePropertyChanged(() => this.AudioLanguageCode);
			this.RaisePropertyChanged(() => this.AudioLanguageAll);
			this.RaisePropertyChanged(() => this.SubtitleSelectionMode);
			this.RaisePropertyChanged(() => this.SubtitleForeignBurnIn);
			this.RaisePropertyChanged(() => this.SubtitleLanguageCode);
			this.RaisePropertyChanged(() => this.SubtitleLanguageOnlyIfDifferent);
			this.RaisePropertyChanged(() => this.SubtitleLanguageAll);
			this.RaisePropertyChanged(() => this.SubtitleLanguageDefault);
			this.RaisePropertyChanged(() => this.SubtitleLanguageBurnIn);
			this.RaisePropertyChanged(() => this.UseEncodingPreset);
			this.RaisePropertyChanged(() => this.SelectedPreset);
			this.RaisePropertyChanged(() => this.AutoQueueOnScan);
			this.RaisePropertyChanged(() => this.AutoEncodeOnScan);

			this.AutomaticChange = false;
		}
	}
}
