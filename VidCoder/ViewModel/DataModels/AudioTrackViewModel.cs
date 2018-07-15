using System;
using HandBrake.ApplicationServices.Interop.Json.Scan;
using ReactiveUI;

namespace VidCoder.ViewModel
{
	public class AudioTrackViewModel : ReactiveObject
	{
		private readonly MainViewModel mainViewModel;

		/// <summary>
		/// Creates an instance of the AudioTrackViewModel class.
		/// </summary>
		/// <param name="mainViewModel">An instance of the main viewmodel.</param>
		/// <param name="audioTrack">The audio track to wrap.</param>
		/// <param name="trackNumber">The (1-based) track number.</param>
		public AudioTrackViewModel(MainViewModel mainViewModel, SourceAudioTrack audioTrack, int trackNumber)
		{
			this.mainViewModel = mainViewModel;
			this.AudioTrack = audioTrack;
			this.TrackNumber = trackNumber;

			this.Duplicate = ReactiveCommand.Create();
			this.Duplicate.Subscribe(_ => this.DuplicateImpl());

			this.Remove = ReactiveCommand.Create();
			this.Remove.Subscribe(_ => this.RemoveImpl());
		}

		private bool selected;
		public bool Selected
		{
			get { return this.selected; }
			set
			{
				this.RaiseAndSetIfChanged(ref this.selected, value);
				this.mainViewModel.RefreshAudioSummary();
			}
		}

		public bool RemoveVisible
		{
			get
			{
				return this.mainViewModel.HasMultipleAudioTracks(this.TrackNumber);
			}
		}

		public bool DuplicateVisible
		{
			get
			{
				return !this.mainViewModel.HasMultipleAudioTracks(this.TrackNumber);
			}
		}

		public ReactiveCommand<object> Duplicate { get; }
		private void DuplicateImpl()
		{
			this.mainViewModel.DuplicateAudioTrack(this);
		}

		public ReactiveCommand<object> Remove { get; }
		private void RemoveImpl()
		{
			this.mainViewModel.RemoveAudioTrack(this);
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

		public void UpdateButtonVisiblity()
		{
			this.RaisePropertyChanged(nameof(this.DuplicateVisible));
			this.RaisePropertyChanged(nameof(this.RemoveVisible));
		}
	}
}
