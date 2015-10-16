using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GalaSoft.MvvmLight;
using HandBrake.ApplicationServices.Interop.Model.Encoding;

namespace VidCoder.ViewModel
{
	using System.Resources;
	using Resources;

	public class MixdownViewModel : ViewModelBase
	{
		private static ResourceManager manager = new ResourceManager(typeof (EncodingRes));

		public HBMixdown Mixdown { get; set; }

		public bool IsCompatible { get; set; }

	    public override string ToString()
	    {
            string resourceString = manager.GetString("Mixdown_" + this.Mixdown.ShortName);

            if (string.IsNullOrWhiteSpace(resourceString))
            {
                return this.Mixdown.DisplayName;
            }

            return resourceString;
        }
	}
}
