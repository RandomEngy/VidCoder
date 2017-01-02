namespace VidCoder.ViewModel
{
    public class AudioOutputPreview
	{
		/// <summary>
		/// Gets or sets the 1-based output track label.
		/// </summary>
		public string TrackNumber { get; set; }

		public string Name { get; set; }

		public string Encoder { get; set; }

		public string Quality { get; set; }

		public string Mixdown { get; set; }

		public string SampleRate { get; set; }

		public string Modifiers { get; set; }
	}
}
