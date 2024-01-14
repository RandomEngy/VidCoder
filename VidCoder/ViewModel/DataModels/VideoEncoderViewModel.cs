using System;
using System.Collections.Generic;
using System.Linq;
using System.Resources;
using System.Text;
using HandBrake.Interop.Interop.Interfaces.Model.Encoders;
using VidCoder.Resources;

namespace VidCoder.ViewModel;

public class VideoEncoderViewModel
{
	private static ResourceManager manager = new ResourceManager(typeof(EncodingRes));

	public HBVideoEncoder Encoder { get; set; }

    public override string ToString()
    {
            string resourceString = manager.GetString("VideoEncoder_" + this.Encoder.ShortName.Replace(':', '_'));

            if (string.IsNullOrWhiteSpace(resourceString))
            {
                return this.Encoder.DisplayName;
            }

            return resourceString;
        }
}
