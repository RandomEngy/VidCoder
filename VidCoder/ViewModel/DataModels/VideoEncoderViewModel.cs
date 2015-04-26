using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HandBrake.ApplicationServices.Interop.Model.Encoding;

namespace VidCoder.ViewModel
{
	using System.Resources;
	using GalaSoft.MvvmLight;
	using Resources;

	public class VideoEncoderViewModel : ViewModelBase
	{
		private static ResourceManager manager = new ResourceManager(typeof(EncodingRes));

		public HBVideoEncoder Encoder { get; set; }

		public string Display
		{
			get
			{
				string resourceString = manager.GetString("VideoEncoder_" + this.Encoder.ShortName.Replace(':', '_'));

				if (string.IsNullOrWhiteSpace(resourceString))
				{
					return this.Encoder.DisplayName;
				}

				return resourceString;
			}
		}
	}
}
