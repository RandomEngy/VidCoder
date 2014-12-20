using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GalaSoft.MvvmLight;
using VidCoder.Model;

namespace VidCoder.ViewModel.DataModels
{
    public class PickerViewModel : ViewModelBase
    {
        private Picker picker;

        public PickerViewModel(Picker picker)
        {
            this.picker = picker;
        }

        public Picker Picker
        {
            get
            {
                return this.picker;
            }

            set
            {
                this.picker = value;
                this.RaisePropertyChanged(() => this.Picker);
            }
        }

        public Picker OriginalPicker { get; set; }

        public bool IsModified
        {
            get { return this.picker.IsModified; }
        }

        public string Name
        {
            get { return this.picker.Name; }
        }

        public string DisplayName
        {
            get { return this.picker.DisplayName; }
        }

        public string DisplayNameWithStar
        {
            get
            {
                string name = this.DisplayName;
                if (this.IsModified)
                {
                    name += " *";
                }

                return name;
            }
        }

        public bool IsNone
        {
            get { return this.picker.IsNone; }
        }

        public void RefreshView()
        {
            this.RaisePropertyChanged(() => this.IsModified);
            this.RaisePropertyChanged(() => this.DisplayName);
            this.RaisePropertyChanged(() => this.DisplayNameWithStar);
            this.RaisePropertyChanged(() => this.IsNone);
            this.RaisePropertyChanged(() => this.Picker);
        }
    }
}
