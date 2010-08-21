﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VidCoder.Model;
using HandBrake.Interop;

namespace VidCoder.ViewModel
{
	public class MixdownViewModel : ViewModelBase
	{
		public Mixdown Mixdown { get; set; }

		public string Display
		{
			get
			{
				return DisplayConversions.DisplayMixdown(this.Mixdown);
			}
		}
	}
}
