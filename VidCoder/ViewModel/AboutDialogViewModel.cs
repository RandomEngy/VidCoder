using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VidCoder.ViewModel
{
	using LocalResources;

	public class AboutDialogViewModel : OkCancelDialogViewModel
	{
		public string Version
		{
			get
			{
#if BETA
				return string.Format(MiscRes.BetaVersionFormat, Utilities.CurrentVersion, Utilities.Architecture);
				//return Utilities.CurrentVersion + " Beta (" + Utilities.Architecture + ")";
#endif
				return string.Format(MiscRes.VersionFormat, Utilities.CurrentVersion, Utilities.Architecture);
				//return Utilities.CurrentVersion + " (" + Utilities.Architecture + ")";
			}
		}

		public string BasedOnHandBrake
		{
			get
			{
				return string.Format(MiscRes.BasedOnHandBrake, 4971);
			}
		}

		public string Copyright
		{
			get
			{
				return string.Format(MiscRes.Copyright, "2010-2012");
			}
		}
	}
}
