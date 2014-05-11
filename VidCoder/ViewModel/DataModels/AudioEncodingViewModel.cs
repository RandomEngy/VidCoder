using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;
using HandBrake.Interop;
using HandBrake.Interop.Model;
using HandBrake.Interop.Model.Encoding;
using HandBrake.Interop.SourceData;
using System.Collections.ObjectModel;
using System.Windows.Input;
using VidCoder.Messages;
using VidCoder.Services;

namespace VidCoder.ViewModel
{
	using System.Resources;
	using System.Windows.Media;
	using Model;
	using Resources;
	using Brush = System.Windows.Media.Brush;

	public class AudioEncodingViewModel : ViewModelBase
	{
		private const int RangeRoundDigits = 5;

		private AudioPanelViewModel audioPanelVM;
		private bool initializing;

		private MainViewModel main = Ioc.Container.GetInstance<MainViewModel>();

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

		private ICommand removeAudioEncodingCommand;

		private string containerName;

		private static List<int> allSampleRateChoices;

		private List<int> currentSampleRateChoices;

		static AudioEncodingViewModel()
		{
			allSampleRateChoices = new List<int> { 0 };
			foreach (var sampleRate in Encoders.AudioSampleRates)
			{
				allSampleRateChoices.Add(sampleRate.Rate);
			}
		}

		public AudioEncodingViewModel(AudioEncoding audioEncoding, Title selectedTitle, List<int> chosenAudioTracks, string containerName, AudioPanelViewModel audioPanelVM)
		{
			this.initializing = true;
			this.audioPanelVM = audioPanelVM;

			this.targetStreams = new ObservableCollection<TargetStreamViewModel>();
			this.targetStreamIndex = audioEncoding.InputNumber;

			this.SetChosenTracks(chosenAudioTracks, selectedTitle);

			this.audioEncoders = new List<AudioEncoderViewModel>();
			this.mixdownChoices = new List<MixdownViewModel>();

			this.containerName = containerName;
			this.RefreshEncoderChoices();

			HBAudioEncoder hbAudioEncoder = Encoders.GetAudioEncoder(audioEncoding.Encoder);
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

			this.SelectMixdown(Encoders.GetMixdown(audioEncoding.Mixdown));

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

			Messenger.Default.Register<SelectedTitleChangedMessage>(
				this,
				message =>
					{
						this.RefreshMixdownChoices();
						this.RefreshBitrateChoices();
						this.RefreshDrc();
					});

			Messenger.Default.Register<AudioInputChangedMessage>(
				this,
				message =>
					{
						this.RefreshMixdownChoices();
						this.RefreshBitrateChoices();
						this.RefreshDrc();
					});

			Messenger.Default.Register<OptionsChangedMessage>(
				this,
				message =>
					{
						this.RaisePropertyChanged(() => this.NameVisible);
					});

			Messenger.Default.Register<ContainerChangedMessage>(
				this,
				message =>
					{
						this.containerName = message.ContainerName;
						this.RefreshEncoderChoices();
					});

			this.initializing = false;
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
					newAudioEncoding.Name = this.Name;
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
					this.RaisePropertyChanged(() => this.TargetStreamIndex);
					this.audioPanelVM.NotifyAudioEncodingChanged();
					this.MarkModified();
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

				this.RaisePropertyChanged(() => this.SelectedAudioEncoder);
				this.RaisePropertyChanged(() => this.EncoderSettingsVisible);
				this.RaisePropertyChanged(() => this.PassthroughChoicesVisible);
				this.RaisePropertyChanged(() => this.AutoPassthroughSettingsVisible);
				this.RaisePropertyChanged(() => this.BitrateVisible);
				this.RaisePropertyChanged(() => this.AudioQualityVisible);
				this.RaisePropertyChanged(() => this.AudioQualityRadioVisible);
				this.RaisePropertyChanged(() => this.AudioCompressionVisible);
				this.RaisePropertyChanged(() => this.BitrateLabelVisible);
				this.RaisePropertyChanged(() => this.AudioQualityMinimum);
				this.RaisePropertyChanged(() => this.AudioQualityMaximum);
				this.RaisePropertyChanged(() => this.AudioQualityGranularity);
				this.RaisePropertyChanged(() => this.AudioQualityToolTip);
				this.RaisePropertyChanged(() => this.AudioCompressionMinimum);
				this.RaisePropertyChanged(() => this.AudioCompressionMaximum);
				this.RaisePropertyChanged(() => this.AudioCompressionGranularity);
				this.RaisePropertyChanged(() => this.AudioCompressionToolTip);
				this.MarkModified();

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
						this.RaiseEncodeRateTypeChanged();
					}

