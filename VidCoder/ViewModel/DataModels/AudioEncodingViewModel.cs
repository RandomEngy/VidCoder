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
using VidCoder.Properties;
using VidCoder.Services;
using Microsoft.Practices.Unity;

namespace VidCoder.ViewModel
{
	public class AudioEncodingViewModel : ViewModelBase
	{
		private const int RangeRoundDigits = 5;

		private AudioPanelViewModel audioPanelVM;
		private bool initializing;

		private MainViewModel main = Unity.Container.Resolve<MainViewModel>();

		private ObservableCollection<TargetStreamViewModel> targetStreams;
		private int targetStreamIndex;
		private List<HBAudioEncoder> audioEncoders;
		private HBAudioEncoder selectedAudioEncoder;
		private List<HBMixdown> mixdownChoices;
		private HBMixdown selectedMixdown;
		private int sampleRate;
		private List<BitrateChoiceViewModel> bitrateChoices;
		private BitrateChoiceViewModel selectedBitrate;
		//private int bitrate;
		private int gain;
		private double drc;
		private string name;

		private ICommand removeAudioEncodingCommand;

		private Container outputFormat;

		private static List<int> sampleRateChoices = new List<int>
		{
			0,
			22050,
			24000,
			32000,
			44100,
			48000
		};

		public AudioEncodingViewModel(AudioEncoding audioEncoding, Title selectedTitle, List<int> chosenAudioTracks, Container outputFormat, AudioPanelViewModel audioPanelVM)
		{
			this.initializing = true;
			this.audioPanelVM = audioPanelVM;

			this.targetStreams = new ObservableCollection<TargetStreamViewModel>();
			this.targetStreamIndex = audioEncoding.InputNumber;

			this.SetChosenTracks(chosenAudioTracks, selectedTitle);

			this.audioEncoders = new List<HBAudioEncoder>();
			this.mixdownChoices = new List<HBMixdown>();

			this.outputFormat = outputFormat;
			this.RefreshEncoderChoices();
			this.RefreshMixdownChoices();
			this.RefreshBitrateChoices();

			this.selectedAudioEncoder = Encoders.GetAudioEncoder(audioEncoding.Encoder);
			this.selectedMixdown = Encoders.GetMixdown(audioEncoding.Mixdown);
			this.sampleRate = audioEncoding.SampleRateRaw;

			if (!this.SelectedAudioEncoder.SupportsQuality)
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
				this.audioCompression = this.SelectedAudioEncoder.DefaultCompression;
			}

			this.selectedBitrate = this.BitrateChoices.Single(b => b.Bitrate == audioEncoding.Bitrate);
			this.gain = audioEncoding.Gain;
			this.drc = audioEncoding.Drc;
			this.name = audioEncoding.Name;

			Messenger.Default.Register<SelectedTitleChangedMessage>(
				this,
				message =>
					{
						this.RefreshBitrateChoices();
					});

			Messenger.Default.Register<AudioInputChangedMessage>(
				this,
				message =>
					{
						this.RefreshBitrateChoices();
					});

			Messenger.Default.Register<OptionsChangedMessage>(
				this,
				message =>
					{
						this.RaisePropertyChanged("NameVisible");
					});

			this.initializing = false;
		}

		public AudioEncoding NewAudioEncoding
		{
			get
			{
				var newAudioEncoding = new AudioEncoding();
				newAudioEncoding.InputNumber = this.TargetStreamIndex;

				newAudioEncoding.Encoder = this.SelectedAudioEncoder.ShortName;

				if (!this.SelectedAudioEncoder.IsPassthrough)
				{
					newAudioEncoding.Mixdown = this.SelectedMixdown.ShortName;
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

					if (this.SelectedAudioEncoder.SupportsCompression)
					{
						newAudioEncoding.Compression = this.AudioCompression;
					}

					newAudioEncoding.Gain = this.Gain;
					newAudioEncoding.Drc = this.Drc;
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
					this.RaisePropertyChanged("TargetStreamIndex");
					this.audioPanelVM.NotifyAudioEncodingChanged();
					this.MarkModified();
				}
			}
		}

