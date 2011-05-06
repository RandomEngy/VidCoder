using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VidCoder.Model;
using HandBrake.SourceData;
using System.Collections.ObjectModel;
using System.Windows.Input;
using VidCoder.Services;
using HandBrake.Interop;

namespace VidCoder.ViewModel
{
	public class AudioEncodingViewModel : ViewModelBase
	{
		private EncodingViewModel encodingDialogVM;
		private bool initializing;

		private AudioEncoding audioEncoding;
		private ObservableCollection<TargetStreamViewModel> targetStreams;
		private int targetStreamIndex;
		private ObservableCollection<AudioEncoderViewModel> audioEncoders;
		private AudioEncoderViewModel selectedAudioEncoder;
		private ObservableCollection<MixdownViewModel> mixdownChoices;
		private MixdownViewModel selectedMixdown;
		private int sampleRate;
		private List<int> bitrateChoices;
		private int bitrate;
		private int gain;
		private double drc;
		private string name;

		private ICommand removeAudioEncodingCommand;

		private OutputFormat outputFormat;

		private static List<int> sampleRateChoices = new List<int>
		{
			0,
			22050,
			24000,
			32000,
			44100,
			48000
		};

		public AudioEncodingViewModel(AudioEncoding audioEncoding, Title selectedTitle, List<int> chosenAudioTracks, OutputFormat outputFormat, EncodingViewModel encodingDialogVM)
		{
			this.initializing = true;
			this.encodingDialogVM = encodingDialogVM;

			this.audioEncoding = audioEncoding;
			this.targetStreams = new ObservableCollection<TargetStreamViewModel>();
			this.targetStreamIndex = audioEncoding.InputNumber;

			this.SetChosenTracks(chosenAudioTracks, selectedTitle);

			this.audioEncoders = new ObservableCollection<AudioEncoderViewModel>();
			this.mixdownChoices = new ObservableCollection<MixdownViewModel>();

			this.OutputFormat = outputFormat;
			this.SetAudioEncoder(audioEncoding.Encoder);
			this.SetMixdown(audioEncoding.Mixdown);
			this.sampleRate = audioEncoding.SampleRateRaw;
			this.bitrate = audioEncoding.Bitrate;
			this.gain = audioEncoding.Gain;
			this.drc = audioEncoding.Drc;
			this.name = audioEncoding.Name;

			this.initializing = false;
		}

		public AudioEncoding NewAudioEncoding
		{
			get
			{
				var newAudioEncoding = new AudioEncoding();
				newAudioEncoding.InputNumber = this.TargetStreamIndex;

				AudioEncoder encoder = this.SelectedAudioEncoder.Encoder;
				newAudioEncoding.Encoder = encoder;

				if (!Utilities.IsPassthrough(encoder))
				{
					newAudioEncoding.Mixdown = this.SelectedMixdown.Mixdown;
					newAudioEncoding.Bitrate = this.Bitrate;
					newAudioEncoding.SampleRateRaw = this.SampleRate;
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
					this.NotifyPropertyChanged("TargetStreamIndex");
					this.encodingDialogVM.NotifyAudioEncodingChanged();
					this.MarkModified();
				}
			}
		}

		public ObservableCollection<AudioEncoderViewModel> AudioEncoders
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
				this.NotifyPropertyChanged("SelectedAudioEncoder");
				this.NotifyPropertyChanged("EncoderSettingsVisible");
				this.MarkModified();

				if (value != null)
				{
					this.RefreshMixdownChoices();
					this.RefreshBitrateChoices();
					if (!Utilities.IsPassthrough(value.Encoder))
					{
						if (this.Bitrate == 0)
						{
							this.Bitrate = 160;
						}
					}

					this.encodingDialogVM.RefreshExtensionChoice();
					this.encodingDialogVM.NotifyAudioEncodingChanged();
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

				return !Utilities.IsPassthrough(this.SelectedAudioEncoder.Encoder);
			}
		}

		public ObservableCollection<MixdownViewModel> MixdownChoices
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
				this.NotifyPropertyChanged("SelectedMixdown");

