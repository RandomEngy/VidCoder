using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Resources;
using GalaSoft.MvvmLight.Messaging;
using HandBrake.ApplicationServices.Interop;
using HandBrake.ApplicationServices.Interop.Model.Encoding;
using VidCoder.Messages;
using VidCoder.Model;
using VidCoder.Resources;
using VidCoder.Services;
using VidCoderCommon.Extensions;
using VidCoderCommon.Model;

namespace VidCoder.ViewModel
{
	public class VideoPanelViewModel : PanelViewModel
	{
		private const int DefaultVideoBitrateKbps = 900;
		private const int DefaultTargetSizeMB = 700;

		private static readonly HashSet<string> CheckBoxTunes = new HashSet<string> { "fastdecode", "zerolatency" }; 

		private ResourceManager resourceManager = new ResourceManager(typeof(EncodingRes));

		private OutputPathService outputPathService = Ioc.Container.GetInstance<OutputPathService>();

		private List<VideoEncoderViewModel> encoderChoices;

		private VideoEncoderViewModel selectedEncoder;
		private bool selectedEncoderInitialized;
		private List<double> framerateChoices;
		private int displayTargetSize;
		private int displayVideoBitrate;

		private List<string> presets;
		private List<ComboChoice> profileChoices;
		private List<LevelChoiceViewModel> levelChoices;
		private List<ComboChoice> tuneChoices;

		public VideoPanelViewModel(EncodingViewModel encodingViewModel)
			: base(encodingViewModel)
		{
			this.encoderChoices = new List<VideoEncoderViewModel>();

			this.framerateChoices = new List<double> { 0 };
			foreach (var framerate in HandBrakeEncoderHelpers.VideoFramerates)
			{
				this.framerateChoices.Add(double.Parse(framerate.Name, CultureInfo.InvariantCulture));
			}

			this.RefreshLevelCompatibility();

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

			Messenger.Default.Register<SelectedTitleChangedMessage>(
				this,
				message =>
				{
					this.RefreshLevelCompatibility();
				});

			Messenger.Default.Register<OutputSizeChangedMessage>(
				this,
				message =>
				{
					this.RefreshLevelCompatibility();
				});

			Messenger.Default.Register<FramerateChangedMessage>(
				this,
				message =>
				{
					this.RefreshLevelCompatibility();
				});

			Messenger.Default.Register<AdvancedOptionsChangedMessage>(
				this,
				message =>
				{
					this.RaisePropertyChanged(() => this.X264AdditionalOptions);
				});

			Messenger.Default.Register<ContainerChangedMessage>(
				this,
				message =>
				{
					// If the encoder changes due to a profile change, we'll want to apply default settings for the encoder.
					this.RefreshEncoderChoices(message.ContainerName, applyDefaults: true);
				});
		}

		public string InputType
		{
			get
			{
				if (this.HasSourceData)
				{
					return DisplayConversions.DisplayTitleType((TitleType)this.SelectedTitle.Type);
				}

				return string.Empty;
			}
		}

		public string InputVideoCodec
		{
			get
			{
				if (this.HasSourceData)
				{
					return DisplayConversions.DisplayVideoCodecName(this.SelectedTitle.VideoCodec);
				}

				return string.Empty;
			}
		}

		public string InputFramerate
		{
			get
			{
				if (this.HasSourceData)
				{
					return string.Format(EncodingRes.FpsFormat, this.SelectedTitle.FrameRate.ToDouble());
				}

				return string.Empty;
			}
		}

		public List<VideoEncoderViewModel> EncoderChoices
		{
			get
			{
				return this.encoderChoices;
			}
		}

		public VideoEncoderViewModel SelectedEncoder
		{
			get
			{
				return this.selectedEncoder;
			}

			set
			{
				if (value != null && value != this.selectedEncoder)
				{
					bool wasAdvancedTab = this.UseAdvancedTab;

					this.selectedEncoder = value;
					this.Profile.VideoEncoder = this.selectedEncoder.Encoder.ShortName;

					// Refresh preset/profile/tune/level choices and values
					this.RefreshEncoderSettings(applyDefaults: false);

					// There might be some auto-generated stuff on the additional options
					if (wasAdvancedTab && this.selectedEncoder.Encoder.ShortName == "x265")
					{
						this.Profile.VideoOptions = string.Empty;
						Messenger.Default.Send(new AdvancedOptionsChangedMessage());

						this.UseAdvancedTab = false;
					}

					this.NotifyEncoderChanged();
					this.IsModified = true;

					this.SetDefaultQuality();
				}
			}
		}

