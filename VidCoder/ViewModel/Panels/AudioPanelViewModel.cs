using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Windows.Input;
using DynamicData;
using DynamicData.Aggregation;
using DynamicData.Binding;
using HandBrake.Interop.Interop;
using HandBrake.Interop.Interop.Interfaces.Model;
using HandBrake.Interop.Interop.Interfaces.Model.Encoders;
using HandBrake.Interop.Interop.Json.Encode;
using HandBrake.Interop.Interop.Json.Scan;
using Microsoft.AnyContainer;
using ReactiveUI;
using VidCoder.Extensions;
using VidCoder.Resources;
using VidCoder.Services;
using VidCoder.ViewModel.DataModels;
using VidCoderCommon;
using VidCoderCommon.Model;

namespace VidCoder.ViewModel;

public class AudioPanelViewModel : PanelViewModel
{
	private List<AudioEncoderViewModel> fallbackEncoderChoices;
	private AudioEncoderViewModel audioEncoderFallback;

	private MainViewModel main = StaticResolver.Resolve<MainViewModel>();

	private PresetsService presetsService = StaticResolver.Resolve<PresetsService>();

	private bool userUpdatingFallbackEncoder;
	private bool userUpdatingCopyMask;

	public AudioPanelViewModel(EncodingWindowViewModel encodingWindowViewModel)
		: base(encodingWindowViewModel)
	{
		this.fallbackEncoderChoices = new List<AudioEncoderViewModel>();

		IObservable<IChangeSet<AudioEncodingViewModel>> audioEncodingsObservable = this.audioEncodings.Connect();
		audioEncodingsObservable.Bind(this.AudioEncodingsBindable).Subscribe();

		IObservable<IChangeSet<AudioOutputPreview>> audioOutputPreviewObservable = this.audioOutputPreviews.Connect();
		audioOutputPreviewObservable.Bind(this.AudioOutputPreviewsBindable).Subscribe();

		// HasAudioTracks
		audioOutputPreviewObservable
			.Count()
			.StartWith(this.audioOutputPreviews.Count)
			.Select(c => c > 0)
			.ToProperty(this, x => x.HasAudioTracks, out this.hasAudioTracks);

		this.main.WhenAnyValue(x => x.SelectedTitle)
			.Skip(1)
			.Subscribe(_ =>
			{
				this.NotifyAudioInputChanged();
			});

		this.main.AudioTracks
			.Connect()
			.WhenAnyPropertyChanged()
			.Subscribe(_ =>
			{
				this.NotifyAudioInputChanged();
			});

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
		audioEncodingsObservable
			.WhenAnyPropertyChanged(nameof(AudioEncodingViewModel.SelectedAudioEncoder), nameof(AudioEncodingViewModel.SelectedPassthrough))
			.Subscribe(_ =>
			{
				this.RaisePropertyChanged(nameof(this.AutoPassthroughSettingsVisible));
			});

		audioEncodingsObservable.Subscribe(changeSet =>
		{
			if (changeSet.Adds > 0 || changeSet.Removes > 0)
			{
				this.RaisePropertyChanged(nameof(this.AutoPassthroughSettingsVisible));
			}
		});

		this.presetsService.WhenAnyValue(x => x.SelectedPreset.Preset.EncodingProfile.AudioEncoderFallback)
			.Subscribe(audioEncoderFallback =>
			{
				if (!this.userUpdatingFallbackEncoder)
				{
					this.audioEncoderFallback = this.fallbackEncoderChoices.FirstOrDefault(c => c.Encoder.ShortName == audioEncoderFallback);
					if (this.audioEncoderFallback == null)
					{
						this.audioEncoderFallback = this.fallbackEncoderChoices[0];
					}

					this.RaisePropertyChanged(nameof(this.AudioEncoderFallback));
				}
			});

		// Make user updates change the profile
		IObservable<IChangeSet<CopyMaskChoiceViewModel>> copyMaskChoicesObservable = this.copyMaskChoices.Connect();
		copyMaskChoicesObservable.Bind(this.CopyMaskChoicesBindable).Subscribe();
		copyMaskChoicesObservable.WhenValueChanged(choice => choice.Enabled, notifyOnInitialValue: false).Subscribe(_ =>
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

				this.copyMaskChoices.Edit(copyMaskChoicesInnerList =>
				{
					copyMaskChoicesInnerList.Clear();

					HBContainer container = HandBrakeEncoderHelpers.GetContainer(this.EncodingWindowViewModel.Profile.ContainerName);
					foreach (HBAudioEncoder encoder in HandBrakeEncoderHelpers.AudioEncoders)
					{
						if ((encoder.CompatibleContainers & container.Id) > 0 && encoder.IsPassthru && encoder.ShortName != AudioEncodingViewModel.AutoPassthroughEncoder)
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

							copyMaskChoicesInnerList.Add(new CopyMaskChoiceViewModel(codec, enabled));
						}
					}
				});

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

	private readonly SourceList<AudioEncodingViewModel> audioEncodings = new SourceList<AudioEncodingViewModel>();
	public ObservableCollectionExtended<AudioEncodingViewModel> AudioEncodingsBindable { get; } = new ObservableCollectionExtended<AudioEncodingViewModel>();

	private readonly SourceList<AudioOutputPreview> audioOutputPreviews = new SourceList<AudioOutputPreview>();
	public ObservableCollectionExtended<AudioOutputPreview> AudioOutputPreviewsBindable { get; } = new ObservableCollectionExtended<AudioOutputPreview>();

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
			return this.audioEncodings.Items.Any(a => a.SelectedAudioEncoder.IsPassthrough && a.SelectedPassthrough == AudioEncodingViewModel.AutoPassthroughEncoder);
		}
	}

	private readonly SourceList<CopyMaskChoiceViewModel> copyMaskChoices = new SourceList<CopyMaskChoiceViewModel>();
	public ObservableCollectionExtended<CopyMaskChoiceViewModel> CopyMaskChoicesBindable { get; } = new ObservableCollectionExtended<CopyMaskChoiceViewModel>();

	private readonly ObservableAsPropertyHelper<bool> hasAudioTracks;
	public bool HasAudioTracks => this.hasAudioTracks.Value;

	private ReactiveCommand<Unit, Unit> addAudioEncoding;
	public ICommand AddAudioEncoding
	{
		get
		{
			return this.addAudioEncoding ?? (this.addAudioEncoding = ReactiveCommand.Create(() =>
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

				this.audioEncodings.Add(new AudioEncodingViewModel(newAudioEncoding, this.MainViewModel.SelectedTitle?.Title, this.MainViewModel.GetChosenAudioTracks(), this));
				this.RefreshAudioPreview();
				this.UpdateAudioEncodings();
			}));
		}
	}

	public void RemoveAudioEncoding(AudioEncodingViewModel audioEncodingVM)
	{
		this.audioEncodings.Remove(audioEncodingVM);
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

		this.FallbackEncoderChoices.Add(new AudioEncoderViewModel { Encoder = HandBrakeEncoderHelpers.GetAudioEncoder("none") });

		foreach (HBAudioEncoder encoder in HandBrakeEncoderHelpers.AudioEncoders)
		{
			if ((encoder.CompatibleContainers & container.Id) > 0 && !encoder.IsPassthru)
			{
				this.FallbackEncoderChoices.Add(new AudioEncoderViewModel { Encoder = encoder });
			}
		}

		this.RaisePropertyChanged(nameof(this.FallbackEncoderChoices));

		this.audioEncoderFallback = this.FallbackEncoderChoices.FirstOrDefault(e => e.Encoder == oldEncoder);

		if (this.audioEncoderFallback == null)
		{
			this.audioEncoderFallback = this.FallbackEncoderChoices[1];
		}

		this.RaisePropertyChanged(nameof(this.AudioEncoderFallback));
	}

	private void RefreshCopyMaskChoices()
	{
		List<CopyMaskChoiceViewModel> oldChoices = new List<CopyMaskChoiceViewModel>(this.copyMaskChoices.Items);

		this.copyMaskChoices.Edit(copyMaskChoicesInnerList =>
		{
			copyMaskChoicesInnerList.Clear();

			HBContainer container = HandBrakeEncoderHelpers.GetContainer(this.EncodingWindowViewModel.Profile.ContainerName);
			foreach (HBAudioEncoder encoder in HandBrakeEncoderHelpers.AudioEncoders)
			{
				if ((encoder.CompatibleContainers & container.Id) > 0 && encoder.IsPassthru && encoder.ShortName != AudioEncodingViewModel.AutoPassthroughEncoder)
				{
					bool enabled = true;
					string codec = encoder.ShortName.Substring(5);
					CopyMaskChoiceViewModel oldChoice = oldChoices.FirstOrDefault(choice => choice.Codec == codec);
					if (oldChoice != null)
					{
						enabled = oldChoice.Enabled;
					}

					copyMaskChoicesInnerList.Add(new CopyMaskChoiceViewModel(codec, enabled));
				}
			}
		});
	}

	private void SaveCopyMasksToProfile()
	{
		List<CopyMaskChoice> choices = this.copyMaskChoices.Items.Select(choice => new CopyMaskChoice { Codec = choice.Codec, Enabled = choice.Enabled }).ToList();
		this.UpdateProfileProperty(nameof(this.Profile.AudioCopyMask), choices, raisePropertyChanged: false);
	}

	public void RefreshAudioPreview()
	{
		if (this.SelectedTitle != null)
		{
			this.audioOutputPreviews.Edit(audioOutputPreviewsInnerList =>
			{
				audioOutputPreviewsInnerList.Clear();

				List<ChosenAudioTrack> chosenAudioTracks = this.MainViewModel.GetChosenAudioTracks();
				var outputPreviews = new List<AudioOutputPreview>();

				var resolvedAudio = JsonEncodeFactory.ResolveAudio(this.main.EncodeJob, this.SelectedTitle);
				foreach (JsonEncodeFactory.ResolvedAudioTrack resolvedTrack in resolvedAudio.ResolvedTracks)
				{
					AudioOutputPreview audioPreview = this.GetAudioPreview(
						resolvedTrack.SourceTrack,
						resolvedTrack.OutputTrack);
					if (audioPreview != null)
					{
						outputPreviews.Add(audioPreview);
					}
				}

				for (int i = 0; i < outputPreviews.Count; i++)
				{
					outputPreviews[i].TrackNumber = "#" + (i + 1);
					audioOutputPreviewsInnerList.Add(outputPreviews[i]);
				}

				// Add the header row
				if (audioOutputPreviewsInnerList.Count > 0)
				{
					audioOutputPreviewsInnerList.Insert(0, new AudioOutputPreview
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
			});
		}
	}

	public AudioOutputPreview GetAudioPreview(SourceAudioTrack inputTrack, AudioTrack handBrakeOutputTrack)
	{
		HBAudioEncoder encoder = HandBrakeEncoderHelpers.GetAudioEncoder(handBrakeOutputTrack.Encoder);

		var outputPreviewTrack = new AudioOutputPreview
		{
			Name = inputTrack.Description,
			Encoder = encoder.DisplayName
		};

		if (encoder.IsPassthru)
		{
			// For passthrough encodes, we need to make sure the input track is of the right type
			if (!HandBrakeEncoderHelpers.AudioEncoderIsCompatible(inputTrack.Codec, encoder) && encoder.ShortName != "copy")
			{
				return null;
			}

			if (this.copyMaskChoices.Items.Any(
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

			if (this.Profile.AudioEncoderFallback == null)
			{
				encoder = HandBrakeEncoderHelpers.AudioEncoders.First(a => !a.IsPassthru);
			}
			else
			{
				// The fallback is explicitly set to "None", so drop the track.
				if (this.Profile.AudioEncoderFallback == "none")
				{
					return null;
				}

				encoder = HandBrakeEncoderHelpers.GetAudioEncoder(this.Profile.AudioEncoderFallback);
			}

                // Get the default output values for the input track and encoder
		    var defaultSettings = AudioUtilities.GetDefaultSettings(inputTrack, encoder);

                // Apply the output values to the preview object
                UpdateAudioPreviewTrack(outputPreviewTrack, defaultSettings);
		}
		else
		{
		    HBMixdown previewMixdown;
		    previewMixdown = HandBrakeEncoderHelpers.SanitizeMixdown(HandBrakeEncoderHelpers.GetMixdown(handBrakeOutputTrack.Mixdown), encoder, (ulong)inputTrack.ChannelLayout);

		    int previewSampleRate = handBrakeOutputTrack.Samplerate;
		    if (previewSampleRate == 0)
		    {
		        previewSampleRate = inputTrack.SampleRate;
		    }

			AudioEncodeRateType encodeRateType = handBrakeOutputTrack.Quality != null ? AudioEncodeRateType.Quality : AudioEncodeRateType.Bitrate;

			// Collect the output values in the AudioTrack object
			var outputTrackInfo = new OutputAudioTrackInfo
                {
                    Encoder = encoder,
                    Mixdown = previewMixdown,
                    SampleRate = HandBrakeEncoderHelpers.SanitizeSampleRate(encoder, previewSampleRate),
                    EncodeRateType = encodeRateType
			};

		    if (encodeRateType == AudioEncodeRateType.Bitrate)
		    {
		        int previewBitrate = handBrakeOutputTrack.Bitrate.Value;
		        if (previewBitrate == 0)
		        {
		            previewBitrate = HandBrakeEncoderHelpers.GetDefaultBitrate(encoder, previewSampleRate, previewMixdown);
		        }
		        else
		        {
		            previewBitrate = HandBrakeEncoderHelpers.SanitizeAudioBitrate(previewBitrate, encoder, previewSampleRate, previewMixdown);
		        }

		        outputTrackInfo.Bitrate = previewBitrate;
		    }
		    else
		    {
		        outputTrackInfo.Quality = handBrakeOutputTrack.Quality.Value;
		    }

		    outputTrackInfo.Gain = handBrakeOutputTrack.Gain;
		    outputTrackInfo.Drc = handBrakeOutputTrack.DRC;

                // Apply the output values to the preview object 
		    UpdateAudioPreviewTrack(outputPreviewTrack, outputTrackInfo);
		}

		return outputPreviewTrack;
	}

        // Applies values from an AudioTrack output to the preview object
    private static void UpdateAudioPreviewTrack(AudioOutputPreview outputPreviewTrack, OutputAudioTrackInfo outputTrack)
    {
            // Change from AudioTrack to custom object and remove dependency on JSON model?

        outputPreviewTrack.Encoder = outputTrack.Encoder.DisplayName;

        outputPreviewTrack.Mixdown = outputTrack.Mixdown.DisplayName;
        outputPreviewTrack.SampleRate = DisplayConversions.DisplaySampleRate(outputTrack.SampleRate);
        if (outputTrack.EncodeRateType == AudioEncodeRateType.Bitrate)
        {
            if (outputTrack.Bitrate >= 0)
            {
                outputPreviewTrack.Quality = outputTrack.Bitrate + " kbps";
            }
            else
            {
                outputPreviewTrack.Quality = string.Empty;
            }
            }
            else
        {
            outputPreviewTrack.Quality = "CQ " + outputTrack.Quality;
        }

        var modifiers = new List<string>();
        if (outputTrack.Gain != 0)
        {
            modifiers.Add(string.Format("{0}{1} dB", outputTrack.Gain > 0 ? "+" : string.Empty, outputTrack.Gain));
        }

        if (outputTrack.Drc != 0)
        {
            modifiers.Add("DRC " + outputTrack.Drc.ToString(CultureInfo.CurrentCulture));
        }

        outputPreviewTrack.Modifiers = string.Join(", ", modifiers);
        }

	public void UpdateAudioEncodings()
	{
		if (!this.SuppressProfileEdits)
		{
			var newAudioEncodings = this.audioEncodings.Items.Select(e => e.NewAudioEncoding).ToList();
			this.UpdateProfileProperty(nameof(this.Profile.AudioEncodings), newAudioEncodings);
		}
	}

	private void OnProfileChanged()
	{
		if (this.presetsService.MarkingPresetModified)
		{
			return;
		}

		foreach (var audioEncodingViewModel in this.audioEncodings.Items)
		{
			audioEncodingViewModel.Dispose();
		}

		this.audioEncodings.Edit(audioEncodingsInnerList =>
		{
			audioEncodingsInnerList.Clear();
			foreach (AudioEncoding audioEncoding in this.Profile.AudioEncodings)
			{
				audioEncodingsInnerList.Add(new AudioEncodingViewModel(audioEncoding, this.MainViewModel.SelectedTitle?.Title, this.MainViewModel.GetChosenAudioTracks(), this));
			}
		});

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
		this.UpdateAudioEncodings();
		this.EncodingWindowViewModel.NotifyAudioEncodingChanged();

		this.RefreshAudioPreview();
	}

	public void NotifyAudioInputChanged()
	{
		this.RefreshAudioPreview();

		foreach (AudioEncodingViewModel encodingVM in this.audioEncodings.Items)
		{
			encodingVM.SetChosenTracks(this.MainViewModel.GetChosenAudioTracks(), this.MainViewModel.SelectedTitle?.Title);
		}
	}
}
