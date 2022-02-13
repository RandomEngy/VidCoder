using System;
using System.Reactive;
using System.Windows.Input;
using HandBrake.Interop.Interop.Json.Scan;
using ReactiveUI;
using VidCoder.Resources;
using VidCoderCommon.Model;

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
		/// <param name="chosenAudioTrack">The chosen audio track, with 1-based track number.</param>
		public AudioTrackViewModel(MainViewModel mainViewModel, SourceAudioTrack audioTrack, ChosenAudioTrack chosenAudioTrack)
		{
			this.mainViewModel = mainViewModel;
			this.AudioTrack = audioTrack;
			this.ChosenAudioTrack = chosenAudioTrack.Clone();
		}

		private bool selected;
		public bool Selected
		{
			get { return this.selected; }
			set
			{
				this.RaiseAndSetIfChanged(ref this.selected, value);
				this.mainViewModel.RefreshAudioSummary();
				this.UpdateButtonVisiblity();
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
				return !this.mainViewModel.HasMultipleAudioTracks(this.TrackNumber) && this.Selected;
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
		public int TrackIndex => this.ChosenAudioTrack.TrackNumber - 1;

		public int TrackNumber => this.ChosenAudioTrack.TrackNumber;
		
		public ChosenAudioTrack ChosenAudioTrack { get; }

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
			if (string.IsNullOrEmpty(this.AudioTrack.Name))
			{
				return this.TrackSummary;
			}
			else
			{
				return this.TrackNumber + " " + this.AudioTrack.Name + " - " + this.AudioTrack.Description;
			}
		}

		public string TrackSummary
		{
			get
			{
				return this.TrackNumber + " " + this.AudioTrack.Description;
			}
		}

		public string Name
		{
			get => this.ChosenAudioTrack.Name ?? this.AudioTrack.Name;
			set
			{
				this.ChosenAudioTrack.Name = value;
				this.RaisePropertyChanged();
			}
		}

		public string SourceName => this.AudioTrack.Name;

		public void UpdateButtonVisiblity()
		{
			this.RaisePropertyChanged(nameof(this.DuplicateVisible));
			this.RaisePropertyChanged(nameof(this.RemoveVisible));
		}
	}
}