		private void NotifyEncoderChanged()
		{
			this.RaisePropertyChanged(() => this.SelectedEncoder);
			this.RaisePropertyChanged(() => this.X264SettingsVisible);
			this.RaisePropertyChanged(() => this.UseAdvancedTab);
			this.RaisePropertyChanged(() => this.PresetName);
			this.RaisePropertyChanged(() => this.EncoderSettingsVisible);
			this.RaisePropertyChanged(() => this.BasicEncoderSettingsVisible);
			this.RaisePropertyChanged(() => this.QsvSettingsVisible);
			this.RaisePropertyChanged(() => this.QualityModulus);
			this.RaisePropertyChanged(() => this.QualitySliderMin);
			this.RaisePropertyChanged(() => this.QualitySliderMax);
			this.RaisePropertyChanged(() => this.QualitySliderLeftText);
			this.RaisePropertyChanged(() => this.QualitySliderRightText);
			this.RaisePropertyChanged(() => this.TurboFirstPassVisible);
			Messenger.Default.Send(new VideoCodecChangedMessage());
		}

		public bool X264SettingsVisible
		{
			get
			{
				return this.SelectedEncoder.Encoder.ShortName == "x264";
			}
		}

		public bool QsvSettingsVisible
		{
			get
			{
				return this.SelectedEncoder.Encoder.ShortName == "qsv_h264";
			}
		}

		public bool EncoderSettingsVisible
		{
			get
			{
				string encoderName = this.SelectedEncoder.Encoder.ShortName;

				return encoderName == "x264" || encoderName == "x265" || encoderName == "qsv_h264";
			}
		}

		public bool BasicEncoderSettingsVisible
		{
			get
			{
				string encoderName = this.SelectedEncoder.Encoder.ShortName;

				if (encoderName == "qsv_h264")
				{
					return true;
				}

				return !this.UseAdvancedTab;
			}
		}

		public List<double> FramerateChoices
		{
			get
			{
				return this.framerateChoices;
			}
		}

		public double SelectedFramerate
		{
			get
			{
				return this.Profile.Framerate;
			}

			set
			{
				this.Profile.Framerate = value;
				this.RaisePropertyChanged(() => this.SelectedFramerate);
				this.RaisePropertyChanged(() => this.VfrChoiceText);
				this.RaisePropertyChanged(() => this.ConstantFramerateToolTip);
				this.RaisePropertyChanged(() => this.VariableFramerateToolTip);
				this.IsModified = true;

				Messenger.Default.Send(new FramerateChangedMessage());
			}
		}

		public bool ConstantFramerate
		{
			get
			{
				return this.Profile.ConstantFramerate;
			}

			set
			{
				this.Profile.ConstantFramerate = value;
				this.RaisePropertyChanged(() => this.ConstantFramerate);
				this.IsModified = true;
			}
		}

		public string ConstantFramerateToolTip
		{
			get
			{
				if (this.Profile.Framerate == 0)
				{
					return EncodingRes.CfrSameAsSourceToolTip;
				}
				else
				{
					return EncodingRes.CfrToolTip;
				}
			}
		}

		public string VariableFramerateToolTip
		{
			get
			{
				if (this.Profile.Framerate == 0)
				{
					return EncodingRes.VfrSameAsSourceToolTip;
				}
				else
				{
					return EncodingRes.VfrPeakFramerateToolTip;
				}
			}
		}

		public string VfrChoiceText
		{
			get
			{
				if (this.Profile.Framerate == 0)
				{
					return EncodingRes.VfrChoice;
				}
				else
				{
					return EncodingRes.VfrPeakChoice;
				}
			}
		}

		public bool TwoPassEncoding
		{
			get
			{
				return this.Profile.TwoPass;
			}

			set
			{
				this.Profile.TwoPass = value;
				this.RaisePropertyChanged(() => this.TwoPassEncoding);
				this.RaisePropertyChanged(() => this.TurboFirstPass);
				this.RaisePropertyChanged(() => this.TurboFirstPassEnabled);
				this.IsModified = true;
			}
		}

		public bool TwoPassEncodingEnabled
		{
			get
			{
                return this.VideoEncodeRateType != VCVideoEncodeRateType.ConstantQuality;
			}
		}

		public bool TurboFirstPass
		{
			get
			{
				if (!this.TwoPassEncoding)
				{
					return false;
				}

				return Profile.TurboFirstPass;
			}

			set
			{
				this.Profile.TurboFirstPass = value;
				this.RaisePropertyChanged(() => this.TurboFirstPass);
				this.IsModified = true;
			}
		}

