using VidCoderCommon.Model;

namespace VidCoder.ViewModel.DataModels
{
	public class RotationViewModel
	{
        public VCPictureRotation Rotation { get; set; }
		public string Image { get; set; }
		public string Display { get; set; }

		public bool ShowImage
		{
			get
			{
				return this.Image != null;
			}
		}
	}
}