		public List<HBAudioEncoder> AudioEncoders
		{
			get
			{
				return this.audioEncoders;
			}
		}

		public HBAudioEncoder SelectedAudioEncoder
		{
			get
			{
				return this.selectedAudioEncoder;
			}

			set
			{
				this.selectedAudioEncoder = value;
				this.RaisePropertyChanged("SelectedAudioEncoder");
				this.RaisePropertyChanged("EncoderSettingsVisible");
				this.RaisePropertyChanged("BitrateVisible");
				this.RaisePropertyChanged("AudioQualityVisible");
				this.RaisePropertyChanged("AudioQualityRadioVisible");
				this.RaisePropertyChanged("AudioCompressionVisible");
				this.RaisePropertyChanged("BitrateRadioButtonVisible");
				this.RaisePropertyChanged("BitrateLabelVisible");
				this.RaisePropertyChanged("AudioQualityMinimum");
				this.RaisePropertyChanged("AudioQualityMaximum");
				this.RaisePropertyChanged("AudioQualityGranularity");
				this.RaisePropertyChanged("AudioQualityToolTip");
				this.RaisePropertyChanged("AudioCompressionMinimum");
				this.RaisePropertyChanged("AudioCompressionMaximum");
				this.RaisePropertyChanged("AudioCompressionGranularity");
				this.RaisePropertyChanged("AudioCompressionToolTip");
				this.MarkModified();

				if (value != null)
				{
					this.RefreshMixdownChoices();
					this.RefreshBitrateChoices();
					if (!value.IsPassthrough)
					{
						if (this.SelectedBitrate == null && this.BitrateChoices.Count > 0)
						{
							this.SelectedBitrate = this.BitrateChoices[0];
						}
					}

					this.audioPanelVM.RefreshExtensionChoice();

					// Set encode rate type to Bitrate if quality is not supported.
					if (!value.IsPassthrough && !value.SupportsQuality)
					{
						this.encodeRateType = AudioEncodeRateType.Bitrate;
						this.RaisePropertyChanged("EncodeRateType");
					}

					// On encoder switch set default quality/compression if supported.
					if (value.SupportsQuality)
					{
						this.audioQuality = value.DefaultQuality;
						this.RaisePropertyChanged("AudioQuality");
					}

					if (value.SupportsCompression)
					{
						this.audioCompression = value.DefaultCompression;
						this.RaisePropertyChanged("AudioCompression");
					}

					this.RaiseAudioEncodingChanged();
				}
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

				return !this.SelectedAudioEncoder.IsPassthrough;
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
				this.RaisePropertyChanged("EncodeRateType");
				this.RaisePropertyChanged("BitrateVisible");
				this.RaisePropertyChanged("AudioQualityVisible");

				// Set default quality when switching to quality
				if (value == AudioEncodeRateType.Quality)
				{
					this.audioQuality = this.SelectedAudioEncoder.DefaultQuality;
					this.RaisePropertyChanged("AudioQuality");
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
					BitrateLimits bitrateLimits = Encoders.GetBitrateLimits(this.SelectedAudioEncoder, 48000, Encoders.GetMixdown("dpl2"));
					return bitrateLimits.High > 0;
				}

				return false;
			}
		}

		public bool BitrateLabelVisible
		{
			get
			{
				return this.BitrateVisible && !this.SelectedAudioEncoder.SupportsQuality;
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
				this.RaisePropertyChanged("AudioQuality");
				this.RaiseAudioEncodingChanged();
			}
		}

		public bool AudioQualityVisible
		{
			get
			{
				return this.SelectedAudioEncoder.SupportsQuality && this.EncodeRateType == AudioEncodeRateType.Quality;
			}
		}