		public bool TurboFirstPassEnabled
		{
			get
			{
                return this.VideoEncodeRateType != VCVideoEncodeRateType.ConstantQuality && this.TwoPassEncoding;
			}
		}

		public bool TurboFirstPassVisible
		{
            get { return this.VideoEncodeRateType != VCVideoEncodeRateType.ConstantQuality && (this.SelectedEncoder.Encoder.ShortName == "x265" || this.SelectedEncoder.Encoder.ShortName == "x264"); }
		}

        public VCVideoEncodeRateType VideoEncodeRateType
		{
			get
			{
				return this.Profile.VideoEncodeRateType;
			}

			set
			{
                VCVideoEncodeRateType oldRateType = this.Profile.VideoEncodeRateType;

				this.Profile.VideoEncodeRateType = value;
				this.RaisePropertyChanged(() => this.VideoEncodeRateType);

                if (value == VCVideoEncodeRateType.ConstantQuality)
				{
					this.SetDefaultQuality();

					// Disable two-pass options
					this.Profile.TwoPass = false;
					this.Profile.TurboFirstPass = false;
					this.RaisePropertyChanged(() => this.TwoPassEncoding);
					this.RaisePropertyChanged(() => this.TurboFirstPass);
				}

				this.RaisePropertyChanged(() => this.TwoPassEncodingEnabled);
				this.RaisePropertyChanged(() => this.TurboFirstPassEnabled);
				this.RaisePropertyChanged(() => this.TurboFirstPassVisible);

                if (value == VCVideoEncodeRateType.AverageBitrate)
				{
                    if (oldRateType == VCVideoEncodeRateType.ConstantQuality)
					{
						if (this.Profile.VideoBitrate == 0)
						{
							this.VideoBitrate = DefaultVideoBitrateKbps;
						}
					}
                    else if (oldRateType == VCVideoEncodeRateType.TargetSize)
					{
						if (this.displayVideoBitrate == 0)
						{
							this.VideoBitrate = DefaultVideoBitrateKbps;
						}
						else
						{
							this.VideoBitrate = this.displayVideoBitrate;
						}
					}

					this.RaisePropertyChanged(() => this.VideoBitrate);
					this.RaisePropertyChanged(() => this.TargetSize);
				}

                if (value == VCVideoEncodeRateType.TargetSize)
				{
                    if (oldRateType == VCVideoEncodeRateType.ConstantQuality)
					{
						if (this.Profile.TargetSize == 0)
						{
							this.TargetSize = DefaultTargetSizeMB;
						}
					}
                    else if (oldRateType == VCVideoEncodeRateType.AverageBitrate)
					{
						if (this.displayTargetSize == 0)
						{
							this.TargetSize = DefaultTargetSizeMB;
						}
						else
						{
							this.TargetSize = this.displayTargetSize;
						}
					}

					this.RaisePropertyChanged(() => this.TargetSize);
					this.RaisePropertyChanged(() => this.VideoBitrate);
				}

				this.outputPathService.GenerateOutputFileName();
				this.IsModified = true;
			}
		}

		public int TargetSize
		{
			get
			{
                if (this.VideoEncodeRateType == VCVideoEncodeRateType.AverageBitrate)
				{
					if (this.MainViewModel.HasVideoSource && this.MainViewModel.JobCreationAvailable && this.VideoBitrate > 0)
					{
						double estimatedSizeBytes = JsonEncodeFactory.CalculateFileSize(this.MainViewModel.EncodeJob, this.MainViewModel.SelectedTitle, this.VideoBitrate);
						this.displayTargetSize = (int)Math.Round(estimatedSizeBytes);
					}
					else
					{
						this.displayTargetSize = 0;
					}

					return this.displayTargetSize;
				}

				return this.Profile.TargetSize;
			}

			set
			{
				this.Profile.TargetSize = value;
				this.RaisePropertyChanged(() => this.TargetSize);
				this.RaisePropertyChanged(() => this.VideoBitrate);
				this.outputPathService.GenerateOutputFileName();
				this.IsModified = true;
			}
		}

