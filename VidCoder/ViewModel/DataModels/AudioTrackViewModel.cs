using HandBrake.ApplicationServices.Interop.Json.Scan;
using ReactiveUI;

namespace VidCoder.ViewModel
{
	public class AudioTrackViewModel : ReactiveObject
	{
		/// <summary>
		/// Creates an instance of the AudioTrackViewModel class.
		/// </summary>
		/// <param name="audioTrack">The audio track to wrap.</param>
		/// <param name="trackNumber">The (1-based) track number.</param>
		public AudioTrackViewModel(SourceAudioTrack audioTrack, int trackNumber)
		{
			this.AudioTrack = audioTrack;
			this.TrackNumber = trackNumber;
		}

		private bool selected;
		public bool Selected
		{
			get { return this.selected; }
			set { this.RaiseAndSetIfChanged(ref this.selected, value); }
		}

		/// <summary>
		/// Gets the 0-based index for the track.
		/// </summary>
		public int TrackIndex => this.TrackNumber - 1;

		/// <summary>
		/// Gets the 1-based track number.
		/// </summary>
		public int TrackNumber { get; }

		public SourceAudioTrack AudioTrack { get; set; }

	    public override string ToString()
	    {
            return this.TrackNumber + " " + this.AudioTrack.Description;
        }
	}
}
