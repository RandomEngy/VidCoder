﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Resources;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using DynamicData;
using HandBrake.Interop.Interop;
using HandBrake.Interop.Interop.HbLib;
using HandBrake.Interop.Interop.Interfaces.Model;
using HandBrake.Interop.Interop.Interfaces.Model.Encoders;
using HandBrake.Interop.Interop.Json.Scan;
using Microsoft.AnyContainer;
using ReactiveUI;
using VidCoder.Extensions;
using VidCoder.Model;
using VidCoder.Resources;
using VidCoder.Services;
using VidCoderCommon.Model;
using VidCoderCommon.Utilities;
using Brush = System.Windows.Media.Brush;

namespace VidCoder.ViewModel
{
	public class AudioEncodingViewModel : ReactiveObject, IDisposable
	{
		private const int RangeRoundDigits = 5;
		public const string AutoPassthroughEncoder = "copy";

		private AudioPanelViewModel audioPanelVM;
		private bool initializing;

		private MainViewModel main = StaticResolver.Resolve<MainViewModel>();
		private PresetsService presetsService = StaticResolver.Resolve<PresetsService>();

		private ObservableCollection<TargetStreamViewModel> targetStreams;
		private int targetStreamIndex;
		private List<AudioEncoderViewModel> audioEncoders;
		private List<ComboChoice> passthroughChoices;
		private AudioEncoderViewModel selectedAudioEncoder;
		private List<MixdownViewModel> mixdownChoices;
		private MixdownViewModel selectedMixdown;
		private int sampleRate;
		private List<BitrateChoiceViewModel> bitrateChoices;
		private BitrateChoiceViewModel selectedBitrate;
		private int gain;
		private double drc;
		private string name;

		private static List<int> allSampleRateChoices;

		private List<int> currentSampleRateChoices;

		private IDisposable containerSubscription;
		private IDisposable presetChangeSubscription;
		private IDisposable selectedTitleSubscription;
		private IDisposable audioTrackChangedSubscription;

		static AudioEncodingViewModel()
		{
			allSampleRateChoices = new List<int> { 0 };
			foreach (var sampleRate in HandBrakeEncoderHelpers.AudioSampleRates)
			{
				allSampleRateChoices.Add(sampleRate.Rate);
			}
		}

