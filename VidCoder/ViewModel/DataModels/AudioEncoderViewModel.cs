using System.Resources;
using HandBrake.ApplicationServices.Interop.Model.Encoding;
using ReactiveUI;
using VidCoder.Resources;

namespace VidCoder.ViewModel
{
    public class AudioEncoderViewModel : ReactiveObject
	{
		private static ResourceManager manager = new ResourceManager(typeof(EncodingRes));

		public HBAudioEncoder Encoder { get; set; }

		public bool IsPassthrough { get; set; }

	    public override string ToString()
	    {
            if (this.IsPassthrough)
            {
                return EncodingRes.AudioEncoder_Passthrough;
            }

            string resourceString = manager.GetString("AudioEncoder_" + this.Encoder.ShortName.Replace(':', '_'));

            if (string.IsNullOrWhiteSpace(resourceString))
            {
                return this.Encoder.DisplayName;
            }

            return resourceString;
        }
	}
}
