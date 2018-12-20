using System;
using System.Reactive;
using System.Windows.Input;
using HandBrake.Interop.Interop.Json.Scan;
using ReactiveUI;
using VidCoder.Resources;

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

		public DateTimeOffset? LastMouseDownTime { get; set; }

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

		private ReactiveCommand<Unit, Unit> duplicate;
		public ICommand Duplicate
		{
			get
			{
				return this.duplicate ?? (this.duplicate = ReactiveCommand.Create(() =>
				{
					this.mainViewModel.DuplicateAudioTrack(this);
				}));
			}
		}

		private ReactiveCommand<Unit, Unit> remove;
		public ICommand Remove
		{
			get
			{
				return this.remove ?? (this.remove = ReactiveCommand.Create(() =>
				{
					this.mainViewModel.RemoveAudioTrack(this);
				}));
			}
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

		public string SourceBitrate
		{
			get
			{
				int bitRateKbps = (this.AudioTrack.BitRate / 1000);
				if (bitRateKbps == 0)
				{
					return CommonRes.Unknown;
				}

				return bitRateKbps.ToString();
			}
		} 

		public override string ToString()
	    {
            return this.TrackNumber + " " + this.AudioTrack.Description;
        }

		public string Display => this.ToString();

		public void UpdateButtonVisiblity()
		{
			this.RaisePropertyChanged(nameof(this.DuplicateVisible));
			this.RaisePropertyChanged(nameof(this.RemoveVisible));
		}
	}
}