		public int VideoBitrate
		{
			get
			{
                if (this.VideoEncodeRateType == VCVideoEncodeRateType.ConstantQuality)
				{
					return DefaultVideoBitrateKbps;
				}

                if (this.VideoEncodeRateType == VCVideoEncodeRateType.TargetSize)
				{
					if (this.MainViewModel.HasVideoSource && this.MainViewModel.JobCreationAvailable && this.TargetSize > 0)
					{
						this.displayVideoBitrate = JsonEncodeFactory.CalculateBitrate(this.MainViewModel.EncodeJob, this.MainViewModel.SelectedTitle, this.TargetSize);
					}
					else
					{
						this.displayVideoBitrate = 0;
					}

					return this.displayVideoBitrate;
				}

				return this.Profile.VideoBitrate;
			}

			set
			{
				this.Profile.VideoBitrate = value;
				this.RaisePropertyChanged(() => this.VideoBitrate);
				this.RaisePropertyChanged(() => this.TargetSize);
				this.outputPathService.GenerateOutputFileName();
				this.IsModified = true;
			}
		}

		public double Quality
		{
			get
			{
				return this.Profile.Quality;
			}

			set
			{
				this.Profile.Quality = value;
				this.RaisePropertyChanged(() => this.Quality);
				this.outputPathService.GenerateOutputFileName();
				this.IsModified = true;
			}
		}

		public double QualityModulus
		{
			get
			{
				return Math.Round(
					HandBrakeEncoderHelpers.GetVideoQualityLimits(HandBrakeEncoderHelpers.GetVideoEncoder(this.Profile.VideoEncoder)).Granularity,
					2);
			}
		}

		public float QualitySliderMin
		{
			get
			{
				return HandBrakeEncoderHelpers.GetVideoQualityLimits(this.SelectedEncoder.Encoder).Low;
			}
		}

		public float QualitySliderMax
		{
			get
			{
				return HandBrakeEncoderHelpers.GetVideoQualityLimits(this.SelectedEncoder.Encoder).High;
			}
		}

		public string QualitySliderLeftText
		{
			get
			{
				if (HandBrakeEncoderHelpers.GetVideoQualityLimits(this.SelectedEncoder.Encoder).Ascending)
				{
					return EncodingRes.LowQuality;
				}
				else
				{
					return EncodingRes.HighQuality;
				}
			}
		}

		public string QualitySliderRightText
		{
			get
			{
				if (HandBrakeEncoderHelpers.GetVideoQualityLimits(this.SelectedEncoder.Encoder).Ascending)
				{
					return EncodingRes.HighQuality;
				}
				else
				{
					return EncodingRes.LowQuality;
				}
			}
		}

		public bool UseAdvancedTab
		{
			get
			{
				if (this.SelectedEncoder.Encoder.ShortName != "x264")
				{
					return false;
				}

				return this.Profile.UseAdvancedTab;
			}

			set
			{
				this.Profile.UseAdvancedTab = value;

				if (value)
				{
					int width, height;
					if (this.MainViewModel.HasVideoSource && this.MainViewModel.SelectedTitle != null)
					{
						width = this.MainViewModel.SelectedTitle.Geometry.Width;
						height = this.MainViewModel.SelectedTitle.Geometry.Height;
					}
					else
					{
						width = 640;
						height = 480;
					}

					this.Profile.VideoOptions = HandBrakeUtils.CreateX264OptionsString(
						this.Profile.VideoPreset,
						this.Profile.VideoTunes,
						this.Profile.VideoOptions,
						this.Profile.VideoProfile,
						this.Profile.VideoLevel,
						width,
						height);

					this.EncodingViewModel.SelectedTabIndex = EncodingViewModel.AdvancedVideoTabIndex;
				}
				else
				{
					this.Profile.VideoOptions = string.Empty;
				}

				Messenger.Default.Send(new AdvancedOptionsChangedMessage());

				this.RaisePropertyChanged(() => this.UseAdvancedTab);
				this.RaisePropertyChanged(() => this.BasicEncoderSettingsVisible);
				this.IsModified = true;
			}
		}

		public List<ComboChoice> ProfileChoices
		{
			get
			{
				return this.profileChoices;
			}
		}

		public bool ProfileVisible
		{
			get
			{
				return this.profileChoices != null;
			}
		}

		public List<LevelChoiceViewModel> LevelChoices
		{
			get
			{
				return this.levelChoices;
			}
		}

		public bool LevelVisible
		{
			get
			{
				return this.levelChoices != null;
			}
		}

		public List<ComboChoice> TuneChoices
		{
			get
			{
				return this.tuneChoices;
			}
		}

		public bool TuneVisible
		{
			get
			{
				return this.tuneChoices != null;
			}
		}

		public string EncoderProfile
		{
			get
			{
				return this.Profile.VideoProfile;
			}

			set
			{
				this.Profile.VideoProfile = value;
				this.RaisePropertyChanged(() => this.EncoderProfile);
				this.IsModified = true;
			}
		}

