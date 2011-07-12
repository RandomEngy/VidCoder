using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Windows.Input;
using HandBrake.Interop;
using HandBrake.SourceData;

namespace VidCoder.ViewModel
{
	public class AudioPanelViewModel : PanelViewModel
	{
		private ObservableCollection<AudioEncodingViewModel> audioEncodings;
		private ObservableCollection<AudioOutputPreview> audioOutputPreviews;

		private ICommand addAudioEncodingCommand;

		public AudioPanelViewModel(EncodingViewModel encodingViewModel)
			: base(encodingViewModel)
		{
			this.audioOutputPreviews = new ObservableCollection<AudioOutputPreview>();
			this.audioEncodings = new ObservableCollection<AudioEncodingViewModel>();
		}


		public ObservableCollection<AudioEncodingViewModel> AudioEncodings
		{
			get
			{
				return this.audioEncodings;
			}
		}

		public ObservableCollection<AudioOutputPreview> AudioOutputPreviews
		{
			get
			{
				return this.audioOutputPreviews;
			}
		}

		public bool HasAudioTracks
		{
			get
			{
				return this.AudioOutputPreviews.Count > 0;
			}
		}

		public ICommand AddAudioEncodingCommand
		{
			get
			{
				if (this.addAudioEncodingCommand == null)
				{
					this.addAudioEncodingCommand = new RelayCommand(param =>
					{
						var newAudioEncoding = new AudioEncoding
						{
							InputNumber = 0,
							Encoder = AudioEncoder.Faac,
							Mixdown = Mixdown.DolbyProLogicII,
							Bitrate = 160,
							SampleRateRaw = 0,
							Gain = 0,
							Drc = 0.0
						};

						this.AudioEncodings.Add(new AudioEncodingViewModel(newAudioEncoding, this.MainViewModel.SelectedTitle, this.MainViewModel.GetChosenAudioTracks(), this.EncodingViewModel.OutputFormat, this));
						this.NotifyPropertyChanged("HasAudioTracks");
						this.RefreshExtensionChoice();
						this.RefreshAudioPreview();
						this.UpdateAudioEncodings();
						this.IsModified = true;
					});
				}

				return this.addAudioEncodingCommand;
			}
		}

		public void RemoveAudioEncoding(AudioEncodingViewModel audioEncodingVM)
		{
			this.AudioEncodings.Remove(audioEncodingVM);
			this.NotifyPropertyChanged("HasAudioTracks");
			this.RefreshExtensionChoice();
			this.RefreshAudioPreview();
			this.UpdateAudioEncodings();
			this.IsModified = true;
		}

		public void RefreshAudioPreview()
		{
			if (this.SelectedTitle != null && this.AudioOutputPreviews != null)
			{
				this.AudioOutputPreviews.Clear();

				List<int> chosenAudioTracks = this.MainViewModel.GetChosenAudioTracks();
				var outputPreviews = new List<AudioOutputPreview>();

				foreach (AudioEncodingViewModel audioVM in this.AudioEncodings)
				{
					if (audioVM.TargetStreamIndex == 0)
					{
						foreach (AudioChoiceViewModel audioChoice in this.MainViewModel.AudioChoices)
						{
							AudioOutputPreview audioPreview = this.GetAudioPreview(this.SelectedTitle.AudioTracks[audioChoice.SelectedIndex], audioVM);
							if (audioPreview != null)
							{
								outputPreviews.Add(audioPreview);
							}
						}
					}
					else if (audioVM.TargetStreamIndex - 1 < chosenAudioTracks.Count)
					{
						int titleAudioIndex = chosenAudioTracks[audioVM.TargetStreamIndex - 1];

						AudioOutputPreview audioPreview = this.GetAudioPreview(this.SelectedTitle.AudioTracks[titleAudioIndex - 1], audioVM);
						if (audioPreview != null)
						{
							outputPreviews.Add(audioPreview);
						}
					}
				}

				for (int i = 0; i < outputPreviews.Count; i++)
				{
					outputPreviews[i].TrackNumber = "#" + (i + 1);
					this.AudioOutputPreviews.Add(outputPreviews[i]);
				}

				// Add the header row
				if (this.AudioOutputPreviews.Count > 0)
				{
					this.AudioOutputPreviews.Insert(0, new AudioOutputPreview
					{
						TrackNumber = "Track",
						Name = "Source",
						Encoder = "Encoder",
						Mixdown = "Channel Layout",
						SampleRate = "Sample Rate",
						Bitrate = "Bitrate",
						Gain = "Gain",
						Drc = "DRC"
					});
				}

				this.NotifyPropertyChanged("HasAudioTracks");
			}
		}