					// On encoder switch set default quality/compression if supported.
					if (value.Encoder.SupportsQuality)
					{
						this.audioQuality = value.Encoder.DefaultQuality;
						this.RaisePropertyChanged(() => this.AudioQuality);
					}

					if (value.Encoder.SupportsCompression)
					{
						this.audioCompression = value.Encoder.DefaultCompression;
						this.RaisePropertyChanged(() => this.AudioCompression);
					}
				}

				this.RaiseAudioEncodingChanged();
			}
		}

		public HBAudioEncoder HBAudioEncoder
		{
			get
			{
				if (this.SelectedAudioEncoder == null)
				{
					return null;
				}

				if (this.SelectedAudioEncoder.IsPassthrough)
				{
					return Encoders.GetAudioEncoder(this.SelectedPassthrough);
				}

				return this.SelectedAudioEncoder.Encoder;
			}
		}

		public bool EncoderSettingsVisible
		{
			get
			{
				if (this.SelectedAudioEncoder == null)
				{
					return false;
				}

				return !this.HBAudioEncoder.IsPassthrough;
			}
		}

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
				this.RaisePropertyChanged(() => this.SelectedPassthrough);
				this.RaisePropertyChanged(() => this.AutoPassthroughSettingsVisible);

				this.RaiseAudioEncodingChanged();
			}
		}

		public bool PassthroughChoicesVisible
		{
			get
			{
				return this.selectedAudioEncoder.IsPassthrough;
			}
		}

		public bool AutoPassthroughSettingsVisible
		{
			get
			{
				if (this.SelectedAudioEncoder == null)
				{
					return false;
				}

				return this.HBAudioEncoder.ShortName == "copy";
			}
		}

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
				this.RaiseEncodeRateTypeChanged();

				// Set default quality when switching to quality
				if (value == AudioEncodeRateType.Quality)
				{
					this.audioQuality = this.HBAudioEncoder.DefaultQuality;
					this.RaisePropertyChanged(() => this.AudioQuality);
				}

				this.RaiseAudioEncodingChanged();
			}
		}

		public bool BitrateVisible
		{
			get
			{
				if (this.EncoderSettingsVisible && this.SelectedMixdown != null && this.EncodeRateType == AudioEncodeRateType.Bitrate)
				{
					// We only need to find out if the bitrate limits exist, so pass in some normal values for sample rate and mixdown.
					BitrateLimits bitrateLimits = Encoders.GetBitrateLimits(this.HBAudioEncoder, 48000, Encoders.GetMixdown("dpl2"));
					return bitrateLimits.High > 0;
				}

				return false;
			}
		}

		public bool BitrateLabelVisible
		{
			get
			{
				return this.BitrateVisible && !this.HBAudioEncoder.SupportsQuality;
			}
		}

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
				this.RaisePropertyChanged(() => this.AudioQuality);
				this.RaiseAudioEncodingChanged();
			}
		}

		public bool AudioQualityVisible
		{
			get
			{
				return this.HBAudioEncoder.SupportsQuality && this.EncodeRateType == AudioEncodeRateType.Quality;
			}
		}

		public bool AudioQualityRadioVisible
		{
			get
			{
				return this.HBAudioEncoder.SupportsQuality;
			}
		}

		public double AudioQualityMinimum
		{
			get
			{
				return Math.Round(this.HBAudioEncoder.QualityLimits.Low, RangeRoundDigits);
			}
		}

		public double AudioQualityMaximum
		{
			get
			{
				return Math.Round(this.HBAudioEncoder.QualityLimits.High, RangeRoundDigits);
			}
		}

		public double AudioQualityGranularity
		{
			get
			{
				return Math.Round(this.HBAudioEncoder.QualityLimits.Granularity, RangeRoundDigits);
			}
		}

		public string AudioQualityToolTip
		{
			get
			{
				string directionSentence;
				if (this.HBAudioEncoder.QualityLimits.Ascending)
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
					this.AudioQualityMinimum,
					this.AudioQualityMaximum);
			}
		}

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
				this.RaisePropertyChanged(() => this.AudioCompression);
				this.RaiseAudioEncodingChanged();
			}
		}

		public bool AudioCompressionVisible
		{
			get
			{
				return this.HBAudioEncoder.SupportsCompression;
			}
		}

		public double AudioCompressionMinimum
		{
			get
			{
				return Math.Round(this.HBAudioEncoder.CompressionLimits.Low, RangeRoundDigits);
			}
		}

		public double AudioCompressionMaximum
		{
			get
			{
				return Math.Round(this.HBAudioEncoder.CompressionLimits.High, RangeRoundDigits);
			}
		}

		public double AudioCompressionGranularity
		{
			get
			{
				return Math.Round(this.HBAudioEncoder.CompressionLimits.Granularity, RangeRoundDigits);
			}
		}

		public string AudioCompressionToolTip
		{
			get
			{
				string directionSentence;
				if (this.HBAudioEncoder.QualityLimits.Ascending)
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
					this.AudioCompressionMinimum,
					this.AudioCompressionMaximum);
			}
		}

		public bool NameVisible
		{
			get
			{
				return Config.ShowAudioTrackNameField;
			}
		}

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
				this.RaisePropertyChanged(() => this.SelectedMixdown);

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
				this.RaisePropertyChanged(() => this.SampleRate);
				this.RaiseAudioEncodingChanged();
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
				this.RaisePropertyChanged(() => this.BitrateChoices);
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
				this.RaisePropertyChanged(() => this.SelectedBitrate);

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
				this.RaisePropertyChanged(() => this.Gain);
				this.RaiseAudioEncodingChanged();
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
				this.RaisePropertyChanged(() => this.Drc);
				this.audioPanelVM.NotifyAudioEncodingChanged();
				this.MarkModified();
			}
		}

		public Brush DrcBrush
		{
			get
			{
				Brush disabledBrush = Brushes.Gray;
				Brush enabledBrush = Brushes.Black;

				if (!this.main.HasVideoSource)
				{
					return enabledBrush;
				}

				if (this.SelectedAudioEncoder == null)
				{
					return enabledBrush;
				}

				// Find if we're encoding a single track
				var track = this.GetTargetAudioTrack();

				// Can only gray out DRC if we're encoding exactly one track
				if (track != null)
				{
					if (Encoders.CanApplyDrc(track, this.SelectedAudioEncoder.Encoder))
					{
						return enabledBrush;
					}
					else
					{
						return disabledBrush;
					}
				}

				return enabledBrush;
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
				this.RaisePropertyChanged(() => this.PassthroughIfPossible);
				this.audioPanelVM.NotifyAudioEncodingChanged();
				this.MarkModified();
			}
		}

		public string Name
		{
			get
			{
				return this.name;
			}

			set
			{
				this.name = value;
				this.RaisePropertyChanged(() => this.Name);
				this.audioPanelVM.NotifyAudioEncodingChanged();
				this.MarkModified();
			}
		}

		public bool IsValid
		{
			get
			{
				return this.HBAudioEncoder.IsPassthrough || this.SelectedMixdown != null && this.SelectedBitrate != null && this.SelectedAudioEncoder != null;
			}
		}

		public ICommand RemoveAudioEncodingCommand
		{
			get
			{
				if (this.removeAudioEncodingCommand == null)
				{
					this.removeAudioEncodingCommand = new RelayCommand(() =>
					{
						this.audioPanelVM.RemoveAudioEncoding(this);
					});
				}

				return this.removeAudioEncodingCommand;
			}
		}

		public void SetChosenTracks(List<int> chosenAudioTracks, Title selectedTitle)
		{
			DispatchService.Invoke(() =>
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
						details = selectedTitle.AudioTracks[chosenAudioTracks[i] - 1].NoTrackDisplay;
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
				this.RaisePropertyChanged(() => this.TargetStreamIndex);

				this.targetStreamIndex = previousIndex;
				this.RaisePropertyChanged(() => this.TargetStreamIndex);
			});
		}

		private void RefreshEncoderChoices()
		{
			HBContainer container = Encoders.GetContainer(this.containerName);
			AudioEncoderViewModel oldEncoder = null;
			string oldPassthrough = null;
			if (this.selectedAudioEncoder != null)
			{
				oldEncoder = this.selectedAudioEncoder;
				oldPassthrough = this.selectedPassthrough;
			}

			var resourceManager = new ResourceManager(typeof(EncodingRes));

			this.audioEncoders = new List<AudioEncoderViewModel>();
			this.audioEncoders.Add(new AudioEncoderViewModel { IsPassthrough = true });

			this.passthroughChoices = new List<ComboChoice>();
			this.passthroughChoices.Add(new ComboChoice("copy", EncodingRes.Passthrough_any));

			foreach (HBAudioEncoder encoder in Encoders.AudioEncoders)
			{
				if ((encoder.CompatibleContainers & container.Id) > 0)
				{
					if (encoder.IsPassthrough)
					{
						if (encoder.ShortName.Contains(":"))
						{
							string inputCodec = encoder.ShortName.Split(':')[1];
							string display = resourceManager.GetString("Passthrough_" + inputCodec);

							this.passthroughChoices.Add(new ComboChoice(encoder.ShortName, display));
						}
					}
					else
					{
						this.AudioEncoders.Add(new AudioEncoderViewModel { Encoder = encoder });
					}
				}
			}

			this.RaisePropertyChanged(() => this.AudioEncoders);
			this.RaisePropertyChanged(() => this.PassthroughChoices);

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

			this.RaisePropertyChanged(() => this.SelectedAudioEncoder);
			this.RaisePropertyChanged(() => this.SelectedPassthrough);
			this.RaisePropertyChanged(() => this.AutoPassthroughSettingsVisible);
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
			foreach (HBMixdown mixdown in Encoders.Mixdowns)
			{
				// Only add option if codec supports the mixdown
				if (Encoders.MixdownHasCodecSupport(mixdown, hbAudioEncoder))
				{
					// Determine compatibility of mixdown with the input channel layout
					// Incompatible mixdowns are grayed out
					bool isCompatible = true;
					if (this.main.HasVideoSource)
					{
						AudioTrack track = this.GetTargetAudioTrack();
						if (track != null)
						{
							isCompatible = Encoders.MixdownHasRemixSupport(mixdown, track.ChannelLayout);
						}
					}

					this.MixdownChoices.Add(new MixdownViewModel { Mixdown = mixdown, IsCompatible = isCompatible });
				}
			}

			this.RaisePropertyChanged(() => this.MixdownChoices);

			this.SelectMixdown(oldMixdown);

			this.RaisePropertyChanged(() => this.SelectedMixdown);
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
						mixdownLimits = Encoders.SanitizeMixdown(mixdownLimits, this.HBAudioEncoder, track.ChannelLayout);
					}

					bitrateLimits = Encoders.GetBitrateLimits(this.HBAudioEncoder, sampleRateLimits, mixdownLimits);
				}
			}

			BitrateLimits encoderBitrateLimits = CodecUtilities.GetAudioEncoderLimits(this.HBAudioEncoder);

			this.bitrateChoices.Add(new BitrateChoiceViewModel
			    {
					Bitrate = 0,
					IsCompatible = true
			    });

			foreach (int bitrateChoice in Encoders.AudioBitrates)
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

			this.RaisePropertyChanged(() => this.BitrateChoices);

			this.selectedBitrate = this.BitrateChoices.SingleOrDefault(b => b.Bitrate == oldBitrate);
			if (this.selectedBitrate == null)
			{
				this.selectedBitrate = this.BitrateChoices[0];
			}

			//this.selectedBitrate = this.BitrateChoices.Single(b => b.Bitrate == oldBitrate);
			this.RaisePropertyChanged(() => this.SelectedBitrate);
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

			this.RaisePropertyChanged(() => this.SampleRateChoices);
			this.RaisePropertyChanged(() => this.SampleRate);
		}

		private void RefreshDrc()
		{
			this.RaisePropertyChanged(() => this.DrcBrush);
		}

		private AudioTrack GetTargetAudioTrack()
		{
			AudioTrack track = null;
			List<int> chosenAudioTracks = this.main.GetChosenAudioTracks();

			if (this.TargetStreamIndex > 0 && this.TargetStreamIndex <= chosenAudioTracks.Count)
			{
				int audioTrack = chosenAudioTracks[this.TargetStreamIndex - 1];
				if (audioTrack <= this.main.SelectedTitle.AudioTracks.Count)
				{
					track = this.main.SelectedTitle.AudioTracks[audioTrack - 1];
				}
			}

			if (this.TargetStreamIndex == 0 && chosenAudioTracks.Count == 1)
			{
				int audioTrack = chosenAudioTracks[0];
				if (audioTrack <= this.main.SelectedTitle.AudioTracks.Count)
				{
					track = this.main.SelectedTitle.AudioTracks[audioTrack - 1];
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

		private void MarkModified()
		{
			if (!this.initializing)
			{
				this.audioPanelVM.IsModified = true;
			}
		}

		private void RaiseAudioEncodingChanged()
		{
			if (this.IsValid)
			{
				this.audioPanelVM.NotifyAudioEncodingChanged();
				this.MarkModified();
			}
		}

		private void RaiseEncodeRateTypeChanged()
		{
			this.RaisePropertyChanged(() => this.EncodeRateType);
			this.RaisePropertyChanged(() => this.BitrateVisible);
			this.RaisePropertyChanged(() => this.BitrateLabelVisible);
			this.RaisePropertyChanged(() => this.AudioQualityVisible);
		}
	}
}
