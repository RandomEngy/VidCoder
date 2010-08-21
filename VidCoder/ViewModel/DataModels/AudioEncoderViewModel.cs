using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VidCoder.Model;
using HandBrake.Interop;

namespace VidCoder.ViewModel
{
	public class AudioEncoderViewModel : ViewModelBase
	{
		public AudioEncoder Encoder { get; set; }

		public string Display
		{
			get
			{
				return DisplayConversions.DisplayAudioEncoder(this.Encoder);
			}
		}
	}
}
