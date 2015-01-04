using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Security.RightsManagement;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;
using Omu.ValueInjecter;
using VidCoder.Messages;
using VidCoder.Model;
using VidCoder.Resources;
using VidCoder.Services;
using VidCoder.ViewModel.DataModels;

namespace VidCoder.ViewModel.Components
{
    /// <summary>
    /// Controls creation/modification/deletion/import/export of pickers.
    /// </summary>
    public class PickersViewModel : ViewModelBase
    {
        private MainViewModel main = Ioc.Container.GetInstance<MainViewModel>();
	    private WindowManagerViewModel windowManagerViewModel = Ioc.Container.GetInstance<WindowManagerViewModel>();

        private PickerViewModel selectedPicker;

        private ObservableCollection<PickerViewModel> pickers;

        public PickersViewModel()
        {
            this.pickers = new ObservableCollection<PickerViewModel>();
            List<Picker> storedPickers = PickerStorage.PickerList;
            var unmodifiedPickers = storedPickers.Where(picker => !picker.IsModified);
            Picker modifiedPicker = storedPickers.FirstOrDefault(picker => picker.IsModified);
            int modifiedPickerIndex = -1;

            Picker nonePicker = CreateDefaultPicker();
            nonePicker.IsNone = true;
            this.pickers.Add(new PickerViewModel(nonePicker));

            foreach (Picker storedPicker in unmodifiedPickers)
            {
                PickerViewModel pickerVM;
                if (modifiedPicker != null && modifiedPicker.Name == storedPicker.Name)
                {
                    modifiedPickerIndex = this.pickers.Count;
                    pickerVM = new PickerViewModel(modifiedPicker);
                    pickerVM.OriginalPicker = storedPicker;
                }
                else
                {
                    pickerVM = new PickerViewModel(storedPicker);
                }

                this.pickers.Add(pickerVM);
            }

            int pickerIndex;
            if (modifiedPickerIndex >= 0)
            {
                pickerIndex = modifiedPickerIndex;
            }
            else
            {
                pickerIndex = Config.LastPickerIndex;
            }

            if (pickerIndex >= this.pickers.Count)
            {
                pickerIndex = 0;
            }

            this.SelectedPicker = this.pickers[pickerIndex];
        }

        public ObservableCollection<PickerViewModel> Pickers
        {
            get { return this.pickers; }
        }

        public PickerViewModel SelectedPicker
        {
            get { return this.selectedPicker; }

            set
            {
                if (value == null)
                {
                    return;
                }

                bool changeSelectedPicker = true;

                if (this.selectedPicker != null && this.selectedPicker.IsModified)
                {
                    MessageBoxResult dialogResult = Utilities.MessageBox.Show(
                        this.main,
                        string.Format(MainRes.SaveConfirmMessage, MainRes.PickerWord),
                        string.Format(MainRes.SaveConfirmTitle, MainRes.PickerWord),
                        MessageBoxButton.YesNoCancel);

                    switch (dialogResult)
                    {
                        case MessageBoxResult.Yes:
                            this.SavePicker();
                            break;
                        case MessageBoxResult.No:
                            this.RevertPicker();
                            break;
                        case MessageBoxResult.Cancel:
                            // Queue up action to switch back to this picker
                            int currentPickerIndex = this.Pickers.IndexOf(this.selectedPicker);
                            DispatchService.BeginInvoke(() =>
                            {
                                this.SelectedPicker = this.Pickers[currentPickerIndex];
                            });

                            changeSelectedPicker = false;
                            break;
                    }
                }

                this.selectedPicker = value;
                if (changeSelectedPicker)
                {
                    this.NotifySelectedPickerChanged();
                }
            }
        }

		public string PickerButtonText
		{
			get { return string.Format(PickerRes.PickerButtonFormat, this.SelectedPicker.DisplayNameWithStar); }
		}

		public Collection<object> PickerButtonMenuItems
		{
			get
			{
				var result = new Collection<object>();

				foreach (var picker in this.Pickers)
				{
					result.Add(new MenuItem
					{
						Header = picker.DisplayNameWithStar,
						IsCheckable = true, 
						IsChecked = picker == this.SelectedPicker,
						Command = this.SelectPickerCommand,
						CommandParameter = picker
					});
				}

				result.Add(new Separator());
				result.Add(new MenuItem { Header = MainRes.EditButton, Command = this.windowManagerViewModel.OpenPickerWindowCommand });

				return result;
			}
		} 

	    private RelayCommand<PickerViewModel> selectPickerCommand;
		public RelayCommand<PickerViewModel> SelectPickerCommand
	    {
		    get
		    {
				return this.selectPickerCommand ?? (this.selectPickerCommand = new RelayCommand<PickerViewModel>(picker =>
			    {
				    if (picker != this.SelectedPicker)
				    {
					    this.SelectedPicker = picker;
				    }
			    }));
		    }
	    }

        public void SavePicker()
        {
            if (this.SelectedPicker.IsModified)
            {
                this.SelectedPicker.OriginalPicker = null;
                this.SelectedPicker.Picker.IsModified = false;
            }

            this.SelectedPicker.RefreshView();
			this.RefreshPickerButton();
            this.SavePickersToStorage();
        }

        public void SavePickerAs(string newName)
        {
            var newPicker = new Picker();
            newPicker.InjectFrom(this.SelectedPicker);
            newPicker.Name = newName;

            var newPickerVM = new PickerViewModel(newPicker);

            this.InsertNewPicker(newPickerVM);

            if (this.SelectedPicker.IsModified)
            {
                this.RevertPicker();
                this.SelectedPicker.RefreshView();
            }

            this.selectedPicker = null;
            this.SelectedPicker = newPickerVM;

			this.SavePickersToStorage();
        }