		public AudioOutputPreview GetAudioPreview(AudioTrack inputTrack, AudioEncodingViewModel audioVM)
		{
			AudioEncoder encoder = audioVM.SelectedAudioEncoder.Encoder;

			var outputPreviewTrack = new AudioOutputPreview
			{
				Name = inputTrack.NoTrackDisplay,
				Encoder = DisplayConversions.DisplayAudioEncoder(encoder)
			};

			if (Utilities.IsPassthrough(encoder))
			{
				// For passthrough encodes, we just need to make sure the input track is of the right type
				if (encoder == AudioEncoder.Ac3Passthrough && inputTrack.Codec != AudioCodec.Ac3)
				{
					return null;
				}

				if (encoder == AudioEncoder.DtsPassthrough && inputTrack.Codec != AudioCodec.Dts)
				{
					return null;
				}

				if (encoder == AudioEncoder.Passthrough && (inputTrack.Codec != AudioCodec.Ac3 && inputTrack.Codec != AudioCodec.Dts))
				{
					return null;
				}

				return outputPreviewTrack;
			}

			// For regular encodes, we need to find out what the real mixdown, sample rate and bitrate will be.
			Mixdown mixdown = audioVM.SelectedMixdown.Mixdown;
			int sampleRate = audioVM.SampleRate;
			int bitrate = audioVM.Bitrate;

			Mixdown previewMixdown;
			if (mixdown == Mixdown.Auto)
			{
				previewMixdown = HandBrakeUtils.GetDefaultMixdown(encoder, inputTrack.ChannelLayout);
			}
			else
			{
				previewMixdown = HandBrakeUtils.SanitizeMixdown(mixdown, encoder, inputTrack.ChannelLayout);
			}

			int previewSampleRate = sampleRate;
			if (previewSampleRate == 0)
			{
				previewSampleRate = inputTrack.SampleRate;
			}

			int previewBitrate = HandBrakeUtils.SanitizeAudioBitrate(bitrate, encoder, previewSampleRate, previewMixdown);

			outputPreviewTrack.Mixdown = DisplayConversions.DisplayMixdown(previewMixdown);
			outputPreviewTrack.SampleRate = DisplayConversions.DisplaySampleRate(previewSampleRate);
			outputPreviewTrack.Bitrate = previewBitrate + " kbps";

			outputPreviewTrack.Gain = string.Format("{0}{1} dB", audioVM.Gain > 0 ? "+" : string.Empty, audioVM.Gain);
			outputPreviewTrack.Drc = audioVM.Drc.ToString();

			return outputPreviewTrack;
		}

		public void UpdateAudioEncodings()
		{
			this.Profile.AudioEncodings = new List<AudioEncoding>();
			foreach (AudioEncodingViewModel audioEncodingVM in this.AudioEncodings)
			{
				this.Profile.AudioEncodings.Add(audioEncodingVM.NewAudioEncoding);
			}
		}

		public void NotifyAllChanged()
		{
			this.NotifyPropertyChanged("HasAudioTracks");
		}

		public void NotifyProfileChanged()
		{
			this.audioEncodings.Clear();
			foreach (AudioEncoding audioEncoding in this.Profile.AudioEncodings)
			{
				this.audioEncodings.Add(new AudioEncodingViewModel(audioEncoding, this.MainViewModel.SelectedTitle, this.MainViewModel.GetChosenAudioTracks(), this.Profile.OutputFormat, this));
			}

			this.audioOutputPreviews.Clear();
			this.RefreshAudioPreview();
			this.UpdateAudioEncodings();
		}

		public void NotifyAudioEncodingChanged()
		{
			this.RefreshAudioPreview();
			this.UpdateAudioEncodings();

			this.EncodingViewModel.NotifyAudioEncodingChanged();
		}

		public void NotifyAudioInputChanged()
		{
			this.RefreshAudioPreview();

			foreach (AudioEncodingViewModel encodingVM in this.AudioEncodings)
			{
				encodingVM.SetChosenTracks(this.MainViewModel.GetChosenAudioTracks(), this.MainViewModel.SelectedTitle);
			}
		}

		public void NotifyOutputFormatChanged(OutputFormat outputFormat)
		{
			// Report output format change to audio encodings.
			foreach (AudioEncodingViewModel audioEncoding in this.AudioEncodings)
			{
				audioEncoding.OutputFormat = outputFormat;
			}
		}

		public void RefreshExtensionChoice()
		{
			this.EncodingViewModel.RefreshExtensionChoice();
		}
	}
}
