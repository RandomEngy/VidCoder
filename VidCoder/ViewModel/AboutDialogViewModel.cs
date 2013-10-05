using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VidCoder.ViewModel
{
	using HandBrake.Interop;
	using Resources;
	using Utilities = VidCoder.Utilities;

	public class AboutDialogViewModel : OkCancelDialogViewModel
	{
		public string Version
		{
			get
			{
				return Utilities.VersionString;
			}
		}

		public string BasedOnHandBrake
		{
			get
			{
				using (var hbInstance = new HandBrakeInstance())
				{
					hbInstance.Initialize(1);
					return string.Format(MiscRes.BasedOnHandBrake, hbInstance.Version);
				}
			}
		}

		public string Copyright
		{
			get
			{
				return string.Format(MiscRes.Copyright, "2010-2013");
			}
		}
	}
}