		public string Level
		{
			get
			{
				return this.Profile.VideoLevel;
			}

			set
			{
				this.Profile.VideoLevel = value;
				this.RaisePropertyChanged(() => this.Level);
				this.IsModified = true;
			}
		}

		private string tune;
		public string Tune
		{
			get
			{
				return tune;
			}

			set
			{
				this.tune = value;
				this.WriteTuneListToProfile();
				this.RaisePropertyChanged(() => this.Tune);
				this.IsModified = true;
			}
		}

		private bool fastDecode;
		public bool FastDecode
		{
			get
			{
				return this.fastDecode;
			}

			set
			{
				this.fastDecode = value;
				this.WriteTuneListToProfile();
				this.RaisePropertyChanged(() => this.FastDecode);
				this.IsModified = true;
			}
		}

		public bool QsvDecode
		{
			get
			{
				return this.Profile.QsvDecode;
			}

			set
			{
				this.Profile.QsvDecode = value;
				this.RaisePropertyChanged(() => this.QsvDecode);
			}
		}

		public int PresetIndex
		{
			get
			{
				if (this.presets == null)
				{
					return 0;
				}

				string preset = this.Profile.VideoPreset;
				if (string.IsNullOrWhiteSpace(preset))
				{
					preset = GetDefaultPreset(this.Profile.VideoEncoder);
				}

				return this.presets.IndexOf(preset);
			}

			set
			{
				this.Profile.VideoPreset = this.presets[value];
				this.RaisePropertyChanged(() => this.PresetIndex);
				this.RaisePropertyChanged(() => this.PresetName);
				this.IsModified = true;
			}
		}

		public int PresetMaxIndex
		{
			get
			{
				if (this.presets == null)
				{
					return 1;
				}

				return this.presets.Count - 1;
			}
		}

		public bool PresetVisible
		{
			get
			{
				return this.presets != null;
			}
		}

		public string PresetName
		{
			get
			{
				string presetName = this.Profile.VideoPreset;
				if (string.IsNullOrEmpty(presetName))
				{
					presetName = GetDefaultPreset(this.SelectedEncoder.Encoder.ShortName);
				}

				switch (presetName)
				{
					case "ultrafast":
						return EncodingRes.Preset_UltraFast;
					case "superfast":
						return EncodingRes.Preset_SuperFast;
					case "veryfast":
						return EncodingRes.Preset_VeryFast;
					case "faster":
						return EncodingRes.Preset_Faster;
					case "fast":
						return EncodingRes.Preset_Fast;
					case "medium":
						return EncodingRes.Preset_Medium;
					case "slow":
						return EncodingRes.Preset_Slow;
					case "slower":
						return EncodingRes.Preset_Slower;
					case "veryslow":
						return EncodingRes.Preset_VerySlow;
					case "placebo":
						return EncodingRes.Preset_Placebo;

					case "speed":
						return EncodingRes.QsvPreset_Speed;
					case "balanced":
						return EncodingRes.QsvPreset_Balanced;
					case "quality":
						return EncodingRes.QsvPreset_Quality;
					default:
						return string.Empty;
				}
			}
		}

		public string X264AdditionalOptions
		{
			get
			{
				return this.Profile.VideoOptions;
			}

			set
			{
				this.Profile.VideoOptions = value;

				// Property notification will happen in response to the message
				Messenger.Default.Send(new AdvancedOptionsChangedMessage());
				this.IsModified = true;
			}
		}

		private void RefreshEncoderChoices(string containerName, bool applyDefaults)
		{
			HBContainer container = HandBrakeEncoderHelpers.GetContainer(containerName);

			HBVideoEncoder oldEncoder = null;
			if (this.selectedEncoder != null)
			{
				oldEncoder = this.selectedEncoder.Encoder;
			}

			this.encoderChoices = new List<VideoEncoderViewModel>();

			foreach (HBVideoEncoder encoder in HandBrakeEncoderHelpers.VideoEncoders)
			{
				if ((encoder.CompatibleContainers & container.Id) > 0)
				{
					this.EncoderChoices.Add(new VideoEncoderViewModel { Encoder = encoder });
				}
			}

			this.RaisePropertyChanged(() => this.EncoderChoices);

			this.selectedEncoder = this.EncoderChoices.FirstOrDefault(e => e.Encoder == oldEncoder);

			if (this.selectedEncoder == null)
			{
				this.selectedEncoder = this.EncoderChoices[0];
			}

			// We only want to refresh the settings if this is not startup.
			if (this.selectedEncoderInitialized && this.selectedEncoder.Encoder != oldEncoder)
			{
				RefreshEncoderSettings(applyDefaults);
				this.NotifyEncoderChanged();
			}

			this.RaisePropertyChanged(() => this.SelectedEncoder);
		}

