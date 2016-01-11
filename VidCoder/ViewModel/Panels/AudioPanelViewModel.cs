using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Windows.Input;
using HandBrake.ApplicationServices.Interop;
using HandBrake.ApplicationServices.Interop.Json.Scan;
using HandBrake.ApplicationServices.Interop.Model.Encoding;
using ReactiveUI;
using VidCoder.Extensions;
using VidCoder.Resources;
using VidCoder.Services;
using VidCoder.ViewModel.DataModels;
using VidCoderCommon.Model;

namespace VidCoder.ViewModel
{
	public class AudioPanelViewModel : PanelViewModel
	{
		private ReactiveList<AudioEncodingViewModel> audioEncodings;
		private ReactiveList<AudioOutputPreview> audioOutputPreviews;

		private List<AudioEncoderViewModel> fallbackEncoderChoices;
		private AudioEncoderViewModel audioEncoderFallback;

		private MainViewModel main = Ioc.Get<MainViewModel>();

		private PresetsService presetsService = Ioc.Get<PresetsService>();

		private bool userUpdatingFallbackEncoder;
		private bool userUpdatingCopyMask;

		public AudioPanelViewModel(EncodingWindowViewModel encodingWindowViewModel)
			: base(encodingWindowViewModel)
		{
			this.audioOutputPreviews = new ReactiveList<AudioOutputPreview>();
			this.audioEncodings = new ReactiveList<AudioEncodingViewModel>();
			this.audioEncodings.ChangeTrackingEnabled = true;

			this.fallbackEncoderChoices = new List<AudioEncoderViewModel>();

			this.audioOutputPreviews.CountChanged
				.Select(c => c > 0)
				.Subscribe(hasAudioTracks =>
				{
					this.HasAudioTracks = hasAudioTracks;
				});

			this.AddAudioEncoding = ReactiveCommand.Create();
			this.AddAudioEncoding.Subscribe(_ => this.AddAudioEncodingImpl());

			this.main.WhenAnyValue(x => x.SelectedTitle)
				.Skip(1)
				.Subscribe(_ =>
				{
					this.NotifyAudioInputChanged();
				});

			this.main.AudioChoiceChanged += (o, e) =>
			{
				this.NotifyAudioInputChanged();
			};

			this.presetsService.WhenAnyValue(x => x.SelectedPreset.Preset.EncodingProfile.ContainerName)
				.Skip(1)
				.Subscribe(_ =>
				{
					this.RefreshFallbackEncoderChoices();
					this.RefreshCopyMaskChoices();
				});

			this.presetsService.WhenAnyValue(x => x.SelectedPreset.Preset.EncodingProfile)
				.Subscribe(Observer.Create((VCProfile _) =>
				{
					this.OnProfileChanged();
				}));

			// AutoPassthroughSettingsVisible
			AudioEncodingViewModel audioEncodingViewModel;
			this.audioEncodings.ItemChanged
				.Where(x => x.PropertyName == nameof(audioEncodingViewModel.SelectedAudioEncoder) || x.PropertyName == nameof(audioEncodingViewModel.SelectedPassthrough))
				.Subscribe(_ =>
				{
					this.RaisePropertyChanged(nameof(this.AutoPassthroughSettingsVisible));
				});
			this.audioEncodings.Changed
				.Subscribe(_ =>
				{
					this.RaisePropertyChanged(nameof(this.AutoPassthroughSettingsVisible));
				});

			this.presetsService.WhenAnyValue(x => x.SelectedPreset.Preset.EncodingProfile.AudioEncoderFallback)
				.Subscribe(audioEncoderFallback =>
				{
					if (!this.userUpdatingFallbackEncoder)
					{
						this.audioEncoderFallback = this.fallbackEncoderChoices.First(c => c.Encoder.ShortName == audioEncoderFallback);
						this.RaisePropertyChanged(nameof(this.AudioEncoderFallback));
					}
				});

			// Make user updates change the profile
			CopyMaskChoiceViewModel copyMaskChoiceViewModel;
			this.CopyMaskChoices.ChangeTrackingEnabled = true;
			this.CopyMaskChoices.ItemChanged
				.Where(x => x.PropertyName == nameof(copyMaskChoiceViewModel.Enabled))
				.Subscribe(_ =>
				{
					this.userUpdatingCopyMask = true;
					this.SaveCopyMasksToProfile();
					this.userUpdatingCopyMask = false;

					this.RefreshAudioPreview();
				});

			// Make profile changes update the local mask choices
			this.presetsService.WhenAnyValue(x => x.SelectedPreset.Preset.EncodingProfile.AudioCopyMask)
				.Subscribe(audioCopyMask =>
				{
					if (this.userUpdatingCopyMask)
					{
						return;
					}

					using (this.CopyMaskChoices.SuppressChangeNotifications())
					{
						this.CopyMaskChoices.Clear();

						HBContainer container = HandBrakeEncoderHelpers.GetContainer(this.EncodingWindowViewModel.Profile.ContainerName);
						foreach (HBAudioEncoder encoder in HandBrakeEncoderHelpers.AudioEncoders)
						{
							if ((encoder.CompatibleContainers & container.Id) > 0 && encoder.IsPassthrough && encoder.ShortName != AudioEncodingViewModel.AutoPassthroughEncoder)
							{
								bool enabled = true;
								string codec = encoder.ShortName.Substring(5);

								if (audioCopyMask != null)
								{
									CopyMaskChoice profileMaskChoice = audioCopyMask.FirstOrDefault(choice => choice.Codec == codec);
									if (profileMaskChoice != null)
									{
										enabled = profileMaskChoice.Enabled;
									}
								}

								this.CopyMaskChoices.Add(new CopyMaskChoiceViewModel(codec, enabled));
							}
						}
					}

					this.RefreshAudioPreview();
				});

			this.RegisterProfileProperties();
		}