		public bool AudioQualityRadioVisible
		{
			get
			{
				return this.SelectedAudioEncoder.SupportsQuality;
			}
		}

		public double AudioQualityMinimum
		{
			get
			{
				return Math.Round(this.SelectedAudioEncoder.QualityLimits.Low, RangeRoundDigits);
			}
		}

		public double AudioQualityMaximum
		{
			get
			{
				return Math.Round(this.SelectedAudioEncoder.QualityLimits.High, RangeRoundDigits);
			}
		}

		public double AudioQualityGranularity
		{
			get
			{
				return Math.Round(this.SelectedAudioEncoder.QualityLimits.Granularity, RangeRoundDigits);
			}
		}

		public string AudioQualityToolTip
		{
			get
			{
				string directionSentence;
				if (this.SelectedAudioEncoder.QualityLimits.Ascending)
				{
					directionSentence = "High values mean high quality.";
				}
				else
				{
					directionSentence = "Low values mean high quality.";
				}

				return string.Format(
					"The audio quality target. {0} Valid values are from {1} to {2}.",
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
				this.RaisePropertyChanged("AudioCompression");
				this.RaiseAudioEncodingChanged();
			}
		}

		public bool AudioCompressionVisible
		{
			get
			{
				return this.SelectedAudioEncoder.SupportsCompression;
			}
		}

		public double AudioCompressionMinimum
		{
			get
			{
				return Math.Round(this.SelectedAudioEncoder.CompressionLimits.Low, RangeRoundDigits);
			}
		}

		public double AudioCompressionMaximum
		{
			get
			{
				return Math.Round(this.SelectedAudioEncoder.CompressionLimits.High, RangeRoundDigits);
			}
		}

		public double AudioCompressionGranularity
		{
			get
			{
				return Math.Round(this.SelectedAudioEncoder.CompressionLimits.Granularity, RangeRoundDigits);
			}
		}

		public string AudioCompressionToolTip
		{
			get
			{
				string directionSentence;
				if (this.SelectedAudioEncoder.QualityLimits.Ascending)
				{
					directionSentence = "High values mean high compression: better quality/smaller size but longer encode times.";
				}
				else
				{
					directionSentence = "Low values mean high compression: better quality/smaller size but longer encode times.";
				}

				return string.Format(
					"The amount of compression to apply. {0} Valid values are from {1} to {2}.",
					directionSentence,
					this.AudioCompressionMinimum,
					this.AudioCompressionMaximum);
			}
		}

		public bool NameVisible
		{
			get
			{
				return Settings.Default.ShowAudioTrackNameField;
			}
		}

		public List<HBMixdown> MixdownChoices
		{
			get
			{
				return this.mixdownChoices;
			}
		}

		public HBMixdown SelectedMixdown
		{
			get
			{
				return this.selectedMixdown;
			}

			set
			{
				this.selectedMixdown = value;
				this.RaisePropertyChanged("SelectedMixdown");

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
				return sampleRateChoices;
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
				this.RaisePropertyChanged("SampleRate");
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
				this.RaisePropertyChanged("BitrateChoices");
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
				this.RaisePropertyChanged("SelectedBitrate");

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
				this.RaisePropertyChanged("Gain");
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
				this.RaisePropertyChanged("Drc");
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
				this.RaisePropertyChanged("Name");
				this.audioPanelVM.NotifyAudioEncodingChanged();
				this.MarkModified();
			}
		}

		public Container OutputFormat
		{
			get
			{
				return this.outputFormat;
			}

			set
			{
				this.outputFormat = value;
				this.RefreshEncoderChoices();
			}
		}

		public bool IsValid
		{
			get
			{
				return this.SelectedMixdown != null && this.SelectedBitrate != null && this.SelectedAudioEncoder != null;
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
				this.targetStreams.Add(new TargetStreamViewModel { Text = "All" });

				int shownStreams = Math.Max(previousIndex, chosenAudioTracks.Count);

				for (int i = 0; i < shownStreams; i++)
				{
					string details = null;
					if (i < chosenAudioTracks.Count && selectedTitle != null)
					{
						details = selectedTitle.AudioTracks[chosenAudioTracks[i] - 1].NoTrackDisplay;
					}

					this.targetStreams.Add(new TargetStreamViewModel { Text = "Stream " + (i + 1), TrackDetails = details });
				}

				// Set to -1, then back to real index in order to force a refresh on the ComboBox
				this.targetStreamIndex = -1;
				this.RaisePropertyChanged("TargetStreamIndex");

				this.targetStreamIndex = previousIndex;
				this.RaisePropertyChanged("TargetStreamIndex");
			});
		}