		// Refreshes preset/profile/tune/level lists.
		private void RefreshEncoderSettings(bool applyDefaults)
		{
			this.EncodingViewModel.AutomaticChange = true;

			// Find the values we want to set to after refreshing the lists
			string newPreset = this.Profile.VideoPreset;
			string newProfile = this.EncoderProfile;
			List<string> newTunes = this.Profile.VideoTunes;
			string newLevel = this.Level;

			if (applyDefaults)
			{
				newPreset = GetDefaultPreset(this.SelectedEncoder.Encoder.ShortName);
				newProfile = null;
				newTunes = new List<string>();
				newLevel = null;
			}

			this.RefreshPresets();
			this.RefreshProfileChoices();
			this.RefreshTuneChoices();
			this.RefreshLevelChoices();

			this.RaisePropertyChanged(() => this.PresetMaxIndex);
			this.RaisePropertyChanged(() => this.PresetVisible);

			// Make sure to notify of the collection change before the index change. So that the system knows that the new value
			// is from the new collection.
			this.RaisePropertyChanged(() => this.ProfileChoices);
			this.RaisePropertyChanged(() => this.ProfileVisible);

			this.RaisePropertyChanged(() => this.TuneChoices);
			this.RaisePropertyChanged(() => this.TuneVisible);

			this.RaisePropertyChanged(() => this.LevelChoices);
			this.RaisePropertyChanged(() => this.LevelVisible);
			this.RefreshLevelCompatibility();

			// Apply the selected values, falling back to default if they no longer exist.
			if (this.presets != null)
			{
				if (this.presets.Contains(newPreset))
				{
					this.Profile.VideoPreset = newPreset;
				}
				else
				{
					this.Profile.VideoPreset = GetDefaultPreset(this.SelectedEncoder.Encoder.ShortName);
				}
			}

			if (this.profileChoices != null)
			{
				if (this.profileChoices.Any(c => c.Value == newProfile))
				{
					this.Profile.VideoProfile = newProfile;
				}
				else
				{
					this.Profile.VideoProfile = null;
				}
			}

			if (this.tuneChoices != null)
			{
				this.Profile.VideoTunes = newTunes;

				foreach (string tune in newTunes)
				{
					if (!this.tuneChoices.Any(t => t.Value == tune) && !CheckBoxTunes.Contains(tune))
					{
						this.Profile.VideoTunes = new List<string>();
					}
				}
			}

			if (this.levelChoices != null)
			{
				if (this.levelChoices.Any(l => l.Value == newLevel))
				{
					this.Profile.VideoLevel = newLevel;
				}
				else
				{
					this.Profile.VideoLevel = null;
				}
			}

			// Notify of the new values
			this.RaisePropertyChanged(() => this.PresetIndex);
			this.RaisePropertyChanged(() => this.PresetName);

			this.RaisePropertyChanged(() => this.EncoderProfile);

			this.RaisePropertyChanged(() => this.Tune);

			this.RaisePropertyChanged(() => this.Level);

			this.EncodingViewModel.AutomaticChange = false;
		}

		public void NotifyProfileChanged()
		{
			this.RefreshEncoderChoices(this.Profile.ContainerName, applyDefaults: false);

			this.selectedEncoder = this.EncoderChoices.SingleOrDefault(e => e.Encoder.ShortName == this.Profile.VideoEncoder);

			if (this.selectedEncoder == null)
			{
				this.selectedEncoder = this.EncoderChoices[0];
			}

			this.selectedEncoderInitialized = true;
			this.RefreshEncoderSettings(applyDefaults: false);
		}