		private void RegisterProfileProperties()
		{
			this.RegisterProfileProperty(nameof(this.Profile.AudioEncoderFallback));
			this.RegisterProfileProperty(nameof(this.Profile.AudioEncodings));
			this.RegisterProfileProperty(nameof(this.Profile.AudioCopyMask));
		}

		public bool SuppressProfileEdits { get; set; }

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

		public AudioEncoderViewModel AudioEncoderFallback
		{
			get
			{
				return this.audioEncoderFallback;
			}

			set
			{
				if (value == null)
				{
					return;
				}

				this.userUpdatingFallbackEncoder = true;
				this.audioEncoderFallback = value;
				this.UpdateProfileProperty(nameof(this.Profile.AudioEncoderFallback), this.audioEncoderFallback.Encoder.ShortName, raisePropertyChanged: false);
				this.RaisePropertyChanged();
				this.RefreshAudioPreview();
				this.userUpdatingFallbackEncoder = false;
			}
		}

		public bool AutoPassthroughSettingsVisible
		{
			get
			{
				return this.audioEncodings.Any(a => a.SelectedAudioEncoder.IsPassthrough && a.SelectedPassthrough == AudioEncodingViewModel.AutoPassthroughEncoder);
			}
		}

		public ReactiveList<CopyMaskChoiceViewModel> CopyMaskChoices { get; } = new ReactiveList<CopyMaskChoiceViewModel>();

		// This would normally be an output property but a bug is stopping this from working. May be fixed in Reactive UI 7
		private bool hasAudioTracks;
		public bool HasAudioTracks
		{
			get { return this.hasAudioTracks; }
			set { this.RaiseAndSetIfChanged(ref this.hasAudioTracks, value); }
		}

		public ReactiveCommand<object> AddAudioEncoding { get; }
		private void AddAudioEncodingImpl()
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

			this.AudioEncodings.Add(new AudioEncodingViewModel(newAudioEncoding, this.MainViewModel.SelectedTitle, this.MainViewModel.GetChosenAudioTracks(), this));
			this.RefreshAudioPreview();
			this.UpdateAudioEncodings();
		}