		public AudioEncodingViewModel(AudioEncoding audioEncoding, SourceTitle selectedTitle, List<ChosenAudioTrack> chosenAudioTracks, AudioPanelViewModel audioPanelVM)
		{
			this.initializing = true;
			this.audioPanelVM = audioPanelVM;

			this.targetStreams = new ObservableCollection<TargetStreamViewModel>();
			this.targetStreamIndex = audioEncoding.InputNumber;

			this.SetChosenTracks(chosenAudioTracks, selectedTitle);

			this.audioEncoders = new List<AudioEncoderViewModel>();
			this.mixdownChoices = new List<MixdownViewModel>();

			this.RefreshEncoderChoices();

			this.presetChangeSubscription = this.presetsService.WhenAnyValue(x => x.SelectedPreset.Preset.EncodingProfile)
				.Subscribe(profile =>
				{
					this.containerSubscription?.Dispose();

					this.containerSubscription = profile.WhenAnyValue(x => x.ContainerName)
						.Skip(1)
						.Subscribe(_ =>
						{
							this.RefreshEncoderChoices(isContainerChange: true);
						});

					this.audioPanelVM.SuppressProfileEdits = true;
					this.RefreshEncoderChoices();
					this.audioPanelVM.SuppressProfileEdits = false;
				});

			HBAudioEncoder hbAudioEncoder = HandBrakeEncoderHelpers.GetAudioEncoder(audioEncoding.Encoder);
			if (hbAudioEncoder.IsPassthrough)
			{
				this.selectedAudioEncoder = this.audioEncoders[0];
				this.selectedPassthrough = audioEncoding.Encoder;
			}
			else
			{
				this.selectedAudioEncoder = this.audioEncoders.Skip(1).FirstOrDefault(e => e.Encoder.ShortName == audioEncoding.Encoder);
				this.selectedPassthrough = "copy";
			}

			if (this.selectedAudioEncoder == null)
			{
				this.selectedAudioEncoder = this.audioEncoders[1];
			}

			this.RefreshMixdownChoices();
			this.RefreshBitrateChoices();
			this.RefreshSampleRateChoices();

			this.SelectMixdown(HandBrakeEncoderHelpers.GetMixdown(audioEncoding.Mixdown));

			this.sampleRate = audioEncoding.SampleRateRaw;

			if (!this.HBAudioEncoder.SupportsQuality)
			{
				this.encodeRateType = AudioEncodeRateType.Bitrate;
			}
			else
			{
				this.encodeRateType = audioEncoding.EncodeRateType;
			}

			this.audioQuality = audioEncoding.Quality;

			if (audioEncoding.Compression >= 0)
			{
				this.audioCompression = audioEncoding.Compression;
			}
			else
			{
				this.audioCompression = this.HBAudioEncoder.DefaultCompression;
			}

			this.selectedBitrate = this.BitrateChoices.SingleOrDefault(b => b.Bitrate == audioEncoding.Bitrate);
			if (this.selectedBitrate == null)
			{
				this.selectedBitrate = this.BitrateChoices.First();
			}

			this.gain = audioEncoding.Gain;
			this.drc = audioEncoding.Drc;
			this.passthroughIfPossible = audioEncoding.PassthroughIfPossible;
			this.name = audioEncoding.Name;

			// EncoderSettingsVisible
			this.WhenAnyValue(x => x.SelectedAudioEncoder, x => x.SelectedPassthrough, (audioEncoder, passthrough) =>
			{
				if (audioEncoder == null)
				{
					return false;
				}

				if (passthrough == null)
				{
					return false;
				}

				return !GetHBAudioEncoder(audioEncoder, passthrough).IsPassthrough;
			}).ToProperty(this, x => x.EncoderSettingsVisible, out this.encoderSettingsVisible);

			// AudioCompressionVisible
			this.WhenAnyValue(x => x.SelectedAudioEncoder, audioEncoder =>
			{
				return !audioEncoder.IsPassthrough && audioEncoder.Encoder.SupportsCompression;
			}).ToProperty(this, x => x.AudioCompressionVisible, out this.audioCompressionVisible);

			// PassthroughIfPossibleVisible
			this.WhenAnyValue(x => x.SelectedAudioEncoder, audioEncoder =>
			{
				if (audioEncoder.IsPassthrough)
				{
					return false;
				}

				if (HandBrakeEncoderHelpers.AudioEncoders.Any(e => e.Id == (HandBrakeNativeConstants.HB_ACODEC_PASS_FLAG | audioEncoder.Encoder.Id)))
				{
					return true;
				}

				return audioEncoder.Encoder.ShortName.ToLowerInvariant().Contains("aac") || audioEncoder.Encoder.ShortName.ToLowerInvariant().Contains("mp3");
			}).ToProperty(this, x => x.PassthroughIfPossibleVisible, out this.passthroughIfPossibleVisible);

			// BitrateVisible
			this.WhenAnyValue(
				x => x.SelectedAudioEncoder, 
				x => x.EncoderSettingsVisible,
				x => x.SelectedMixdown, 
				x => x.EncodeRateType,
				(audioEncoder, encoderSettingsVisible, mixdown, encodeRateType) =>
				{
				    if (audioEncoder.IsPassthrough)
				    {
				        return false;
				    }

					if (encoderSettingsVisible && mixdown != null && encodeRateType == AudioEncodeRateType.Bitrate)
					{
						// We only need to find out if the bitrate limits exist, so pass in some normal values for sample rate and mixdown.
						BitrateLimits bitrateLimits = HandBrakeEncoderHelpers.GetBitrateLimits(audioEncoder.Encoder, 48000, HandBrakeEncoderHelpers.GetMixdown("dpl2"));
						return bitrateLimits.High > 0;
					}

					return false;
				}).ToProperty(this, x => x.BitrateVisible, out this.bitrateVisible);

			// BitrateLabelVisible
			this.WhenAnyValue(x => x.BitrateVisible, x => x.SelectedAudioEncoder, (bitrateVisible, audioEncoder) =>
			{
				return !audioEncoder.IsPassthrough && bitrateVisible && !audioEncoder.Encoder.SupportsQuality;
			}).ToProperty(this, x => x.BitrateLabelVisible, out this.bitrateLabelVisible);

			// AudioQualityVisible
			this.WhenAnyValue(x => x.SelectedAudioEncoder, x => x.EncodeRateType, (audioEncoder, encodeRateType) =>
			{
                return !audioEncoder.IsPassthrough &&
                    audioEncoder.Encoder.SupportsQuality &&
                    encodeRateType == AudioEncodeRateType.Quality;
				
			}).ToProperty(this, x => x.AudioQualityVisible, out this.audioQualityVisible);

			// AudioQualityRadioVisible
			this.WhenAnyValue(x => x.SelectedAudioEncoder, audioEncoder =>
			{
				return !audioEncoder.IsPassthrough && audioEncoder.Encoder.SupportsQuality;
			}).ToProperty(this, x => x.AudioQualityRadioVisible, out this.audioQualityRadioVisible);

			// AudioQualityMinimum
			this.WhenAnyValue(x => x.SelectedAudioEncoder, audioEncoder =>
			{
                if (audioEncoder.IsPassthrough)
                {
                    return 0;
                }

                return Math.Round(audioEncoder.Encoder.QualityLimits.Low, RangeRoundDigits);
			}).ToProperty(this, x => x.AudioQualityMinimum, out this.audioQualityMinimum);

			// AudioQualityMaximum
			this.WhenAnyValue(x => x.SelectedAudioEncoder, audioEncoder =>
			{
			    if (audioEncoder.IsPassthrough)
			    {
			        return 0;
			    }

				return Math.Round(audioEncoder.Encoder.QualityLimits.High, RangeRoundDigits);
			}).ToProperty(this, x => x.AudioQualityMaximum, out this.audioQualityMaximum);

			// AudioQualityGranularity
			this.WhenAnyValue(x => x.SelectedAudioEncoder, audioEncoder =>
			{
                if (audioEncoder.IsPassthrough)
                {
                    return 0;
                }

                return Math.Round(audioEncoder.Encoder.QualityLimits.Granularity, RangeRoundDigits);
			}).ToProperty(this, x => x.AudioQualityGranularity, out this.audioQualityGranularity);

			// AudioQualityToolTip
			this.WhenAnyValue(x => x.SelectedAudioEncoder, audioEncoder =>
			{
			    if (audioEncoder.IsPassthrough)
			    {
			        return string.Empty;
			    }

				string directionSentence;
				if (audioEncoder.Encoder.QualityLimits.Ascending)
				{
					directionSentence = EncodingRes.AscendingQualityToolTip;
				}
				else
				{
					directionSentence = EncodingRes.DescendingQualityToolTip;
				}

				return string.Format(
					EncodingRes.AudioQualityToolTip,
					directionSentence,
					audioEncoder.Encoder.QualityLimits.Low,
					audioEncoder.Encoder.QualityLimits.High);
			}).ToProperty(this, x => x.AudioQualityToolTip, out this.audioQualityToolTip);

			// AudioCompressionMinimum
			this.WhenAnyValue(x => x.SelectedAudioEncoder, audioEncoder =>
			{
                if (audioEncoder.IsPassthrough)
                {
                    return 0;
                }

                return Math.Round(audioEncoder.Encoder.CompressionLimits.Low, RangeRoundDigits);
			}).ToProperty(this, x => x.AudioCompressionMinimum, out this.audioCompressionMinimum);

			// AudioCompressionMaximum
			this.WhenAnyValue(x => x.SelectedAudioEncoder, audioEncoder =>
			{
                if (audioEncoder.IsPassthrough)
                {
                    return 0;
                }

                return Math.Round(audioEncoder.Encoder.CompressionLimits.High, RangeRoundDigits);
			}).ToProperty(this, x => x.AudioCompressionMaximum, out this.audioCompressionMaximum);

			// AudioCompressionGranularity
			this.WhenAnyValue(x => x.SelectedAudioEncoder, audioEncoder =>
			{
                if (audioEncoder.IsPassthrough)
                {
                    return 0;
                }

                return Math.Round(audioEncoder.Encoder.CompressionLimits.Granularity, RangeRoundDigits);
			}).ToProperty(this, x => x.AudioCompressionGranularity, out this.audioCompressionGranularity);

			// AudioCompressionToolTip
			this.WhenAnyValue(x => x.SelectedAudioEncoder, audioEncoder =>
			{
                if (audioEncoder.IsPassthrough)
                {
                    return string.Empty;
                }

                string directionSentence;
				if (audioEncoder.Encoder.QualityLimits.Ascending)
				{
					directionSentence = EncodingRes.AscendingCompressionToolTip;
				}
				else
				{
					directionSentence = EncodingRes.DescendingCompressionToolTip;
				}

				return string.Format(
					EncodingRes.AudioCompressionToolTip,
					directionSentence,
					audioEncoder.Encoder.CompressionLimits.Low,
					audioEncoder.Encoder.CompressionLimits.High);
			}).ToProperty(this, x => x.AudioCompressionToolTip, out this.audioCompressionToolTip);

			// PassthroughChoicesVisible
			this.WhenAnyValue(x => x.SelectedAudioEncoder, audioEncoder =>
			{
				return audioEncoder.IsPassthrough;
			}).ToProperty(this, x => x.PassthroughChoicesVisible, out this.passthroughChoicesVisible);

			this.selectedTitleSubscription = this.main.WhenAnyValue(x => x.SelectedTitle)
				.Skip(1)
				.Subscribe(_ =>
				{
					this.RefreshFromNewInput();
				});

			this.audioTrackChangedSubscription = this.main.AudioTracks.Connect().WhenAnyPropertyChanged().Subscribe(_ =>
			{
				this.RefreshFromNewInput();
			});

			this.initializing = false;
		}

