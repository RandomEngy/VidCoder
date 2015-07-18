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

	    private ObservableAsPropertyHelper<string> displayNameWithStar;
	    public string DisplayNameWithStar
	    {
			get { return this.displayNameWithStar.Value; }
	    }
    }
}