		public void NotifyAllChanged()
		{
			this.ReadTuneListFromProfile();

			this.RaisePropertyChanged(() => this.SelectedEncoder);
			this.RaisePropertyChanged(() => this.SelectedFramerate);
			this.RaisePropertyChanged(() => this.ConstantFramerate);
			this.RaisePropertyChanged(() => this.ConstantFramerateToolTip);
			this.RaisePropertyChanged(() => this.VariableFramerateToolTip);
			this.RaisePropertyChanged(() => this.VfrChoiceText);
			this.RaisePropertyChanged(() => this.TwoPassEncoding);
			this.RaisePropertyChanged(() => this.TurboFirstPass);
			this.RaisePropertyChanged(() => this.TwoPassEncodingEnabled);
			this.RaisePropertyChanged(() => this.TurboFirstPassEnabled);
			this.RaisePropertyChanged(() => this.TurboFirstPassVisible);
			this.RaisePropertyChanged(() => this.VideoEncodeRateType);
			this.RaisePropertyChanged(() => this.TargetSize);
			this.RaisePropertyChanged(() => this.VideoBitrate);
			this.RaisePropertyChanged(() => this.Quality);
			this.RaisePropertyChanged(() => this.QualityModulus);
			this.RaisePropertyChanged(() => this.QualitySliderMin);
			this.RaisePropertyChanged(() => this.QualitySliderMax);
			this.RaisePropertyChanged(() => this.QualitySliderLeftText);
			this.RaisePropertyChanged(() => this.QualitySliderRightText);
			this.RaisePropertyChanged(() => this.X264SettingsVisible);
			this.RaisePropertyChanged(() => this.EncoderSettingsVisible);
			this.RaisePropertyChanged(() => this.BasicEncoderSettingsVisible);
			this.RaisePropertyChanged(() => this.UseAdvancedTab);
			this.RaisePropertyChanged(() => this.QsvSettingsVisible);
			this.RaisePropertyChanged(() => this.PresetIndex);
			this.RaisePropertyChanged(() => this.PresetMaxIndex);
			this.RaisePropertyChanged(() => this.PresetVisible);
			this.RaisePropertyChanged(() => this.PresetName);
			this.RaisePropertyChanged(() => this.EncoderProfile);
			this.RaisePropertyChanged(() => this.ProfileChoices);
			this.RaisePropertyChanged(() => this.ProfileVisible);
			this.RaisePropertyChanged(() => this.Level);
			this.RaisePropertyChanged(() => this.LevelChoices);
			this.RaisePropertyChanged(() => this.LevelVisible);
			this.RaisePropertyChanged(() => this.Tune);
			this.RaisePropertyChanged(() => this.TuneChoices);
			this.RaisePropertyChanged(() => this.TuneVisible);
			this.RaisePropertyChanged(() => this.X264AdditionalOptions);
		}

		/// <summary>
		/// Re-calculates video bitrate if needed.
		/// </summary>
		public void UpdateVideoBitrate()
		{
            if (this.VideoEncodeRateType == VCVideoEncodeRateType.TargetSize)
			{
				this.RaisePropertyChanged(() => this.VideoBitrate);
			}
		}

		/// <summary>
		/// Re-calculates target size if needed.
		/// </summary>
		public void UpdateTargetSize()
		{
            if (this.VideoEncodeRateType == VCVideoEncodeRateType.AverageBitrate)
			{
				this.RaisePropertyChanged(() => this.TargetSize);
			}
		}

		public override void NotifySelectedTitleChanged()
		{
			this.RaisePropertyChanged(() => this.InputType);
			this.RaisePropertyChanged(() => this.InputVideoCodec);
			this.RaisePropertyChanged(() => this.InputFramerate);

			base.NotifySelectedTitleChanged();
		}

		private static string GetDefaultPreset(string encoderName)
		{
			switch (encoderName)
			{
				case "x264":
					return "medium";
				case "qsv_h264":
					return "balanced";
				default:
					return null;
			}
		}

		private void NotifyAudioInputChanged()
		{
			this.UpdateVideoBitrate();
			this.UpdateTargetSize();
		}

		private void RefreshPresets()
		{
			if (this.SelectedEncoder == null)
			{
				return;
			}

			this.presets = this.SelectedEncoder.Encoder.Presets;
		}

		private void RefreshProfileChoices()
		{
			if (this.SelectedEncoder == null)
			{
				return;
			}

			List<string> profiles = this.SelectedEncoder.Encoder.Profiles;

			if (profiles == null)
			{
				this.profileChoices = null;
			}
			else
			{
				this.profileChoices = new List<ComboChoice>
				{
					new ComboChoice(null, CommonRes.Automatic)
				};

				foreach (string profile in profiles)
				{
					if (profile != "auto")
					{
						this.profileChoices.Add(new ComboChoice(profile, GetProfileDisplay(profile)));
					}
				}
			}
		}

		private string GetProfileDisplay(string profile)
		{
			string profileString = this.resourceManager.GetString("Profile_" + profile);
			return string.IsNullOrEmpty(profileString) ? profile : profileString;
		}