				if (value != null)
				{
					this.encodingDialogVM.NotifyAudioEncodingChanged();
					this.RefreshBitrateChoices();
					this.MarkModified();
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
				this.NotifyPropertyChanged("SampleRate");
				this.encodingDialogVM.NotifyAudioEncodingChanged();
				this.MarkModified();
			}
		}

		public List<int> BitrateChoices
		{
			get
			{
				return this.bitrateChoices;
			}

			set
			{
				this.bitrateChoices = value;
				this.NotifyPropertyChanged("BitrateChoices");
			}
		}

		public int Bitrate
		{
			get
			{
				return this.bitrate;
			}

			set
			{
				this.bitrate = value;
				this.NotifyPropertyChanged("Bitrate");
				this.encodingDialogVM.NotifyAudioEncodingChanged();
				this.MarkModified();
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
				this.NotifyPropertyChanged("Gain");
				this.encodingDialogVM.NotifyAudioEncodingChanged();
				this.MarkModified();
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
				this.NotifyPropertyChanged("Drc");
				this.encodingDialogVM.NotifyAudioEncodingChanged();
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
				this.NotifyPropertyChanged("Name");
				this.encodingDialogVM.NotifyAudioEncodingChanged();
				this.MarkModified();
			}
		}

		public OutputFormat OutputFormat
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

		public ICommand RemoveAudioEncodingCommand
		{
			get
			{
				if (this.removeAudioEncodingCommand == null)
				{
					this.removeAudioEncodingCommand = new RelayCommand(param =>
					{
						this.encodingDialogVM.RemoveAudioEncoding(this);
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
				this.NotifyPropertyChanged("TargetStreamIndex");

				this.targetStreamIndex = previousIndex;
				this.NotifyPropertyChanged("TargetStreamIndex");
			});
		}

		private void RefreshEncoderChoices()
		{
			AudioEncoderViewModel lastSelectedEncoder = this.SelectedAudioEncoder;
			this.AudioEncoders.Clear();

			if (this.OutputFormat == OutputFormat.Mkv)
			{
				this.AudioEncoders.Add(new AudioEncoderViewModel { Encoder = AudioEncoder.Faac });
				this.AudioEncoders.Add(new AudioEncoderViewModel { Encoder = AudioEncoder.Lame });
				this.AudioEncoders.Add(new AudioEncoderViewModel { Encoder = AudioEncoder.Ac3 });
				this.AudioEncoders.Add(new AudioEncoderViewModel { Encoder = AudioEncoder.Vorbis });
				this.AudioEncoders.Add(new AudioEncoderViewModel { Encoder = AudioEncoder.Passthrough });
			}
			else
			{
				this.AudioEncoders.Add(new AudioEncoderViewModel { Encoder = AudioEncoder.Faac });
				this.AudioEncoders.Add(new AudioEncoderViewModel { Encoder = AudioEncoder.Lame });
				this.AudioEncoders.Add(new AudioEncoderViewModel { Encoder = AudioEncoder.Ac3 });
				this.AudioEncoders.Add(new AudioEncoderViewModel { Encoder = AudioEncoder.Ac3Passthrough });
			}

			AudioEncoderViewModel newSelectedVM = null;
			if (lastSelectedEncoder != null)
			{
				newSelectedVM = this.AudioEncoders.SingleOrDefault(encoderVM => encoderVM.Encoder == lastSelectedEncoder.Encoder);
			}

			if (newSelectedVM == null)
			{
				newSelectedVM = this.AudioEncoders.Single(encoderVM => encoderVM.Encoder == AudioEncoder.Faac);
			}

			this.SelectedAudioEncoder = newSelectedVM;
		}

		private void RefreshMixdownChoices()
		{
			MixdownViewModel lastSelectedMixdown = this.SelectedMixdown;
			this.MixdownChoices.Clear();
			this.MixdownChoices.Add(new MixdownViewModel { Mixdown = Mixdown.Auto });

			switch (this.SelectedAudioEncoder.Encoder)
			{
				case AudioEncoder.Faac:
				case AudioEncoder.Ac3:
				case AudioEncoder.Lame:
					this.MixdownChoices.Add(new MixdownViewModel { Mixdown = Mixdown.Mono });
					this.MixdownChoices.Add(new MixdownViewModel { Mixdown = Mixdown.Stereo });
					this.MixdownChoices.Add(new MixdownViewModel { Mixdown = Mixdown.DolbySurround });
					this.MixdownChoices.Add(new MixdownViewModel { Mixdown = Mixdown.DolbyProLogicII });
					this.MixdownChoices.Add(new MixdownViewModel { Mixdown = Mixdown.SixChannelDiscrete });
					break;
				case AudioEncoder.Vorbis:
					this.MixdownChoices.Add(new MixdownViewModel { Mixdown = Mixdown.Mono });
					this.MixdownChoices.Add(new MixdownViewModel { Mixdown = Mixdown.Stereo });
					this.MixdownChoices.Add(new MixdownViewModel { Mixdown = Mixdown.DolbySurround });
					this.MixdownChoices.Add(new MixdownViewModel { Mixdown = Mixdown.DolbyProLogicII });
					break;
				default:
					break;
			}

			MixdownViewModel newSelectedMixdown = null;
			if (lastSelectedMixdown != null)
			{
				newSelectedMixdown = this.MixdownChoices.SingleOrDefault(mixdownVM => mixdownVM.Mixdown == lastSelectedMixdown.Mixdown);
			}

			if (newSelectedMixdown == null)
			{
				newSelectedMixdown = this.MixdownChoices.SingleOrDefault(mixdownVM => mixdownVM.Mixdown == Mixdown.DolbyProLogicII);
			}

			this.SelectedMixdown = newSelectedMixdown;
		}

		private void RefreshBitrateChoices()
		{
			switch (this.SelectedAudioEncoder.Encoder)
			{
				case AudioEncoder.Faac:
					if (this.SelectedMixdown.Mixdown == Mixdown.SixChannelDiscrete || this.SelectedMixdown.Mixdown == Mixdown.Auto)
					{
						this.SetMaxBitrate(768);
					}
					else
					{
						this.SetMaxBitrate(320);
					}

					break;
				case AudioEncoder.Lame:
					this.SetMaxBitrate(320);
					break;
				case AudioEncoder.Vorbis:
					this.SetMaxBitrate(384);
					break;
				case AudioEncoder.Ac3:
					this.SetMaxBitrate(640);
					break;
				default:
					break;
			}
		}

		private void SetMaxBitrate(int maxBitrate)
		{
			switch (maxBitrate)
			{
				case 320:
					this.BitrateChoices = new List<int> { 32, 40, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320 };
					break;
				case 384:
					this.BitrateChoices = new List<int> { 32, 40, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320, 384 };
					break;
				case 640:
					this.BitrateChoices = new List<int> { 32, 40, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320, 384, 448, 640 };
					break;
				case 768:
					this.BitrateChoices = new List<int> { 32, 40, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320, 384, 448, 640, 768 };
					break;
			}

			if (this.Bitrate > maxBitrate)
			{
				this.Bitrate = maxBitrate;
			}
		}

		private void SetAudioEncoder(AudioEncoder encoder)
		{
			AudioEncoderViewModel newSelectedVM = null;
			newSelectedVM = this.AudioEncoders.SingleOrDefault(encoderVM => encoderVM.Encoder == encoder);

			if (newSelectedVM == null)
			{
				newSelectedVM = this.AudioEncoders.Single(encoderVM => encoderVM.Encoder == AudioEncoder.Faac);
			}

			this.SelectedAudioEncoder = newSelectedVM;
		}

		private void SetMixdown(Mixdown mixdown)
		{
			MixdownViewModel newSelectedMixdown = null;
			newSelectedMixdown = this.MixdownChoices.SingleOrDefault(mixdownVM => mixdownVM.Mixdown == mixdown);

			if (newSelectedMixdown == null)
			{
				newSelectedMixdown = this.MixdownChoices.SingleOrDefault(mixdownVM => mixdownVM.Mixdown == Mixdown.DolbyProLogicII);
			}

			this.SelectedMixdown = newSelectedMixdown;
		}

		private void MarkModified()
		{
			if (!this.initializing)
			{
				this.encodingDialogVM.IsModified = true;
			}
		}
	}
}