		public void Dispose()
		{
			this.containerSubscription?.Dispose();
			this.containerSubscription = null;

			this.presetChangeSubscription?.Dispose();
			this.presetChangeSubscription = null;

			this.selectedTitleSubscription?.Dispose();
			this.selectedTitleSubscription = null;

			this.audioTrackChangedSubscription?.Dispose();
			this.audioTrackChangedSubscription = null;
		}

		private void RefreshFromNewInput()
		{
			this.RefreshMixdownChoices();
			this.RefreshBitrateChoices();
			this.RefreshDrc();
			this.RefreshSourceName();
		}

		public AudioPanelViewModel AudioPanelVM
		{
			get
			{
				return this.audioPanelVM;
			}
		}

		public AudioEncoding NewAudioEncoding
		{
			get
			{
				var newAudioEncoding = new AudioEncoding();
				newAudioEncoding.InputNumber = this.TargetStreamIndex;

				newAudioEncoding.Encoder = this.HBAudioEncoder.ShortName;

				newAudioEncoding.Name = this.Name;

				if (!this.HBAudioEncoder.IsPassthrough)
				{
					newAudioEncoding.Mixdown = this.SelectedMixdown.Mixdown.ShortName;
					newAudioEncoding.SampleRateRaw = this.SampleRate;

					newAudioEncoding.EncodeRateType = this.EncodeRateType;
					if (this.EncodeRateType == AudioEncodeRateType.Bitrate)
					{
						newAudioEncoding.Bitrate = this.SelectedBitrate.Bitrate;
					}
					else if (this.EncodeRateType == AudioEncodeRateType.Quality)
					{
						newAudioEncoding.Quality = this.AudioQuality;
					}

					if (this.HBAudioEncoder.SupportsCompression)
					{
						newAudioEncoding.Compression = this.AudioCompression;
					}

					newAudioEncoding.Gain = this.Gain;
					newAudioEncoding.Drc = this.Drc;
					newAudioEncoding.PassthroughIfPossible = this.PassthroughIfPossible;
				}

				return newAudioEncoding;
			}
		}

