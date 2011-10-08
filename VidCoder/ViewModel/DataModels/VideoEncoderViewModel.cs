using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GalaSoft.MvvmLight;
using HandBrake.Interop.Model.Encoding;

namespace VidCoder.ViewModel
{
	public class VideoEncoderViewModel : ViewModelBase
	{
		public VideoEncoder Encoder { get; set; }
		public string Display { get; set; }
	}
}
