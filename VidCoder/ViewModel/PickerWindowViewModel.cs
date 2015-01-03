using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using HandBrake.Interop.Model;
using Omu.ValueInjecter;
using VidCoder.Model;
using VidCoder.Resources;
using VidCoder.ViewModel.Components;

namespace VidCoder.ViewModel
{
    public class PickerWindowViewModel : OkCancelDialogViewModel
    {
        private PickersViewModel pickersViewModel = Ioc.Container.GetInstance<PickersViewModel>();

        private Picker picker;
        private Picker originalPicker;
        private bool isNone;

        public PickerWindowViewModel(Picker picker)
        {
            this.EditingPicker = picker;
        }


        public PickersViewModel PickersViewModel
        {
            get { return this.pickersViewModel; }
        }

        public Picker EditingPicker
        {
            get { return this.originalPicker; }

            set
            {
                if (value.IsModified)
                {
                    this.picker = value;
                }
                else
                {
                    var newPicker = new Picker();
                    newPicker.InjectFrom(value);
                    this.picker = newPicker;
                }

                this.originalPicker = value;

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
                return this.originalPicker.DisplayName;
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
                    if (this.picker.IsModified != value)
                    {
                        if (value)
                        {
                            this.pickersViewModel.ModifyPicker(this.picker);
                        }
                    }

                    this.DeleteCommand.RaiseCanExecuteChanged();
                    this.RaisePropertyChanged(() => this.IsModified);
                    this.RaisePropertyChanged(() => this.WindowTitle);
                    this.RaisePropertyChanged(() => this.DeleteButtonVisible);

                    // If we've made a modification, we need to save the pickers.
                    if (value)
                    {
                        this.pickersViewModel.SavePickersToStorage();
                    }
                }
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

        private void UpdateProperty(Action action)
        {
            bool createPicker = this.picker.IsNone;
            if (createPicker)
            {
                this.pickersViewModel.AutoCreatePicker();
            }

            action();

            if (createPicker)
            {
                this.pickersViewModel.SavePickersToStorage();
            }
            else
            {
                this.IsModified = true;
            }
        }

        public IList<Language> Languages
        {
            get
            {
                return HandBrake.Interop.Helpers.Languages.AllLanguages;
            }
        }

        private RelayCommand saveCommand;
        public RelayCommand SaveCommand
        {
            get
            {
                return this.saveCommand ?? (this.saveCommand = new RelayCommand(() =>
                {
                    this.pickersViewModel.SavePicker();
                    this.IsModified = false;

                    // Clone the picker so that on modifications, we're working on a new copy.
                    var newPicker = new Picker();
                    newPicker.InjectFrom(this.picker);
                    this.picker = newPicker;
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
                    var dialogVM = new ChooseNameViewModel(MainRes.PickerWord, this.pickersViewModel.Pickers.Skip(1).Select(p => p.Name));
                    dialogVM.Name = this.originalPicker.DisplayName;
                    WindowManager.OpenDialog(dialogVM, this);

                    if (dialogVM.DialogResult)
                    {
                        string newPickerName = dialogVM.Name;

                        this.pickersViewModel.SavePickerAs(newPickerName);

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
                    var dialogVM = new ChooseNameViewModel(MainRes.PickerWord, this.pickersViewModel.Pickers.Skip(1).Select(p => p.Name));
                    dialogVM.Name = this.originalPicker.DisplayName;
                    WindowManager.OpenDialog(dialogVM, this);

                    if (dialogVM.DialogResult)
                    {
                        string newPickerName = dialogVM.Name;
                        this.originalPicker.Name = newPickerName;
                        this.picker.Name = newPickerName;

                        this.pickersViewModel.SavePicker();

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
                            this.pickersViewModel.RevertPicker();
                        }

                        this.EditingPicker = this.originalPicker;
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
                            this.pickersViewModel.DeletePicker();
                        }
                    }
                }, () =>
                {
                    // We can delete or revert if it's not the "None" picker or if there have been modifications.
                    return !this.IsNone || this.IsModified;
                }));
            }
        }

        private void NotifyAllChanged()
        {
            this.AutomaticChange = true;

            this.RaisePropertyChanged(() => this.WindowTitle);
            this.RaisePropertyChanged(() => this.PickerName);
            this.RaisePropertyChanged(() => this.IsNone);
            this.RaisePropertyChanged(() => this.DeleteButtonVisible);
            this.RaisePropertyChanged(() => this.IsModified);

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

            this.AutomaticChange = false;
        }
    }
}
