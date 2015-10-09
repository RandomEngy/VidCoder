using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Windows.Input;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;
using HandBrake.ApplicationServices.Interop;
using HandBrake.ApplicationServices.Interop.Json.Scan;
using HandBrake.ApplicationServices.Interop.Model.Encoding;
using ReactiveUI;
using VidCoder.Messages;
using VidCoder.Resources;
using VidCoder.Services;
using VidCoderCommon.Model;

namespace VidCoder.ViewModel
{
	public class AudioPanelViewModel : PanelViewModel
	{
		private ReactiveList<AudioEncodingViewModel> audioEncodings;
		private ReactiveList<AudioOutputPreview> audioOutputPreviews;

		private List<AudioEncoderViewModel> fallbackEncoderChoices;
		private AudioEncoderViewModel selectedFallbackEncoder;

		private PresetsService presetsService = Ioc.Get<PresetsService>();

		private ICommand addAudioEncodingCommand;

		public AudioPanelViewModel(EncodingWindowViewModel encodingWindowViewModel)
			: base(encodingWindowViewModel)
		{
			this.audioOutputPreviews = new ReactiveList<AudioOutputPreview>();
			this.audioEncodings = new ReactiveList<AudioEncodingViewModel>();

			this.fallbackEncoderChoices = new List<AudioEncoderViewModel>();

			// HasAudioTracks
			this.audioOutputPreviews.CountChanged
				.Select(c => c > 0)
				.ToProperty(this, x => x.HasAudioTracks, out this.hasAudioTracks);

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

			this.presetsService.WhenAnyValue(x => x.SelectedPreset.Preset.EncodingProfile)
				.Subscribe(Observer.Create((VCProfile _) =>
				{
					this.OnProfileChanged();
				}));

			this.RegisterProfileProperties();
		}

		private void RegisterProfileProperties()
		{
			this.RegisterProfileProperty(() => this.Profile.AudioEncoderFallback);
			this.RegisterProfileProperty(() => this.Profile.AudioEncodings);
		}

		public ReactiveList<AudioEncodingViewModel> AudioEncodings
		{
			get
			{
				return this.audioEncodings;
			}
		}

		public ReactiveList<AudioOutputPreview> AudioOutputPreviews
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
				this.UpdateProfileProperty(() => this.Profile.AudioEncoderFallback, this.selectedFallbackEncoder.Encoder.ShortName, raisePropertyChanged: false);
				this.RaisePropertyChanged();
				this.RefreshAudioPreview();
			}
		}

		private ObservableAsPropertyHelper<bool> hasAudioTracks;
		public bool HasAudioTracks
		{
			get { return this.hasAudioTracks.Value; }
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

						this.AudioEncodings.Add(new AudioEncodingViewModel(newAudioEncoding, this.MainViewModel.SelectedTitle, this.MainViewModel.GetChosenAudioTracks(), this.EncodingWindowViewModel.ContainerName, this));
						this.RefreshAudioPreview();
						this.UpdateAudioEncodings();
					});
				}

				return this.addAudioEncodingCommand;
			}
		}

		public void RemoveAudioEncoding(AudioEncodingViewModel audioEncodingVM)
		{
			this.AudioEncodings.Remove(audioEncodingVM);
			this.RefreshAudioPreview();
			this.UpdateAudioEncodings();
		}

		private void RefreshFallbackEncoderChoices()
		{
			if (this.EncodingWindowViewModel.Profile == null)
			{
				return;
			}

			HBContainer container = HandBrakeEncoderHelpers.GetContainer(this.EncodingWindowViewModel.Profile.ContainerName);
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

			this.RaisePropertyChanged(MvvmUtilities.GetPropertyName(() => this.FallbackEncoderChoices));

			this.selectedFallbackEncoder = this.FallbackEncoderChoices.FirstOrDefault(e => e.Encoder == oldEncoder);

			if (this.selectedFallbackEncoder == null)
			{
				this.selectedFallbackEncoder = this.FallbackEncoderChoices[0];
			}

			this.RaisePropertyChanged(MvvmUtilities.GetPropertyName(() => this.SelectedFallbackEncoder));
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
			var newAudioEncodings = this.AudioEncodings.Select(e => e.NewAudioEncoding).ToList();
			this.UpdateProfileProperty(() => this.Profile.AudioEncodings, newAudioEncodings);
		}

		private void OnProfileChanged()
		{
			this.audioEncodings.Clear();
			foreach (AudioEncoding audioEncoding in this.Profile.AudioEncodings)
			{
				this.audioEncodings.Add(new AudioEncodingViewModel(audioEncoding, this.MainViewModel.SelectedTitle, this.MainViewModel.GetChosenAudioTracks(), this.Profile.ContainerName, this));
			}

			this.audioOutputPreviews.Clear();
			this.RefreshAudioPreview();
			//this.UpdateAudioEncodings();
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

			this.EncodingWindowViewModel.NotifyAudioEncodingChanged();
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
