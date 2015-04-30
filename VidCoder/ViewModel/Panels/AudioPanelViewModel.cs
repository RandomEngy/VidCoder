using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;
using HandBrake.ApplicationServices.Interop;
using HandBrake.ApplicationServices.Interop.Json.Scan;
using HandBrake.ApplicationServices.Interop.Model.Encoding;
using VidCoder.Messages;
using VidCoder.Resources;
using VidCoderCommon.Model;

namespace VidCoder.ViewModel
{
	public class AudioPanelViewModel : PanelViewModel
	{
		private ObservableCollection<AudioEncodingViewModel> audioEncodings;
		private ObservableCollection<AudioOutputPreview> audioOutputPreviews;

		private List<AudioEncoderViewModel> fallbackEncoderChoices;
		private AudioEncoderViewModel selectedFallbackEncoder;

		private ICommand addAudioEncodingCommand;

		public AudioPanelViewModel(EncodingViewModel encodingViewModel)
			: base(encodingViewModel)
		{
			this.audioOutputPreviews = new ObservableCollection<AudioOutputPreview>();
			this.audioEncodings = new ObservableCollection<AudioEncodingViewModel>();

			this.fallbackEncoderChoices = new List<AudioEncoderViewModel>();

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

			Messenger.Default.Register<ContainerChangedMessage>(
				this,
				message =>
				{
					this.RefreshFallbackEncoderChoices();

					// Refresh the preview as the warnings displayed may change.
					this.RefreshAudioPreview();
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

		public List<AudioEncoderViewModel> FallbackEncoderChoices
		{
			get
			{
				return this.fallbackEncoderChoices;
			}
		}

		public AudioEncoderViewModel SelectedFallbackEncoder
		{
			get
			{
				return this.selectedFallbackEncoder;
			}

			set
			{
				if (value == null)
				{
					return;
				}

				this.selectedFallbackEncoder = value;
				this.Profile.AudioEncoderFallback = this.selectedFallbackEncoder.Encoder.ShortName;
				this.RaisePropertyChanged(() => this.SelectedFallbackEncoder);
				this.RefreshAudioPreview();

				this.IsModified = true;
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
					this.addAudioEncodingCommand = new RelayCommand(() =>
					{
						var newAudioEncoding = new AudioEncoding
						{
							InputNumber = 0,
							Encoder = HandBrakeEncoderHelpers.AudioEncoders[0].ShortName,
							Mixdown = "dpl2",
							Bitrate = 0,
							SampleRateRaw = 0,
							Gain = 0,
							Drc = 0.0
						};

						this.AudioEncodings.Add(new AudioEncodingViewModel(newAudioEncoding, this.MainViewModel.SelectedTitle, this.MainViewModel.GetChosenAudioTracks(), this.EncodingViewModel.ContainerName, this));
						this.RaisePropertyChanged(() => this.HasAudioTracks);
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
			this.RefreshAudioPreview();
			this.UpdateAudioEncodings();
			this.IsModified = true;
		}

		private void RefreshFallbackEncoderChoices()
		{
			if (this.EncodingViewModel.EncodingProfile == null)
			{
				return;
			}

			HBContainer container = HandBrakeEncoderHelpers.GetContainer(this.EncodingViewModel.EncodingProfile.ContainerName);
			HBAudioEncoder oldEncoder = null;
			if (this.selectedFallbackEncoder != null)
			{
				oldEncoder = this.selectedFallbackEncoder.Encoder;
			}

			this.fallbackEncoderChoices = new List<AudioEncoderViewModel>();

			foreach (HBAudioEncoder encoder in HandBrakeEncoderHelpers.AudioEncoders)
			{
				if ((encoder.CompatibleContainers & container.Id) > 0 && !encoder.IsPassthrough)
				{
					this.FallbackEncoderChoices.Add(new AudioEncoderViewModel { Encoder = encoder });
				}
			}

			this.RaisePropertyChanged(() => this.FallbackEncoderChoices);

			this.selectedFallbackEncoder = this.FallbackEncoderChoices.FirstOrDefault(e => e.Encoder == oldEncoder);

			if (this.selectedFallbackEncoder == null)
			{
				this.selectedFallbackEncoder = this.FallbackEncoderChoices[0];
			}

			this.RaisePropertyChanged(() => this.SelectedFallbackEncoder);
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
					if (audioVM.IsValid)
					{
						if (audioVM.TargetStreamIndex == 0)
						{
							foreach (AudioChoiceViewModel audioChoice in this.MainViewModel.AudioChoices)
							{
								AudioOutputPreview audioPreview = this.GetAudioPreview(
									this.SelectedTitle.AudioList[audioChoice.SelectedIndex], audioVM);
								if (audioPreview != null)
								{
									outputPreviews.Add(audioPreview);
								}
							}
						}
						else if (audioVM.TargetStreamIndex - 1 < chosenAudioTracks.Count)
						{
							int titleAudioIndex = chosenAudioTracks[audioVM.TargetStreamIndex - 1];

							AudioOutputPreview audioPreview = this.GetAudioPreview(this.SelectedTitle.AudioList[titleAudioIndex - 1],
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
						TrackNumber = EncodingRes.Track,
						Name = EncodingRes.Source,
						Encoder = EncodingRes.Encoder,
						Mixdown = EncodingRes.ChannelLayout,
						SampleRate = EncodingRes.SampleRate,
						Quality = EncodingRes.Quality,
						Modifiers = EncodingRes.Modifiers
					});
				}

				this.RaisePropertyChanged(() => this.HasAudioTracks);
			}
		}

		public AudioOutputPreview GetAudioPreview(SourceAudioTrack inputTrack, AudioEncodingViewModel audioVM)
		{
			HBAudioEncoder encoder = audioVM.HBAudioEncoder;

			var outputPreviewTrack = new AudioOutputPreview
			{
				Name = inputTrack.Description,
				Encoder = encoder.DisplayName
			};

			if (encoder.IsPassthrough)
			{
				// For passthrough encodes, we need to make sure the input track is of the right type
				if (!HandBrakeEncoderHelpers.AudioEncoderIsCompatible(inputTrack.Codec, encoder) && encoder.ShortName != "copy")
				{
					return null;
				}
			}

			// Find out what the real mixdown, sample rate and bitrate will be.
			HBMixdown mixdown;
			int sampleRate, bitrate;

			if (encoder.ShortName == "copy")
			{
				if (HandBrakeEncoderHelpers.AudioEncoderIsCompatible(inputTrack.Codec, encoder))
				{
					return outputPreviewTrack;
				}

				if (this.Profile.AudioEncoderFallback == null)
				{
					encoder = HandBrakeEncoderHelpers.AudioEncoders.First(a => !a.IsPassthrough);
				}
				else
				{
					encoder = HandBrakeEncoderHelpers.GetAudioEncoder(this.Profile.AudioEncoderFallback);
				}

				mixdown = HandBrakeEncoderHelpers.GetDefaultMixdown(encoder, (ulong)inputTrack.ChannelLayout);
				sampleRate = 0;
				bitrate = 0;

				outputPreviewTrack.Encoder = encoder.DisplayName;
			}
			else
			{
				mixdown = audioVM.SelectedMixdown.Mixdown;
				sampleRate = audioVM.SampleRate;
				bitrate = audioVM.SelectedBitrate.Bitrate;
			}

			HBMixdown previewMixdown;
			previewMixdown = HandBrakeEncoderHelpers.SanitizeMixdown(mixdown, encoder, (ulong)inputTrack.ChannelLayout);

			int previewSampleRate = sampleRate;
			if (previewSampleRate == 0)
			{
				previewSampleRate = inputTrack.SampleRate;
			}

			int previewBitrate = bitrate;
			if (previewBitrate == 0)
			{
				previewBitrate = HandBrakeEncoderHelpers.GetDefaultBitrate(encoder, previewSampleRate, previewMixdown);
			}
			else
			{
				previewBitrate = HandBrakeEncoderHelpers.SanitizeAudioBitrate(previewBitrate, encoder, previewSampleRate, previewMixdown);
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
				this.audioEncodings.Add(new AudioEncodingViewModel(audioEncoding, this.MainViewModel.SelectedTitle, this.MainViewModel.GetChosenAudioTracks(), this.Profile.ContainerName, this));
			}

			this.audioOutputPreviews.Clear();
			this.RefreshAudioPreview();
			this.UpdateAudioEncodings();
			this.RefreshFallbackEncoderChoices();

			this.selectedFallbackEncoder = this.FallbackEncoderChoices.SingleOrDefault(e => e.Encoder.ShortName == this.Profile.AudioEncoderFallback);
			if (this.selectedFallbackEncoder == null)
			{
				this.selectedFallbackEncoder = this.FallbackEncoderChoices[0];
			}
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
	}
}
