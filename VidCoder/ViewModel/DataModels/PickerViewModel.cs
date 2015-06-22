using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ReactiveUI;
using VidCoder.Model;

namespace VidCoder.ViewModel.DataModels
{
    public class PickerViewModel : ReactiveObject
    {
        public PickerViewModel(Picker picker)
        {
            this.picker = picker;

	        this.WhenAnyValue(x => x.Picker.IsModified, x => x.Picker.DisplayName, (isModified, displayName) =>
	        {
				string name = displayName;
				if (isModified)
				{
					name += " *";
				}

				return name;
	        }).ToProperty(this, x => x.DisplayNameWithStar, out this.displayNameWithStar);
        }

	    private Picker picker;
	    public Picker Picker
	    {
		    get { return this.picker; }
		    set { this.RaiseAndSetIfChanged(ref this.picker, value); }
	    }

        public Picker OriginalPicker { get; set; }

		//public bool IsModified
		//{
		//	get { return this.picker.IsModified; }
		//}

		//public string Name
		//{
		//	get { return this.picker.Name; }
		//}

		//public string DisplayName
		//{
		//	get { return this.picker.DisplayName; }
		//}

	    private ObservableAsPropertyHelper<string> displayNameWithStar;
	    public string DisplayNameWithStar
	    {
			get { return this.displayNameWithStar.Value; }
	    }

		//public string DisplayNameWithStar
		//{
		//	get
		//	{
		//		string name = this.DisplayName;
		//		if (this.IsModified)
		//		{
		//			name += " *";
		//		}

		//		return name;
		//	}
		//}

		//public bool IsNone
		//{
		//	get { return this.picker.IsNone; }
		//}

		//public void RefreshView()
		//{
		//	this.RaisePropertyChanged(() => this.IsModified);
		//	this.RaisePropertyChanged(() => this.DisplayName);
		//	this.RaisePropertyChanged(() => this.DisplayNameWithStar);
		//	this.RaisePropertyChanged(() => this.IsNone);
		//	this.RaisePropertyChanged(() => this.Picker);
		//}
    }
}
