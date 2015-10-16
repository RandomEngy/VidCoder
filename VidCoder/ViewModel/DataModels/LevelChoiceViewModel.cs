using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VidCoder.ViewModel
{
	using GalaSoft.MvvmLight;

	public class LevelChoiceViewModel : ViewModelBase
	{
		public LevelChoiceViewModel(string value)
		{
			this.Value = value;
			this.Display = value;
			this.isCompatible = true;
		}

		public LevelChoiceViewModel(string value, string display)
		{
			this.Value = value;
			this.Display = display;
			this.isCompatible = true;
		}

		public string Value { get; set; }

		public string Display { get; set; }

		private bool isCompatible;
		public bool IsCompatible
		{
			get
			{
				return this.isCompatible;
			}

			set
			{
				this.isCompatible = value;
				this.RaisePropertyChanged(() => this.IsCompatible);
			}
		}

	    public override string ToString()
	    {
	        return this.Display;
	    }
	}
}
