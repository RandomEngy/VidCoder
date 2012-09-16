using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GalaSoft.MvvmLight;
using HandBrake.Interop.Model.Encoding;

namespace VidCoder.ViewModel
{
	public class MixdownViewModel : ViewModelBase
	{
		public HBMixdown Mixdown { get; set; }

		public string Display
		{
			get
			{
				return this.Mixdown.DisplayName;
			}
		}

		public bool IsCompatible { get; set; }
	}
}
