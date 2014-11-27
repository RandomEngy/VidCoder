namespace VidCoder.ViewModel.DataModels
{
	using HandBrake.Interop.Model.Encoding;

	public class RotationViewModel
	{
		public PictureRotation Rotation { get; set; }
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