		private void RefreshTuneChoices()
		{
			if (this.SelectedEncoder == null)
			{
				return;
			}

			List<string> tunes = this.SelectedEncoder.Encoder.Tunes;

			if (tunes == null)
			{
				this.tuneChoices = null;
			}
			else
			{
				this.tuneChoices = new List<ComboChoice>
				{
					new ComboChoice(null, CommonRes.None)
				};

				foreach (string tune in tunes)
				{
					if (!CheckBoxTunes.Contains(tune))
					{
						this.tuneChoices.Add(new ComboChoice(tune, this.GetTuneDisplay(tune)));
					}
				}
			}
		}

		private string GetTuneDisplay(string tune)
		{
			string tuneString = this.resourceManager.GetString("Tune_" + tune);
			return string.IsNullOrEmpty(tuneString) ? tune : tuneString;
		}

		private void RefreshLevelChoices()
		{
			if (this.SelectedEncoder == null)
			{
				return;
			}

			List<string> levels = this.SelectedEncoder.Encoder.Levels;

			if (levels == null)
			{
				this.levelChoices = null;
			}
			else
			{
				this.levelChoices = new List<LevelChoiceViewModel>
				{
					new LevelChoiceViewModel(null, CommonRes.Automatic)
				};

				foreach (string level in levels.Skip(1))
				{
					this.levelChoices.Add(new LevelChoiceViewModel(level));
				}
			}
		}

		private void RefreshLevelCompatibility()
		{
			if (this.LevelChoices == null)
			{
				return;
			}

			foreach (LevelChoiceViewModel levelChoice in this.LevelChoices)
			{
				if (levelChoice.Value != null)
				{
					var main = this.EncodingViewModel.MainViewModel;
					var picturePanel = this.EncodingViewModel.PicturePanelViewModel;
					if (main.HasVideoSource && picturePanel.StorageWidth > 0 && picturePanel.StorageHeight > 0)
					{
						int fpsNumerator;
						int fpsDenominator;

						if (this.Profile.Framerate == 0)
						{
							fpsNumerator = main.SelectedTitle.FrameRate.Num;
							fpsDenominator = main.SelectedTitle.FrameRate.Den;
						}
						else
						{
							fpsNumerator = 27000000;
							fpsDenominator = HandBrakeUnitConversionHelpers.FramerateToVrate(this.Profile.Framerate);
						}

						bool interlaced = false;
						bool fakeInterlaced = false;

						Dictionary<string, string> advancedOptions = AdvancedOptionsParsing.ParseOptions(this.Profile.VideoOptions);
						if (advancedOptions.ContainsKey("interlaced") && advancedOptions["interlaced"] == "1" ||
							advancedOptions.ContainsKey("tff") && advancedOptions["tff"] == "1" ||
							advancedOptions.ContainsKey("bff") && advancedOptions["bff"] == "1")
						{
							interlaced = true;
						}

						if (advancedOptions.ContainsKey("fake-interlaced") && advancedOptions["fake-interlaced"] == "1")
						{
							fakeInterlaced = true;
						}

						levelChoice.IsCompatible = HandBrakeUtils.IsH264LevelValid(
							levelChoice.Value,
							picturePanel.StorageWidth,
							picturePanel.StorageHeight,
							fpsNumerator,
							fpsDenominator,
							interlaced,
							fakeInterlaced);
					}
					else
					{
						levelChoice.IsCompatible = true;
					}
				}
			}
		}

		private void SetDefaultQuality()
		{
			switch (this.SelectedEncoder.Encoder.ShortName)
			{
				case "x264":
					this.Quality = 20;
					break;
				case "mpeg4":
				case "mpeg2":
					this.Quality = 12;
					break;
				case "theora":
					this.Quality = 38;
					break;
				default:
					throw new InvalidOperationException("Unrecognized encoder.");
			}
		}

		private void ReadTuneListFromProfile()
		{
			this.tune = null;
			this.fastDecode = false;

			if (this.Profile.VideoTunes != null)
			{
				this.ReadTuneList(this.Profile.VideoTunes);
			}
		}

		private void ReadTuneList(IList<string> tunes)
		{
			foreach (string tune in tunes)
			{
				switch (tune)
				{
					case "fastdecode":
						this.fastDecode = true;
						break;
					case "zerolatency":
						break;
					default:
						this.tune = tune;
						break;
				}
			}
		}

		private void WriteTuneListToProfile()
		{
			var tunes = new List<string>();
			if (this.tune != null)
			{
				tunes.Add(this.tune);
			}

			if (this.fastDecode)
			{
				tunes.Add("fastdecode");
			}

			this.Profile.VideoTunes = tunes;
		}
	}
}
