using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VidCoder.Model;
using HandBrake.Interop;

namespace VidCoder.ViewModel
{
	public class VideoEncoderViewModel : ViewModelBase
	{
		public VideoEncoder Encoder { get; set; }
		public string Display { get; set; }
	}
}