		public void RemoveAudioEncoding(AudioEncodingViewModel audioEncodingVM)
		{
			this.AudioEncodings.Remove(audioEncodingVM);
			audioEncodingVM.Dispose();

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
			if (this.audioEncoderFallback != null)
			{
				oldEncoder = this.audioEncoderFallback.Encoder;
			}

			this.fallbackEncoderChoices = new List<AudioEncoderViewModel>();

			foreach (HBAudioEncoder encoder in HandBrakeEncoderHelpers.AudioEncoders)
			{
				if ((encoder.CompatibleContainers & container.Id) > 0 && !encoder.IsPassthrough)
				{
					this.FallbackEncoderChoices.Add(new AudioEncoderViewModel { Encoder = encoder });
				}
			}

			this.RaisePropertyChanged(nameof(this.FallbackEncoderChoices));

			this.audioEncoderFallback = this.FallbackEncoderChoices.FirstOrDefault(e => e.Encoder == oldEncoder);

			if (this.audioEncoderFallback == null)
			{
				this.audioEncoderFallback = this.FallbackEncoderChoices[0];
			}

			this.RaisePropertyChanged(nameof(this.AudioEncoderFallback));
		}

		private void RefreshCopyMaskChoices()
		{
			List<CopyMaskChoiceViewModel> oldChoices = new List<CopyMaskChoiceViewModel>(this.CopyMaskChoices);

			using (this.CopyMaskChoices.SuppressChangeNotifications())
			{
				this.CopyMaskChoices.Clear();

				HBContainer container = HandBrakeEncoderHelpers.GetContainer(this.EncodingWindowViewModel.Profile.ContainerName);
				foreach (HBAudioEncoder encoder in HandBrakeEncoderHelpers.AudioEncoders)
				{
					if ((encoder.CompatibleContainers & container.Id) > 0 && encoder.IsPassthrough && encoder.ShortName != AudioEncodingViewModel.AutoPassthroughEncoder)
					{
						bool enabled = true;
						string codec = encoder.ShortName.Substring(5);
						CopyMaskChoiceViewModel oldChoice = oldChoices.FirstOrDefault(choice => choice.Codec == codec);
						if (oldChoice != null)
						{
							enabled = oldChoice.Enabled;
						}

						this.CopyMaskChoices.Add(new CopyMaskChoiceViewModel(codec, enabled));
					}
				}
			}
		}

		private void SaveCopyMasksToProfile()
		{
			List<CopyMaskChoice> choices = this.CopyMaskChoices.Select(choice => new CopyMaskChoice { Codec = choice.Codec, Enabled = choice.Enabled }).ToList();
			this.UpdateProfileProperty(nameof(this.Profile.AudioCopyMask), choices, raisePropertyChanged: false);
		}

		public void RefreshAudioPreview()
		{
			if (this.SelectedTitle != null)
			{
				using (this.AudioOutputPreviews.SuppressChangeNotifications())
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
				if (this.CopyMaskChoices.Any(
					choice =>
					{
						if (!choice.Enabled)
						{
							return false;
						}

						return (HandBrakeEncoderHelpers.GetAudioEncoder("copy:" + choice.Codec).Id & inputTrack.Codec) > 0;
					}))
				{
					return outputPreviewTrack;
				}

				//if (HandBrakeEncoderHelpers.AudioEncoderIsCompatible(inputTrack.Codec, encoder))
				//{
				//	return outputPreviewTrack;
				//}

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
			if (!this.SuppressProfileEdits)
			{
				var newAudioEncodings = this.AudioEncodings.Select(e => e.NewAudioEncoding).ToList();
				this.UpdateProfileProperty(nameof(this.Profile.AudioEncodings), newAudioEncodings);
			}
		}

		private void OnProfileChanged()
		{
			foreach (var audioEncodingViewModel in this.audioEncodings)
			{
				audioEncodingViewModel.Dispose();
			}

			this.audioEncodings.Clear();
			foreach (AudioEncoding audioEncoding in this.Profile.AudioEncodings)
			{
				this.audioEncodings.Add(new AudioEncodingViewModel(audioEncoding, this.MainViewModel.SelectedTitle, this.MainViewModel.GetChosenAudioTracks(), this));
			}

			this.audioOutputPreviews.Clear();
			this.RefreshAudioPreview();
			this.RefreshFallbackEncoderChoices();
			this.RefreshCopyMaskChoices();

			this.audioEncoderFallback = this.FallbackEncoderChoices.SingleOrDefault(e => e.Encoder.ShortName == this.Profile.AudioEncoderFallback);
			if (this.audioEncoderFallback == null)
			{
				this.audioEncoderFallback = this.FallbackEncoderChoices[0];
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