        public void AddPicker(Picker newPicker)
        {
            var newPickerVM = new PickerViewModel(newPicker);

            this.InsertNewPicker(newPickerVM);

            if (!this.SelectedPicker.IsModified)
            {
                this.selectedPicker = null;
                this.SelectedPicker = newPickerVM;
            }

			this.RefreshPickerButton();
			this.SavePickersToStorage();
        }

        public Picker RevertPicker()
        {
            Trace.Assert(this.SelectedPicker.OriginalPicker != null, "Error reverting preset: Original profile cannot be null.");
            Trace.Assert(this.SelectedPicker.OriginalPicker != this.SelectedPicker.Picker, "Error reverting preset: Original profile must be different from current profile.");

            if (this.SelectedPicker.OriginalPicker == null || this.SelectedPicker.OriginalPicker == this.SelectedPicker.Picker)
            {
                return null;
            }

            this.SelectedPicker.Picker = this.SelectedPicker.OriginalPicker;
            this.SelectedPicker.OriginalPicker = null;
            this.SelectedPicker.Picker.IsModified = false;
            this.SelectedPicker.RefreshView();

			this.SavePickersToStorage();

	        return this.SelectedPicker.Picker;
        }

        public void DeletePicker()
        {
            this.Pickers.Remove(this.SelectedPicker);
            this.selectedPicker = null;
            this.SelectedPicker = this.Pickers[0];

			this.SavePickersToStorage();
        }

        public void AutoCreatePicker()
        {
            Picker newPicker = CreateDefaultPicker();

            for (int i = 1; i < 500; i++)
            {
                string newName = string.Format(MainRes.PickerNameTemplate, i);
                if (!this.Pickers.Any(p => p.Name == newName))
                {
                    newPicker.Name = newName;
                    break;
                }
            }

            if (newPicker.Name == null)
            {
                newPicker.Name = string.Format(MainRes.PickerNameTemplate, 501);
            }

            var newPickerVM = new PickerViewModel(newPicker);

            this.InsertNewPicker(newPickerVM);

            this.selectedPicker = null;
            this.SelectedPicker = newPickerVM;
		}

		/// <summary>
		/// Starts modification of the current picker, using the new passed-in picker.
		/// </summary>
		/// <param name="newPicker">The new picker. This should be a clone of the original, with a property modified.</param>
        public void ModifyPicker(Picker newPicker)
        {
            Trace.Assert(!this.SelectedPicker.IsModified, "Cannot start modification on already modified picker.");
            Trace.Assert(this.SelectedPicker.OriginalPicker == null, "Picker already has OriginalProfile.");

            if (this.SelectedPicker.IsModified || this.SelectedPicker.OriginalPicker != null)
            {
                return;
            }

            this.SelectedPicker.OriginalPicker = this.SelectedPicker.Picker;
            this.SelectedPicker.Picker = newPicker;
            this.SelectedPicker.Picker.IsModified = true;
            this.SelectedPicker.RefreshView();

			this.RefreshPickerButton();
		}

        public void SavePickersToStorage()
        {
            List<Picker> storagePickers = new List<Picker>();
            foreach (PickerViewModel pickerVM in this.pickers)
            {
                if (!pickerVM.Picker.IsNone)
                {
                    // Add the picker
                    storagePickers.Add(pickerVM.Picker);

                    // If it's modified, add the original one in as well.
                    if (pickerVM.IsModified)
                    {
                        Trace.Assert(pickerVM.OriginalPicker != null, "Error saving pickers: Picker marked as modified but no OriginalPicker could be found.");
                        if (pickerVM.OriginalPicker != null)
                        {
                            var originalPicker = new Picker();
                            originalPicker.InjectFrom(pickerVM.OriginalPicker);
                            originalPicker.IsModified = false;

                            storagePickers.Add(originalPicker);
                        }
                    }
                }
            }

            PickerStorage.PickerList = storagePickers;
        }

        private static Picker CreateDefaultPicker()
        {
            return new Picker
            {
				TitleRangeSelectStartMinutes = 40,
				TitleRangeSelectEndMinutes = 50,
                AudioSelectionMode = AudioSelectionMode.Disabled,
                AudioLanguageCode = "und",
                SubtitleSelectionMode = SubtitleSelectionMode.Disabled,
                SubtitleLanguageCode = "und",
                SubtitleLanguageOnlyIfDifferent = true
            };
        }

        private void InsertNewPicker(PickerViewModel newPickerVM)
        {
            for (int i = 1; i < this.Pickers.Count; i++)
            {
                if (string.CompareOrdinal(newPickerVM.Picker.Name, this.Pickers[i].Picker.Name) < 0)
                {
                    this.Pickers.Insert(i, newPickerVM);
                    return;
                }
            }

            this.Pickers.Insert(this.Pickers.Count, newPickerVM);
        }

	    private void RefreshPickerButton()
	    {
			this.RaisePropertyChanged(() => this.PickerButtonText);
			this.RaisePropertyChanged(() => this.PickerButtonMenuItems);
	    }

        private void NotifySelectedPickerChanged()
        {
            var pickerWindow = WindowManager.FindWindow<PickerWindowViewModel>();
            if (pickerWindow != null)
            {
                pickerWindow.EditingPicker = this.selectedPicker.Picker;
            }

            this.RaisePropertyChanged(() => this.SelectedPicker);
			this.RefreshPickerButton();

			Messenger.Default.Send(new PickerChangedMessage());

            Config.LastPickerIndex = this.Pickers.IndexOf(this.selectedPicker);
        }
    }
}
