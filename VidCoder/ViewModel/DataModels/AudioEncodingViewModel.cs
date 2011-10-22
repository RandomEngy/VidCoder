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
using Microsoft.Practices.Unity;

namespace VidCoder.ViewModel
{
	public class AudioEncodingViewModel : ViewModelBase
	{
		private AudioPanelViewModel audioPanelVM;
		private bool initializing;

		private MainViewModel main = Unity.Container.Resolve<MainViewModel>();

		private ObservableCollection<TargetStreamViewModel> targetStreams;
		private int targetStreamIndex;
		private List<HBEncoder> audioEncoders;
		private HBEncoder selectedAudioEncoder;
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

			this.audioEncoders = new List<HBEncoder>();
			//this.audioEncoders = new ObservableCollection<AudioEncoderViewModel>();
			this.mixdownChoices = new List<HBMixdown>();

			this.outputFormat = outputFormat;
			this.RefreshEncoderChoices();
			this.RefreshMixdownChoices();
			this.RefreshBitrateChoices();

			this.SelectedAudioEncoder = Encoders.GetAudioEncoder(audioEncoding.Encoder);
			//this.SetAudioEncoder(audioEncoding.Encoder);
			this.SelectedMixdown = Encoders.GetMixdown(audioEncoding.Mixdown);
			//this.SetMixdown(audioEncoding.Mixdown);
			this.sampleRate = audioEncoding.SampleRateRaw;
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
					newAudioEncoding.Bitrate = this.SelectedBitrate.Bitrate;
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
					this.RaisePropertyChanged("TargetStreamIndex");
					this.audioPanelVM.NotifyAudioEncodingChanged();
					this.MarkModified();
				}
			}
		}

		public List<HBEncoder> AudioEncoders
		{
			get
			{
				return this.audioEncoders;
			}
		}

		public HBEncoder SelectedAudioEncoder
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

		public bool BitrateVisible
		{
			get
			{
				if (this.EncoderSettingsVisible && this.SelectedMixdown != null)
				{
					BitrateLimits bitrateLimits = Encoders.GetBitrateLimits(this.SelectedAudioEncoder, this.SampleRate, this.SelectedMixdown);
					return bitrateLimits.High > 0;
				}

				return false;
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

		private void RefreshEncoderChoices()
		{
			HBEncoder oldEncoder = this.selectedAudioEncoder;

			this.audioEncoders = new List<HBEncoder>();

			foreach (HBEncoder encoder in Encoders.AudioEncoders)
			{
				if ((encoder.CompatibleContainers & this.OutputFormat) > 0)
				{
					this.AudioEncoders.Add(encoder);
				}
			}

			this.RaisePropertyChanged("AudioEncoders");

			if (this.AudioEncoders.Contains(oldEncoder))
			{
				this.SelectedAudioEncoder = oldEncoder;
			}
			else
			{
				this.SelectedAudioEncoder = this.AudioEncoders[0];
			}
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
				this.SelectedMixdown = oldMixdown;
			}
			else
			{
				this.SelectedMixdown = this.MixdownChoices[0];
			}
		}

		private void RefreshBitrateChoices()
		{
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

			this.SelectedBitrate = this.BitrateChoices.Single(b => b.Bitrate == oldBitrate);
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
