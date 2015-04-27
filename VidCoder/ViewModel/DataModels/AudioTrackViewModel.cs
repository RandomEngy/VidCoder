using HandBrake.ApplicationServices.Interop.Json.Scan;

namespace VidCoder.ViewModel
{
	public class AudioTrackViewModel
	{
		private int trackNumber;

		/// <summary>
		/// Creates an instance of the AudioTrackViewModel class.
		/// </summary>
		/// <param name="audioTrack">The audio track to wrap.</param>
		/// <param name="trackNumber">The (1-based) track number.</param>
		public AudioTrackViewModel(SourceAudioTrack audioTrack, int trackNumber)
		{
			this.AudioTrack = audioTrack;
			this.trackNumber = trackNumber;
		}

		public SourceAudioTrack AudioTrack { get; set; }

		public string Display
		{
			get { return this.trackNumber + " " + this.AudioTrack.Description; }
		}
	}
}