		public ObservableCollection<TargetStreamViewModel> TargetStreams
		{
			get
			{
				return this.targetStreams;
			}
		}

		/// <summary>
		/// Gets or sets the target stream index for this encoding. 0 is All.
		/// </summary>
		public int TargetStreamIndex
		{
			get
			{
				return this.targetStreamIndex;
			}

			set
			{
				if (value >= 0)
				{
					this.targetStreamIndex = value;
					this.RaisePropertyChanged();
					this.audioPanelVM.NotifyAudioEncodingChanged();
				}
			}
		}

		public List<AudioEncoderViewModel> AudioEncoders
		{
			get
			{
				return this.audioEncoders;
			}
		}

		public AudioEncoderViewModel SelectedAudioEncoder
		{
			get
			{
				return this.selectedAudioEncoder;
			}

			set
			{
				this.selectedAudioEncoder = value;

				if (value == null)
				{
					return;
				}

				this.RaisePropertyChanged();

				this.RefreshMixdownChoices();
				this.RefreshBitrateChoices();
				this.RefreshSampleRateChoices();
				this.RefreshDrc();

				if (!value.IsPassthrough)
				{
					if (this.SelectedBitrate == null && this.BitrateChoices.Count > 0)
					{
						this.SelectedBitrate = this.BitrateChoices[0];
					}

					// Set encode rate type to Bitrate if quality is not supported.
					if (!value.Encoder.SupportsQuality)
					{
						this.encodeRateType = AudioEncodeRateType.Bitrate;
						this.RaisePropertyChanged(nameof(this.EncodeRateType));
					}

					// On encoder switch set default quality/compression if supported.
					if (value.Encoder.SupportsQuality)
					{
						this.audioQuality = value.Encoder.DefaultQuality;
						this.RaisePropertyChanged(nameof(this.AudioQuality));
					}

					if (value.Encoder.SupportsCompression)
					{
						this.audioCompression = value.Encoder.DefaultCompression;
						this.RaisePropertyChanged(nameof(this.AudioCompression));
					}
				}

				this.RaiseAudioEncodingChanged();
			}
		}

		public HBAudioEncoder HBAudioEncoder
		{
			get
			{
				return GetHBAudioEncoder(this.SelectedAudioEncoder, this.SelectedPassthrough);
			}
		}

		/// <summary>
		/// Gets the audio encoder to use, given the main encoder choice and the passthrough choice. The generic "Passthrough" choice
		/// on the main encoder picker is split out into the specific types via the "scope" dropdown.
		/// </summary>
		/// <param name="audioEncoderViewModel">The chosen audio encoder.</param>
		/// <param name="passthrough">The short encoder id of the passthrough option.</param>
		/// <returns></returns>
		private static HBAudioEncoder GetHBAudioEncoder(AudioEncoderViewModel audioEncoderViewModel, string passthrough)
		{
			if (audioEncoderViewModel == null)
			{
				return null;
			}

			if (audioEncoderViewModel.IsPassthrough)
			{
				return HandBrakeEncoderHelpers.GetAudioEncoder(passthrough);
			}

			return audioEncoderViewModel.Encoder;
		}

		private ObservableAsPropertyHelper<bool> encoderSettingsVisible;
		public bool EncoderSettingsVisible => this.encoderSettingsVisible.Value;

