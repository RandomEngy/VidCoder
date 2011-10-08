using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GalaSoft.MvvmLight;

namespace VidCoder.ViewModel
{
	public class TargetStreamViewModel : ViewModelBase
	{
		public string Text { get; set; }
		public string TrackDetails { get; set; }

		public bool HasTrackDetails
		{
			get
			{
				return this.TrackDetails != null;
			}
		}
	}
}
