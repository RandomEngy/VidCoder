using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HandBrake.Interop.Interop;
using HandBrake.Interop.Utilities;
using VidCoder.Extensions;

namespace VidCoder.ViewModel
{
	using Resources;
	using Utilities = VidCoder.Utilities;

	public class AboutDialogViewModel : OkCancelDialogViewModel
	{
		public string Version => Utilities.VersionString;

		public string BasedOnHandBrake
		{
			get
			{
				return string.Format(MiscRes.BasedOnHandBrake, VersionHelper.Version);
			}
		}

		public string Copyright
		{
			get
			{
				return string.Format(MiscRes.Copyright, "2010-2018");
			}
		}
	}
}