		public List<ComboChoice> PassthroughChoices
		{
			get
			{
				return this.passthroughChoices;
			}
		}

		private string selectedPassthrough;
		public string SelectedPassthrough
		{
			get
			{
				return this.selectedPassthrough;
			}

			set
			{
				this.selectedPassthrough = value;
				this.RaiseAudioPropertyChanged();
			}
		}

		private ObservableAsPropertyHelper<bool> passthroughChoicesVisible;
		public bool PassthroughChoicesVisible => this.passthroughChoicesVisible.Value;

		private AudioEncodeRateType encodeRateType;
		public AudioEncodeRateType EncodeRateType
		{
			get
			{
				return this.encodeRateType;
			}

			set
			{
				this.encodeRateType = value;

				// Set default quality when switching to quality
				if (value == AudioEncodeRateType.Quality)
				{
					this.audioQuality = this.HBAudioEncoder.DefaultQuality;
					this.RaisePropertyChanged(nameof(this.AudioQuality));
				}

				this.RaiseAudioPropertyChanged();
			}
		}

		private ObservableAsPropertyHelper<bool> bitrateVisible;
		public bool BitrateVisible => this.bitrateVisible.Value;

		private ObservableAsPropertyHelper<bool> bitrateLabelVisible;
		public bool BitrateLabelVisible => this.bitrateLabelVisible.Value;

		private float audioQuality;
		public float AudioQuality
		{
			get
			{
				return this.audioQuality;
			}

			set
			{
				this.audioQuality = value;
				this.RaiseAudioPropertyChanged();
			}
		}

		private ObservableAsPropertyHelper<bool> audioQualityVisible;
		public bool AudioQualityVisible => this.audioQualityVisible.Value;

		private ObservableAsPropertyHelper<bool> audioQualityRadioVisible;
		public bool AudioQualityRadioVisible => this.audioQualityRadioVisible.Value;

		private ObservableAsPropertyHelper<double> audioQualityMinimum;
		public double AudioQualityMinimum => this.audioQualityMinimum.Value;

		private ObservableAsPropertyHelper<double> audioQualityMaximum;
		public double AudioQualityMaximum => this.audioQualityMaximum.Value;

		private ObservableAsPropertyHelper<double> audioQualityGranularity;
		public double AudioQualityGranularity => this.audioQualityGranularity.Value;

		private ObservableAsPropertyHelper<string> audioQualityToolTip;
		public string AudioQualityToolTip => this.audioQualityToolTip.Value;

		private float audioCompression;
		public float AudioCompression
		{
			get
			{
				return this.audioCompression;
			}

			set
			{
				this.audioCompression = value;
				this.RaiseAudioPropertyChanged();
			}
		}

		private ObservableAsPropertyHelper<bool> audioCompressionVisible;
		public bool AudioCompressionVisible => this.audioCompressionVisible.Value;

		private ObservableAsPropertyHelper<double> audioCompressionMinimum;
		public double AudioCompressionMinimum => this.audioCompressionMinimum.Value;

		private ObservableAsPropertyHelper<double> audioCompressionMaximum;
		public double AudioCompressionMaximum => this.audioCompressionMaximum.Value;

		private ObservableAsPropertyHelper<double> audioCompressionGranularity;
		public double AudioCompressionGranularity => this.audioCompressionGranularity.Value;

		private ObservableAsPropertyHelper<string> audioCompressionToolTip;
		public string AudioCompressionToolTip => this.audioCompressionToolTip.Value;

		public List<MixdownViewModel> MixdownChoices
		{
			get
			{
				return this.mixdownChoices;
			}
		}

		public MixdownViewModel SelectedMixdown
		{
			get
			{
				return this.selectedMixdown;
			}

			set
			{
				this.selectedMixdown = value;
				this.RaisePropertyChanged();

				if (value != null)
				{
					this.RaiseAudioEncodingChanged();
					this.RefreshBitrateChoices();
				}
			}
		}

		public List<int> SampleRateChoices
		{
			get
			{
				return this.currentSampleRateChoices;
			}
		}

		/// <summary>
		/// Gets or sets the sample rate in Hz for this audio encoding.
		/// </summary>
		public int SampleRate
		{
			get
			{
				return this.sampleRate;
			}

			set
			{
				this.sampleRate = value;
				this.RaiseAudioPropertyChanged();
			}
		}

		public List<BitrateChoiceViewModel> BitrateChoices
		{
			get
			{
				return this.bitrateChoices;
			}

			set
			{
				this.bitrateChoices = value;
				this.RaisePropertyChanged();
			}
		}

		public BitrateChoiceViewModel SelectedBitrate
		{
			get
			{
				return this.selectedBitrate;
			}

			set
			{
				this.selectedBitrate = value;
				this.RaisePropertyChanged();

				if (value != null)
				{
					this.RaiseAudioEncodingChanged();
				}
			}
		}

