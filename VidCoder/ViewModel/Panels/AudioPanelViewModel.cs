using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Windows.Input;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;
using HandBrake.Interop;
using HandBrake.Interop.Model;
using HandBrake.Interop.Model.Encoding;
using HandBrake.Interop.SourceData;
using VidCoder.Messages;

namespace VidCoder.ViewModel
{
	public class AudioPanelViewModel : PanelViewModel
	{
		private string passthroughWarningText;
		private bool passthroughWarningVisible;

		private ObservableCollection<AudioEncodingViewModel> audioEncodings;
		private ObservableCollection<AudioOutputPreview> audioOutputPreviews;

		private ICommand addAudioEncodingCommand;

		public AudioPanelViewModel(EncodingViewModel encodingViewModel)
			: base(encodingViewModel)
		{
			this.audioOutputPreviews = new ObservableCollection<AudioOutputPreview>();
			this.audioEncodings = new ObservableCollection<AudioEncodingViewModel>();

			Messenger.Default.Register<AudioInputChangedMessage>(
				this,
				message =>
					{
						this.NotifyAudioInputChanged();
					});

			Messenger.Default.Register<SelectedTitleChangedMessage>(
				this,
				message =>
					{
						this.NotifyAudioInputChanged();
					});
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

		public string PassthroughWarningText
		{
			get
			{
				return this.passthroughWarningText;
			}

			set
			{
				this.passthroughWarningText = value;
				this.RaisePropertyChanged(() => this.PassthroughWarningText);
			}
		}

		public bool PassthroughWarningVisible
		{
			get
			{
				return this.passthroughWarningVisible;
			}

			set
			{
				this.passthroughWarningVisible = value;
				this.RaisePropertyChanged(() => this.PassthroughWarningVisible);
			}
		}

		public ICommand AddAudioEncodingCommand
		{
			get
			{
				if (this.addAudioEncodingCommand == null)
				{
					this.addAudioEncodingCommand = new RelayCommand(() =>
					{
						var newAudioEncoding = new AudioEncoding
						{
							InputNumber = 0,
							Encoder = Encoders.AudioEncoders[0].ShortName,
							Mixdown = "dpl2",
							Bitrate = 0,
							SampleRateRaw = 0,
							Gain = 0,
							Drc = 0.0
						};

						this.AudioEncodings.Add(new AudioEncodingViewModel(newAudioEncoding, this.MainViewModel.SelectedTitle, this.MainViewModel.GetChosenAudioTracks(), this.EncodingViewModel.OutputFormat, this));
						this.RaisePropertyChanged(() => this.HasAudioTracks);
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
			this.RaisePropertyChanged(() => this.HasAudioTracks);
			this.RefreshExtensionChoice();
			this.RefreshAudioPreview();
			this.UpdateAudioEncodings();
			this.IsModified = true;
		}

		public void RefreshAudioPreview()
		{
			this.PassthroughWarningVisible = false;

			if (this.SelectedTitle != null && this.AudioOutputPreviews != null)
			{
				this.AudioOutputPreviews.Clear();

				List<int> chosenAudioTracks = this.MainViewModel.GetChosenAudioTracks();
				var outputPreviews = new List<AudioOutputPreview>();

				foreach (AudioEncodingViewModel audioVM in this.AudioEncodings)
				{
					if (audioVM.IsValid)
					{
						if (audioVM.TargetStreamIndex == 0)
						{
							foreach (AudioChoiceViewModel audioChoice in this.MainViewModel.AudioChoices)
							{
								AudioOutputPreview audioPreview = this.GetAudioPreview(
									this.SelectedTitle.AudioTracks[audioChoice.SelectedIndex], audioVM);
								if (audioPreview != null)
								{
									outputPreviews.Add(audioPreview);
								}
							}
						}
						else if (audioVM.TargetStreamIndex - 1 < chosenAudioTracks.Count)
						{
							int titleAudioIndex = chosenAudioTracks[audioVM.TargetStreamIndex - 1];

							AudioOutputPreview audioPreview = this.GetAudioPreview(this.SelectedTitle.AudioTracks[titleAudioIndex - 1],
							                                                       audioVM);
							if (audioPreview != null)
							{
								outputPreviews.Add(audioPreview);
							}
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
						Quality = "Quality",
						Modifiers = "Modifiers"
					});
				}

				this.RaisePropertyChanged(() => this.HasAudioTracks);
			}
		}

		public AudioOutputPreview GetAudioPreview(AudioTrack inputTrack, AudioEncodingViewModel audioVM)
		{
			HBAudioEncoder encoder = audioVM.SelectedAudioEncoder;

			var outputPreviewTrack = new AudioOutputPreview
			{
				Name = inputTrack.NoTrackDisplay,
				Encoder = encoder.DisplayName
			};

			if (encoder.IsPassthrough)
			{
				// For passthrough encodes, we need to make sure the input track is of the right type
				if (!Encoders.AudioEncoderIsCompatible(inputTrack, encoder))
				{
					if (encoder.ShortName == "copy")
					{
						this.PassthroughWarningText = "Passthrough does not work for this input codec; the track has been dropped.";
						this.PassthroughWarningVisible = true;
					}

					return null;
				}

				if (encoder.ShortName == "copy" && (inputTrack.Codec == AudioCodec.Dts || inputTrack.Codec == AudioCodec.DtsHD) && this.Profile.OutputFormat == Container.Mp4)
				{
					this.PassthroughWarningText = "Few players support playback of DTS audio in MP4 containers. MKV is recommended for DTS audio.";
					this.PassthroughWarningVisible = true;
				}

				return outputPreviewTrack;
			}

			// For regular encodes, we need to find out what the real mixdown, sample rate and bitrate will be.
			HBMixdown mixdown = audioVM.SelectedMixdown;
			int sampleRate = audioVM.SampleRate;
			int bitrate = audioVM.SelectedBitrate.Bitrate;

			HBMixdown previewMixdown;
			previewMixdown = Encoders.SanitizeMixdown(mixdown, encoder, inputTrack.ChannelLayout);

			int previewSampleRate = sampleRate;
			if (previewSampleRate == 0)
			{
				previewSampleRate = inputTrack.SampleRate;
			}

			int previewBitrate = bitrate;
			if (previewBitrate == 0)
			{
				previewBitrate = Encoders.GetDefaultBitrate(encoder, previewSampleRate, previewMixdown);
			}
			else
			{
				previewBitrate = Encoders.SanitizeAudioBitrate(previewBitrate, encoder, previewSampleRate, previewMixdown);
			}

			outputPreviewTrack.Mixdown = previewMixdown.DisplayName;
			outputPreviewTrack.SampleRate = DisplayConversions.DisplaySampleRate(previewSampleRate);

			if (audioVM.EncodeRateType == AudioEncodeRateType.Bitrate)
			{
				if (previewBitrate >= 0)
				{
					outputPreviewTrack.Quality = previewBitrate + " kbps";
				}
				else
				{
					outputPreviewTrack.Quality = string.Empty;
				}
			}
			else
			{
				outputPreviewTrack.Quality = "CQ " + audioVM.AudioQuality;
			}

			var modifiers = new List<string>();
			if (audioVM.Gain != 0)
			{
				modifiers.Add(string.Format("{0}{1} dB", audioVM.Gain > 0 ? "+" : string.Empty, audioVM.Gain));
			}

			if (audioVM.Drc != 0)
			{
				modifiers.Add("DRC " + audioVM.Drc.ToString());
			}

			outputPreviewTrack.Modifiers = string.Join(", ", modifiers);

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
			this.RaisePropertyChanged(() => this.HasAudioTracks);
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

		public void NotifyOutputFormatChanged(Container outputFormat)
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
