using System;
using System.Collections.Generic;
using System.Linq;
using System.Resources;
using System.Text;
using HandBrake.Interop.Interop.Model.Encoding;
using VidCoder.Resources;

namespace VidCoder.ViewModel
{
	public class MixdownViewModel
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