		public int Gain
		{
			get
			{
				return this.gain;
			}

			set
			{
				this.gain = value;
				this.RaiseAudioPropertyChanged();
			}
		}

		public double Drc
		{
			get
			{
				return this.drc;
			}

			set
			{
				this.drc = value;
				this.RaiseAudioPropertyChanged();
			}
		}

		public bool DrcEnabled
		{
			get
			{
				if (!this.main.HasVideoSource)
				{
					return true;
				}

				if (this.SelectedAudioEncoder == null)
				{
					return true;
				}

				// Find if we're encoding a single track
				var track = this.GetTargetAudioTrack();

				// Can only gray out DRC if we're encoding exactly one track
				if (track != null)
				{
					int trackNumber = this.main.SelectedTitle.AudioList.IndexOf(track);
					if (!this.SelectedAudioEncoder.IsPassthrough && this.main.ScanInstance != null && this.main.ScanInstance.CanApplyDrc(trackNumber, this.SelectedAudioEncoder.Encoder, this.main.SelectedTitle.Index))
					{
						return true;
					}
					else
					{
						return false;
					}
				}

				return true;
			}
		}

		private bool passthroughIfPossible;
		public bool PassthroughIfPossible
		{
			get
			{
				return this.passthroughIfPossible;
			}

			set
			{
				this.passthroughIfPossible = value;
				this.RaiseAudioPropertyChanged();
			}
		}

		private ObservableAsPropertyHelper<bool> passthroughIfPossibleVisible;
		public bool PassthroughIfPossibleVisible => this.passthroughIfPossibleVisible.Value;

		public string Name
		{
			get
			{
				return this.name;
			}

			set
			{
				this.name = value;
				this.RaiseAudioPropertyChanged();
			}
		}

		public string SourceName
		{
			get
			{
				if (!this.main.HasVideoSource)
				{
					return string.Empty;
				}

				var track = this.GetTargetAudioTrack();
				if (track != null)
				{
					return track.Name ?? string.Empty;
				}

				return string.Empty;
			}
		}

		private void RefreshSourceName()
		{
			this.RaisePropertyChanged(nameof(this.SourceName));
		}

		public bool IsValid
		{
			get
			{
				HBAudioEncoder audioEncoder = this.HBAudioEncoder;
				if (audioEncoder == null)
				{
					return false;
				}

				return audioEncoder.IsPassthrough || this.SelectedMixdown != null && this.SelectedBitrate != null && this.SelectedAudioEncoder != null;
			}
		}

		private ReactiveCommand<Unit, Unit> removeAudioEncoding;
		public ReactiveCommand<Unit, Unit> RemoveAudioEncoding
		{
			get
			{
				return this.removeAudioEncoding ?? (this.removeAudioEncoding = ReactiveCommand.Create(() =>
				{
					this.audioPanelVM.RemoveAudioEncoding(this);
				}));
			}
		}

		public void SetChosenTracks(List<ChosenAudioTrack> chosenAudioTracks, SourceTitle selectedTitle)
		{
			DispatchUtilities.Invoke(() =>
			{
				int previousIndex = this.TargetStreamIndex;

				this.targetStreams.Clear();
				this.targetStreams.Add(new TargetStreamViewModel { Text = CommonRes.All });

				int shownStreams = Math.Max(previousIndex, chosenAudioTracks.Count);

				for (int i = 0; i < shownStreams; i++)
				{
					string details = null;
					if (i < chosenAudioTracks.Count && selectedTitle != null)
					{
						details = selectedTitle.AudioList[chosenAudioTracks[i].TrackNumber - 1].Description;
					}

					this.targetStreams.Add(
						new TargetStreamViewModel
					    {
							Text = string.Format(CommonRes.StreamChoice, (i + 1)), 
							TrackDetails = details
					    });
				}

				// Set to -1, then back to real index in order to force a refresh on the ComboBox
				this.targetStreamIndex = -1;
				this.RaisePropertyChanged(nameof(this.TargetStreamIndex));

				this.targetStreamIndex = previousIndex;
				this.RaisePropertyChanged(nameof(this.TargetStreamIndex));
			});
		}

		private void RaiseAudioPropertyChanged([CallerMemberName] string callerName = null)
		{
			this.RaisePropertyChanged(callerName);
			this.RaiseAudioEncodingChanged();
		}

