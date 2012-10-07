using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GalaSoft.MvvmLight;
using HandBrake.Interop.Model.Encoding;

namespace VidCoder.ViewModel
{
	using System.Resources;
	using LocalResources;

	public class MixdownViewModel : ViewModelBase
	{
		private static ResourceManager manager = new ResourceManager(typeof (EncodingRes));

		public HBMixdown Mixdown { get; set; }

		public string Display
		{
			get
			{
				string resourceString = manager.GetString("Mixdown_" + this.Mixdown.ShortName);

				if (string.IsNullOrWhiteSpace(resourceString))
				{
					return this.Mixdown.DisplayName;
				}

				return resourceString;
			}
		}

		public bool IsCompatible { get; set; }
	}
}