		private void RefreshEncoderChoices(bool signalRefresh = true)
		{
			HBAudioEncoder oldEncoder = this.selectedAudioEncoder;

			this.audioEncoders = new List<HBAudioEncoder>();

			foreach (HBAudioEncoder encoder in Encoders.AudioEncoders)
			{
				if ((encoder.CompatibleContainers & this.OutputFormat) > 0)
				{
					this.AudioEncoders.Add(encoder);
				}
			}

			this.RaisePropertyChanged("AudioEncoders");

			if (this.AudioEncoders.Contains(oldEncoder))
			{
				this.selectedAudioEncoder = oldEncoder;
			}
			else
			{
				this.selectedAudioEncoder = this.AudioEncoders[0];
			}

			this.RaisePropertyChanged("SelectedAudioEncoder");
		}

		private void RefreshMixdownChoices()
		{
			HBMixdown oldMixdown = this.SelectedMixdown;
			this.mixdownChoices = new List<HBMixdown>();

			int maxMixdownIndex = Encoders.GetMaxMixdownIndex(this.SelectedAudioEncoder);
			for (int i = 0; i <= maxMixdownIndex; i++)
			{
				this.MixdownChoices.Add(Encoders.Mixdowns[i]);
			}

			this.RaisePropertyChanged("MixdownChoices");

			if (this.MixdownChoices.Contains(oldMixdown))
			{
				this.selectedMixdown = oldMixdown;
			}
			else
			{
				this.selectedMixdown = this.MixdownChoices[0];
			}

			this.RaisePropertyChanged("SelectedMixdown");
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

				// Can only gray out bitrates if we're encoding exactly one track
				if (track != null)
				{
					int sampleRateLimits = this.SampleRate;
					if (sampleRateLimits == 0)
					{
						sampleRateLimits = track.SampleRate;
					}

					HBMixdown mixdownLimits = this.SelectedMixdown;
					if (mixdownLimits.ShortName == "none" || string.IsNullOrEmpty(mixdownLimits.ShortName))
					{
						mixdownLimits = Encoders.SanitizeMixdown(mixdownLimits, this.SelectedAudioEncoder, track.ChannelLayout);
					}

					bitrateLimits = Encoders.GetBitrateLimits(this.SelectedAudioEncoder, sampleRateLimits, mixdownLimits);
				}
			}

			this.bitrateChoices.Add(new BitrateChoiceViewModel
			    {
					Bitrate = 0,
					IsCompatible = true
			    });

			foreach (int bitrateChoice in Encoders.AudioBitrates)
			{
				bool isCompatible = bitrateLimits == null || bitrateChoice >= bitrateLimits.Low && bitrateChoice <= bitrateLimits.High;

				this.bitrateChoices.Add(new BitrateChoiceViewModel
				    {
				        Bitrate = bitrateChoice,
						IsCompatible = isCompatible
				    });
			}

			this.RaisePropertyChanged("BitrateChoices");

			this.selectedBitrate = this.BitrateChoices.Single(b => b.Bitrate == oldBitrate);
			this.RaisePropertyChanged("SelectedBitrate");
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
	}
}