		private void RefreshEncoderChoices(bool isContainerChange = false)
		{
			HBContainer container = HandBrakeEncoderHelpers.GetContainer(this.presetsService.SelectedPreset.Preset.EncodingProfile.ContainerName);
			AudioEncoderViewModel oldEncoder = null;
			string oldPassthrough = null;
			if (this.selectedAudioEncoder != null)
			{
				oldEncoder = this.selectedAudioEncoder;
				oldPassthrough = this.selectedPassthrough;
			}

			var resourceManager = new ResourceManager(typeof(EncodingRes));

			this.audioEncoders = new List<AudioEncoderViewModel>();
			//this.audioEncoders.Add(new AudioEncoderViewModel { IsPassthrough = true });

			this.passthroughChoices = new List<ComboChoice>();
			this.passthroughChoices.Add(new ComboChoice(AutoPassthroughEncoder, EncodingRes.Passthrough_auto));

			bool anyCodecSupportsPassthrough = false;

			foreach (HBAudioEncoder encoder in HandBrakeEncoderHelpers.AudioEncoders)
			{
				if ((encoder.CompatibleContainers & container.Id) > 0)
				{
					if (encoder.IsPassthrough)
					{
						if (encoder.ShortName.Contains(":"))
						{
							string inputCodec = encoder.ShortName.Split(':')[1];
							string display = resourceManager.GetString("Passthrough_" + inputCodec);

							if (string.IsNullOrEmpty(display))
							{
								display = encoder.DisplayName;
							}

							this.passthroughChoices.Add(new ComboChoice(encoder.ShortName, display));
						}

						anyCodecSupportsPassthrough = true;
					}
					else
					{
						this.AudioEncoders.Add(new AudioEncoderViewModel { Encoder = encoder });
					}
				}
			}

			if (anyCodecSupportsPassthrough)
			{
				this.audioEncoders.Insert(0, new AudioEncoderViewModel { IsPassthrough = true });
			}

			this.RaisePropertyChanged(nameof(this.PassthroughChoices));
			this.RaisePropertyChanged(nameof(this.AudioEncoders));

			if (oldEncoder == null)
			{
				this.selectedAudioEncoder = this.audioEncoders[1];
			}
			else
			{
				if (oldEncoder.IsPassthrough)
				{
					this.selectedAudioEncoder = this.audioEncoders[0];
					this.selectedPassthrough = oldPassthrough;
				}
				else
				{
					this.selectedAudioEncoder = this.audioEncoders.Skip(1).FirstOrDefault(e => e.Encoder == oldEncoder.Encoder);
					this.selectedPassthrough = "copy";
				}
			}

			if (this.selectedAudioEncoder == null)
			{
				this.selectedAudioEncoder = this.audioEncoders[1];
			}

			// If it's a container change we need to commit the new encoder change.
			if (isContainerChange && this.selectedAudioEncoder != oldEncoder)
			{
				this.RaiseAudioEncodingChanged();
			}

			this.RaisePropertyChanged(nameof(this.SelectedAudioEncoder));
			this.RaisePropertyChanged(nameof(this.SelectedPassthrough));
		}

		private void RefreshMixdownChoices()
		{
			HBMixdown oldMixdown = null;
			if (this.SelectedMixdown != null)
			{
				oldMixdown = this.SelectedMixdown.Mixdown;
			}

			this.mixdownChoices = new List<MixdownViewModel>();

			HBAudioEncoder hbAudioEncoder = this.HBAudioEncoder;
			foreach (HBMixdown mixdown in HandBrakeEncoderHelpers.Mixdowns)
			{
				// Only add option if codec supports the mixdown
				if (HandBrakeEncoderHelpers.MixdownHasCodecSupport(mixdown, hbAudioEncoder))
				{
					// Determine compatibility of mixdown with the input channel layout
					// Incompatible mixdowns are grayed out
					bool isCompatible = true;
					if (this.main.HasVideoSource)
					{
						SourceAudioTrack track = this.GetTargetAudioTrack();
						if (track != null)
						{
							isCompatible = HandBrakeEncoderHelpers.MixdownHasRemixSupport(mixdown, (ulong)track.ChannelLayout);
						}
					}

					this.MixdownChoices.Add(new MixdownViewModel { Mixdown = mixdown, IsCompatible = isCompatible });
				}
			}

			this.RaisePropertyChanged(nameof(this.MixdownChoices));

			this.SelectMixdown(oldMixdown);

			this.RaisePropertyChanged(nameof(this.SelectedMixdown));
		}

