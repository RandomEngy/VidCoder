using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VidCoder.ViewModel
{
	using Resources;

	public class AboutDialogViewModel : OkCancelDialogViewModel
	{
		public string Version
		{
			get
			{
#pragma warning disable 162
#if BETA
				return string.Format(MiscRes.BetaVersionFormat, Utilities.CurrentVersion, Utilities.Architecture);
#endif
				return string.Format(MiscRes.VersionFormat, Utilities.CurrentVersion, Utilities.Architecture);
#pragma warning restore 162
			}
		}

		public string BasedOnHandBrake
		{
			get
			{
				return string.Format(MiscRes.BasedOnHandBrake, 5079);
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