		private void RefreshBitrateChoices()
		{
			if (this.SelectedAudioEncoder == null || this.SelectedMixdown == null)
			{
				return;
			}

			int oldBitrate = 0;
			if (this.SelectedBitrate != null)
			{
				oldBitrate = this.SelectedBitrate.Bitrate;
			}

			this.bitrateChoices = new List<BitrateChoiceViewModel>();
			BitrateLimits bitrateLimits = null;

			// Determine if we should gray out "out of range" bitrates
			// Can only do this if a source is loaded
			if (this.main.HasVideoSource)
			{
				// Find if we're encoding a single track
				var track = this.GetTargetAudioTrack();

				// Can only gray out bitrates if we're encoding exactly one track
				if (track != null)
				{
					int sampleRateLimits = this.SampleRate;
					if (sampleRateLimits == 0)
					{
						sampleRateLimits = track.SampleRate;
					}

					HBMixdown mixdownLimits = this.SelectedMixdown.Mixdown;
					if (mixdownLimits.ShortName == "none" || string.IsNullOrEmpty(mixdownLimits.ShortName))
					{
						mixdownLimits = HandBrakeEncoderHelpers.SanitizeMixdown(mixdownLimits, this.HBAudioEncoder, (ulong)track.ChannelLayout);
					}

					bitrateLimits = HandBrakeEncoderHelpers.GetBitrateLimits(this.HBAudioEncoder, sampleRateLimits, mixdownLimits);
				}
			}

			BitrateLimits encoderBitrateLimits = CodecUtilities.GetAudioEncoderLimits(this.HBAudioEncoder);

			this.bitrateChoices.Add(new BitrateChoiceViewModel
			    {
					Bitrate = 0,
					IsCompatible = true
			    });

			foreach (int bitrateChoice in HandBrakeEncoderHelpers.AudioBitrates)
			{
				if (bitrateChoice >= encoderBitrateLimits.Low && bitrateChoice <= encoderBitrateLimits.High)
				{
					bool isCompatible = bitrateLimits == null || bitrateChoice >= bitrateLimits.Low && bitrateChoice <= bitrateLimits.High;

					this.bitrateChoices.Add(new BitrateChoiceViewModel
					{
						Bitrate = bitrateChoice,
						IsCompatible = isCompatible
					});
				}
			}

			this.RaisePropertyChanged(nameof(this.BitrateChoices));

			this.selectedBitrate = this.BitrateChoices.SingleOrDefault(b => b.Bitrate == oldBitrate);
			if (this.selectedBitrate == null)
			{
				this.selectedBitrate = this.BitrateChoices[0];
			}

			this.RaisePropertyChanged(nameof(this.SelectedBitrate));
		}

		private void RefreshSampleRateChoices()
		{
			if (this.SelectedAudioEncoder == null)
			{
				return;
			}

			// Many AC3 decoders do not support <32 kHz sample rate. For the AC3 encoder, we remove those
			// samplerate choices.
			int oldSampleRate = this.SampleRate;
			if (this.HBAudioEncoder.ShortName == "ac3")
			{
				this.currentSampleRateChoices = new List<int>();
				foreach (int sampleRateChoice in allSampleRateChoices)
				{
					if (sampleRateChoice == 0 || sampleRateChoice >= 32000)
					{
						this.currentSampleRateChoices.Add(sampleRateChoice);
					}
				}

				if (oldSampleRate == 0 || oldSampleRate >= 32000)
				{
					this.sampleRate = oldSampleRate;
				}
				else
				{
					this.sampleRate = 32000;
				}
			}
			else
			{
				this.currentSampleRateChoices = allSampleRateChoices;
				this.sampleRate = oldSampleRate;
			}

			this.RaisePropertyChanged(nameof(this.SampleRateChoices));
			this.RaisePropertyChanged(nameof(this.SampleRate));
		}

		private void RefreshDrc()
		{
			this.RaisePropertyChanged(nameof(this.DrcEnabled));
		}

		private SourceAudioTrack GetTargetAudioTrack()
		{
			if (this.main.SelectedTitle == null)
			{
				return null;
			}

			SourceAudioTrack track = null;
			List<ChosenAudioTrack> chosenAudioTracks = this.main.GetChosenAudioTracks();

			if (this.TargetStreamIndex > 0 && this.TargetStreamIndex <= chosenAudioTracks.Count)
			{
				int audioTrack = chosenAudioTracks[this.TargetStreamIndex - 1].TrackNumber;
				if (audioTrack <= this.main.SelectedTitle.AudioList.Count)
				{
					track = this.main.SelectedTitle.AudioList[audioTrack - 1];
				}
			}

			if (this.TargetStreamIndex == 0 && chosenAudioTracks.Count == 1)
			{
				int audioTrack = chosenAudioTracks[0].TrackNumber;
				if (audioTrack <= this.main.SelectedTitle.AudioList.Count)
				{
					track = this.main.SelectedTitle.AudioList[audioTrack - 1];
				}
			}

			return track;
		}

		// Tries to select the given mixdown. If it cannot, selects the last mixdown on the list.
		// Does not raise the propertychanged event.
		private void SelectMixdown(HBMixdown mixdown)
		{
			MixdownViewModel mixdownToSelect = this.MixdownChoices.FirstOrDefault(m => m.Mixdown == mixdown);
			if (mixdownToSelect != null)
			{
				this.selectedMixdown = mixdownToSelect;
			}
			else
			{
				this.selectedMixdown = this.MixdownChoices[this.MixdownChoices.Count - 1];
			}
		}

		private void RaiseAudioEncodingChanged()
		{
			if (this.IsValid && !this.initializing)
			{
				this.audioPanelVM.NotifyAudioEncodingChanged();
			}
		}
	}
}
