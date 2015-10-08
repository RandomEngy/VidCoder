using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Resources;
using System.Windows.Media.TextFormatting;
using GalaSoft.MvvmLight.Messaging;
using HandBrake.ApplicationServices.Interop;
using HandBrake.ApplicationServices.Interop.Model.Encoding;
using ReactiveUI;
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

		private OutputPathService outputPathService = Ioc.Get<OutputPathService>();

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

		public VideoPanelViewModel(EncodingWindowViewModel encodingWindowViewModel)
			: base(encodingWindowViewModel)
		{
			this.AutomaticChange = true;

			this.RegisterProfileProperties();


			this.encoderChoices = new List<VideoEncoderViewModel>();

			this.framerateChoices = new List<double> { 0 };
			foreach (var framerate in HandBrakeEncoderHelpers.VideoFramerates)
			{
				this.framerateChoices.Add(double.Parse(framerate.Name, CultureInfo.InvariantCulture));
			}

			// InputType
			this.WhenAnyValue(x => x.HasSourceData, x => x.MainViewModel.SelectedTitle.Type, (hasSourceData, titleType) =>
			{
				if (hasSourceData)
				{
					return DisplayConversions.DisplayTitleType((TitleType)titleType);
				}

				return string.Empty;
			}).ToProperty(this, x => x.InputType, out this.inputType);

			// InputVideoCodec
			this.WhenAnyValue(x => x.HasSourceData, x => x.MainViewModel.SelectedTitle.VideoCodec, (hasSourceData, videoCodec) =>
			{
				if (hasSourceData)
				{
					return DisplayConversions.DisplayVideoCodecName(videoCodec);
				}

				return string.Empty;
			}).ToProperty(this, x => x.InputVideoCodec, out this.inputVideoCodec);

			// InputFramerate
			this.WhenAnyValue(x => x.HasSourceData, x => x.MainViewModel.SelectedTitle.FrameRate, (hasSourceData, framerate) =>
			{
				if (hasSourceData)
				{
					return string.Format(EncodingRes.FpsFormat, framerate.ToDouble());
				}

				return string.Empty;
			}).ToProperty(this, x => x.InputFramerate, out this.inputFramerate);

			// X264SettingsVisible
			this.WhenAnyValue(x => x.SelectedEncoder, selectedEncoder =>
			{
				if (selectedEncoder == null)
				{
					return false;
				}

				string videoEncoder = selectedEncoder.Encoder.ShortName;

				return videoEncoder == "x264";
			}).ToProperty(this, x => x.X264SettingsVisible, out this.x264SettingsVisible);

			// QsvSettingsVisible
			this.WhenAnyValue(x => x.SelectedEncoder, selectedEncoder =>
			{
				if (selectedEncoder == null)
				{
					return false;
				}

				string videoEncoder = selectedEncoder.Encoder.ShortName;

				return videoEncoder == "qsv_h264";
			}).ToProperty(this, x => x.QsvSettingsVisible, out this.qsvSettingsVisible);

			// EncoderSettingsVisible
			this.WhenAnyValue(x => x.SelectedEncoder, selectedEncoder =>
			{
				if (selectedEncoder == null)
				{
					return false;
				}

				string videoEncoder = selectedEncoder.Encoder.ShortName;

				return videoEncoder == "x264" || videoEncoder == "x265" || videoEncoder == "qsv_h264";
			}).ToProperty(this, x => x.EncoderSettingsVisible, out this.encoderSettingsVisible);

			// BasicEncoderSettingsVisible
			this.WhenAnyValue(x => x.SelectedEncoder, x => x.UseAdvancedTab, (selectedEncoder, useAdvancedTab) =>
			{
				if (this.selectedEncoder == null)
				{
					return false;
				}

				string videoEncoder = selectedEncoder.Encoder.ShortName;

				if (videoEncoder == "qsv_h264")
				{
					return true;
				}

				if (videoEncoder != "x264")
				{
					return true;
				}

				return !useAdvancedTab;
			}).ToProperty(this, x => x.BasicEncoderSettingsVisible, out this.basicEncoderSettingsVisible);

			// ConstantFramerateToolTip
			this.WhenAnyValue(x => x.Framerate, framerate =>
			{
				if (framerate == 0)
				{
					return EncodingRes.CfrSameAsSourceToolTip;
				}
				else
				{
					return EncodingRes.CfrToolTip;
				}
			}).ToProperty(this, x => x.ConstantFramerateToolTip, out this.constantFramerateToolTip);

			// VariableFramerateToolTip
			this.WhenAnyValue(x => x.Framerate, framerate =>
			{
				if (framerate == 0)
				{
					return EncodingRes.VfrSameAsSourceToolTip;
				}
				else
				{
					return EncodingRes.VfrPeakFramerateToolTip;
				}
			}).ToProperty(this, x => x.VariableFramerateToolTip, out this.variableFramerateToolTip);

			// VfrChoiceText
			this.WhenAnyValue(x => x.Framerate, framerate =>
			{
				if (framerate == 0)
				{
					return EncodingRes.VfrChoice;
				}
				else
				{
					return EncodingRes.VfrPeakChoice;
				}
			}).ToProperty(this, x => x.VfrChoiceText, out this.vfrChoiceText);

			// TwoPassEnabled
			this.WhenAnyValue(x => x.VideoEncodeRateType, videoEncodeRateType =>
			{
				return videoEncodeRateType != VCVideoEncodeRateType.ConstantQuality;
			}).ToProperty(this, x => x.TwoPassEnabled, out this.twoPassEnabled);

			// TurboFirstPassEnabled
			this.WhenAnyValue(x => x.VideoEncodeRateType, x => x.TwoPass, (videoEncodeRateType, twoPass) =>
			{
				return videoEncodeRateType != VCVideoEncodeRateType.ConstantQuality && twoPass;
			}).ToProperty(this, x => x.TurboFirstPassEnabled, out this.turboFirstPassEnabled);

			// TurboFirstPassVisible
			this.WhenAnyValue(x => x.VideoEncodeRateType, x => x.SelectedEncoder, (videoEncodeRateType, selectedEncoder) =>
			{
				if (selectedEncoder == null)
				{
					return false;
				}

				string videoEncoderShortName = selectedEncoder.Encoder.ShortName;

				return videoEncodeRateType != VCVideoEncodeRateType.ConstantQuality && (videoEncoderShortName == "x265" || videoEncoderShortName == "x264");
			}).ToProperty(this, x => x.TurboFirstPassVisible, out this.turboFirstPassVisible);

			// QualityModulus
			this.WhenAnyValue(x => x.SelectedEncoder, selectedEncoder =>
			{
				if (selectedEncoder == null)
				{
					return 0;
				}

				return Math.Round(
					HandBrakeEncoderHelpers.GetVideoQualityLimits(selectedEncoder.Encoder).Granularity,
					2);
			}).ToProperty(this, x => x.QualityModulus, out this.qualityModulus);

			// QualitySliderMin
			this.WhenAnyValue(x => x.SelectedEncoder, selectedEncoder =>
			{
				if (selectedEncoder == null)
				{
					return 0;
				}

				return HandBrakeEncoderHelpers.GetVideoQualityLimits(selectedEncoder.Encoder).Low;
			}).ToProperty(this, x => x.QualitySliderMin, out this.qualitySliderMin);

			// QualitySliderMax
			this.WhenAnyValue(x => x.SelectedEncoder, selectedEncoder =>
			{
				if (selectedEncoder == null)
				{
					return 0;
				}

				return HandBrakeEncoderHelpers.GetVideoQualityLimits(selectedEncoder.Encoder).High;
			}).ToProperty(this, x => x.QualitySliderMax, out this.qualitySliderMax);

			// QualitySliderLeftText
			this.WhenAnyValue(x => x.SelectedEncoder, selectedEncoder =>
			{
				if (selectedEncoder == null)
				{
					return string.Empty;
				}

				if (HandBrakeEncoderHelpers.GetVideoQualityLimits(selectedEncoder.Encoder).Ascending)
				{
					return EncodingRes.LowQuality;
				}
				else
				{
					return EncodingRes.HighQuality;
				}
			}).ToProperty(this, x => x.QualitySliderLeftText, out this.qualitySliderLeftText);

			// QualitySliderRightText
			this.WhenAnyValue(x => x.SelectedEncoder, selectedEncoder =>
			{
				if (selectedEncoder == null)
				{
					return string.Empty;
				}

				if (HandBrakeEncoderHelpers.GetVideoQualityLimits(selectedEncoder.Encoder).Ascending)
				{
					return EncodingRes.HighQuality;
				}
				else
				{
					return EncodingRes.LowQuality;
				}
			}).ToProperty(this, x => x.QualitySliderRightText, out this.qualitySliderRightText);

			// PresetName
			this.WhenAnyValue(x => x.SelectedEncoder, x => x.PresetIndex, (selectedEncoder, presetIndex) =>
			{
				if (selectedEncoder == null)
				{
					return string.Empty;
				}

				string currentPresetName = null;
				if (this.presets != null && presetIndex < this.presets.Count)
				{
					currentPresetName = this.presets[presetIndex];
				}

				if (string.IsNullOrEmpty(currentPresetName))
				{
					currentPresetName = GetDefaultPreset(selectedEncoder.Encoder.ShortName);
				}

				switch (currentPresetName)
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
			}).ToProperty(this, x => x.PresetName, out this.presetName);

			this.RefreshLevelCompatibility();

			this.PresetsService.WhenAnyValue(x => x.SelectedPreset.Preset.EncodingProfile)
				.Subscribe(x =>
				{
					this.ReadTuneListFromProfile();
					this.RaisePropertyChanged(MvvmUtilities.GetPropertyName(() => this.Tune));
					this.RaisePropertyChanged(MvvmUtilities.GetPropertyName(() => this.FastDecode));
				});

			this.PresetsService.WhenAnyValue(x => x.SelectedPreset.Preset.EncodingProfile.VideoOptions)
				.Subscribe(_ =>
				{
					if (!this.updatingLocalVideoOptions)
					{
						this.RaisePropertyChanged(MvvmUtilities.GetPropertyName(() => this.VideoOptions));
					}
				});

			this.PresetsService.WhenAnyValue(x => x.SelectedPreset.Preset.EncodingProfile.ContainerName)
				.Subscribe(containerName =>
				{
					this.RefreshEncoderChoices(containerName, applyDefaults: false);
				});

			this.WhenAnyValue(x => x.SelectedEncoder.Encoder.ShortName)
				.Subscribe(videoEncoder =>
				{
					this.RefreshEncoderSettings(applyDefaults: false);
				});

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

			Messenger.Default.Register<ContainerChangedMessage>(
				this,
				message =>
				{
					// If the encoder changes due to a profile change, we'll want to apply default settings for the encoder.
					this.RefreshEncoderChoices(message.ContainerName, applyDefaults: true);
				});

			this.AutomaticChange = false;
		}

		private void RegisterProfileProperties()
		{
			this.RegisterProfileProperty(() => this.Profile.VideoEncoder);

			this.RegisterProfileProperty(() => this.Profile.Framerate, () =>
			{
				Messenger.Default.Send(new FramerateChangedMessage());
			});

			this.RegisterProfileProperty(() => this.Profile.ConstantFramerate);
			this.RegisterProfileProperty(() => this.Profile.TwoPass, () =>
			{
				if (!this.TwoPass && this.TurboFirstPass)
				{
					this.TurboFirstPass = false;
				}
			});

			this.RegisterProfileProperty(() => this.Profile.TurboFirstPass);
			this.RegisterProfileProperty(() => this.VideoEncodeRateType, oldValue =>
			{
				VCVideoEncodeRateType oldRateType = (VCVideoEncodeRateType)oldValue;

				if (this.VideoEncodeRateType == VCVideoEncodeRateType.ConstantQuality)
				{
					this.SetDefaultQuality();

					// Disable two-pass options
					this.TwoPass = false;
					this.TurboFirstPass = false;
				}

				if (this.VideoEncodeRateType == VCVideoEncodeRateType.AverageBitrate)
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
				}

				if (this.VideoEncodeRateType == VCVideoEncodeRateType.TargetSize)
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
				}

				this.outputPathService.GenerateOutputFileName();
			});

			this.RegisterProfileProperty(() => this.Profile.Quality, () =>
			{
				this.outputPathService.GenerateOutputFileName();
			});

			this.RegisterProfileProperty(() => this.Profile.UseAdvancedTab, () =>
			{
				if (this.UseAdvancedTab)
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

					this.EncodingWindowViewModel.SelectedTabIndex = EncodingWindowViewModel.AdvancedVideoTabIndex;
				}
				else
				{
					this.Profile.VideoOptions = string.Empty;
				}

				Messenger.Default.Send(new AdvancedOptionsChangedMessage());
			});

			this.RegisterProfileProperty(() => this.Profile.VideoProfile);
			this.RegisterProfileProperty(() => this.Profile.VideoLevel);
			this.RegisterProfileProperty(() => this.Profile.VideoPreset);
			this.RegisterProfileProperty(() => this.Profile.QsvDecode);
			this.RegisterProfileProperty(() => this.Profile.TargetSize, () =>
			{
				this.RaisePropertyChanged(MvvmUtilities.GetPropertyName(() => this.VideoBitrate));
				this.outputPathService.GenerateOutputFileName();
			});

			this.RegisterProfileProperty(() => this.Profile.VideoBitrate, () =>
			{
				this.RaisePropertyChanged(MvvmUtilities.GetPropertyName(() => this.TargetSize));
				this.outputPathService.GenerateOutputFileName();
			});

			this.RegisterProfileProperty(() => this.Profile.VideoTunes);
		}

		private ObservableAsPropertyHelper<string> inputType;
		public string InputType
		{
			get { return this.inputType.Value; }
		}

		private ObservableAsPropertyHelper<string> inputVideoCodec;
		public string InputVideoCodec
		{
			get { return this.inputVideoCodec.Value; }
		}

		private ObservableAsPropertyHelper<string> inputFramerate;
		public string InputFramerate
		{
			get { return this.inputFramerate.Value; }
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

					this.UpdateProfileProperty(() => this.Profile.VideoEncoder, this.selectedEncoder.Encoder.ShortName, raisePropertyChanged: false);

					// Refresh preset/profile/tune/level choices and values
					this.RefreshEncoderSettings(applyDefaults: false);

					// There might be some auto-generated stuff on the additional options
					if (wasAdvancedTab && this.selectedEncoder.Encoder.ShortName == "x265")
					{
						this.Profile.VideoOptions = string.Empty;
						Messenger.Default.Send(new AdvancedOptionsChangedMessage());

						this.UseAdvancedTab = false;
					}

					this.RaisePropertyChanged();
					Messenger.Default.Send(new VideoCodecChangedMessage());

					this.SetDefaultQuality();
				}
			}
		}

		private ObservableAsPropertyHelper<bool> x264SettingsVisible;
		public bool X264SettingsVisible
		{
			get { return this.x264SettingsVisible.Value; }
		}

		private ObservableAsPropertyHelper<bool> qsvSettingsVisible;
		public bool QsvSettingsVisible
		{
			get { return this.qsvSettingsVisible.Value; }
		}

		private ObservableAsPropertyHelper<bool> encoderSettingsVisible;
		public bool EncoderSettingsVisible
		{
			get { return this.encoderSettingsVisible.Value; }
		}

		private ObservableAsPropertyHelper<bool> basicEncoderSettingsVisible;
		public bool BasicEncoderSettingsVisible
		{
			get { return this.basicEncoderSettingsVisible.Value; }
		}

		public List<double> FramerateChoices
		{
			get
			{
				return this.framerateChoices;
			}
		}

		public double Framerate
		{
			get { return this.Profile.Framerate; }
			set { this.UpdateProfileProperty(() => this.Profile.Framerate, value); }
		}

		public bool ConstantFramerate
		{
			get { return this.Profile.ConstantFramerate; }
			set { this.UpdateProfileProperty(() => this.Profile.ConstantFramerate, value); }
		}

		private ObservableAsPropertyHelper<string> constantFramerateToolTip;
		public string ConstantFramerateToolTip
		{
			get { return this.constantFramerateToolTip.Value; }
		}

		private ObservableAsPropertyHelper<string> variableFramerateToolTip;
		public string VariableFramerateToolTip
		{
			get { return this.variableFramerateToolTip.Value; }
		}

		private ObservableAsPropertyHelper<string> vfrChoiceText;
		public string VfrChoiceText
		{
			get { return this.vfrChoiceText.Value; }
		}

		public bool TwoPass
		{
			get { return this.Profile.TwoPass; }
			set { this.UpdateProfileProperty(() => this.Profile.TwoPass, value); }
		}

		private ObservableAsPropertyHelper<bool> twoPassEnabled;
		public bool TwoPassEnabled
		{
			get { return this.twoPassEnabled.Value; }
		}

		public bool TurboFirstPass
		{
			get { return this.Profile.TurboFirstPass; }
			set { this.UpdateProfileProperty(() => this.Profile.TurboFirstPass, value); }
		}

		private ObservableAsPropertyHelper<bool> turboFirstPassEnabled;
		public bool TurboFirstPassEnabled
		{
			get { return this.turboFirstPassEnabled.Value; }
		}

		private ObservableAsPropertyHelper<bool> turboFirstPassVisible;
		public bool TurboFirstPassVisible
		{
			get { return this.turboFirstPassVisible.Value; }
		}

		public VCVideoEncodeRateType VideoEncodeRateType
		{
			get { return this.Profile.VideoEncodeRateType; }
			set { this.UpdateProfileProperty(() => this.Profile.VideoEncodeRateType, value); }
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
				this.UpdateProfileProperty(() => this.Profile.TargetSize, value);
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
				this.UpdateProfileProperty(() => this.Profile.VideoBitrate, value);
			}
		}

		public double Quality
		{
			get { return this.Profile.Quality; }
			set { this.UpdateProfileProperty(() => this.Profile.Quality, value); }
		}

		private ObservableAsPropertyHelper<double> qualityModulus;
		public double QualityModulus
		{
			get { return this.qualityModulus.Value; }
		}

		private ObservableAsPropertyHelper<float> qualitySliderMin;
		public float QualitySliderMin
		{
			get { return this.qualitySliderMin.Value; }
		}

		private ObservableAsPropertyHelper<float> qualitySliderMax;
		public float QualitySliderMax
		{
			get { return this.qualitySliderMax.Value; }
		}

		private ObservableAsPropertyHelper<string> qualitySliderLeftText;
		public string QualitySliderLeftText
		{
			get { return this.qualitySliderLeftText.Value; }
		}

		private ObservableAsPropertyHelper<string> qualitySliderRightText;
		public string QualitySliderRightText
		{
			get { return this.qualitySliderRightText.Value; }
		}

		public bool UseAdvancedTab
		{
			get { return this.Profile.UseAdvancedTab; }
			set { this.UpdateProfileProperty(() => this.Profile.UseAdvancedTab, value); }
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

		public string VideoProfile
		{
			get { return this.Profile.VideoProfile; }
			set { this.UpdateProfileProperty(() => this.Profile.VideoProfile, value); }
		}

		public string VideoLevel
		{
			get { return this.Profile.VideoLevel; }
			set { this.UpdateProfileProperty(() => this.Profile.VideoLevel, value); }
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
				this.RaisePropertyChanged();
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
				this.RaisePropertyChanged();
			}
		}

		public bool QsvDecode
		{
			get { return this.Profile.QsvDecode; }
			set { this.UpdateProfileProperty(() => this.Profile.QsvDecode, value); }
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
				this.UpdateProfileProperty(() => this.Profile.VideoPreset, this.presets[value], raisePropertyChanged: false);

				this.RaisePropertyChanged();
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

		private ObservableAsPropertyHelper<string> presetName;
		public string PresetName
		{
			get { return this.presetName.Value; }
		}

		private bool updatingLocalVideoOptions;

		public string VideoOptions
		{
			get { return this.Profile.VideoOptions; }
			set
			{
				this.updatingLocalVideoOptions = true;
				this.UpdateProfileProperty(() => this.Profile.VideoOptions, value);
				this.updatingLocalVideoOptions = false;
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

			this.RaisePropertyChanged(MvvmUtilities.GetPropertyName(() => this.EncoderChoices));

			this.selectedEncoder = this.EncoderChoices.FirstOrDefault(e => e.Encoder == oldEncoder);

			if (this.selectedEncoder == null)
			{
				this.selectedEncoder = this.EncoderChoices[0];
			}

			// We only want to refresh the settings if this is not startup.
			if (this.selectedEncoderInitialized && this.selectedEncoder.Encoder != oldEncoder)
			{
				RefreshEncoderSettings(applyDefaults);

				Messenger.Default.Send(new VideoCodecChangedMessage());
			}

			this.RaisePropertyChanged(MvvmUtilities.GetPropertyName(() => this.SelectedEncoder));
		}

		// Refreshes preset/profile/tune/level lists.
		private void RefreshEncoderSettings(bool applyDefaults)
		{
			this.EncodingWindowViewModel.AutomaticChange = true;

			// Find the values we want to set to after refreshing the lists
			string newPreset = this.Profile.VideoPreset;
			string newProfile = this.Profile.VideoProfile;
			List<string> newTunes = this.Profile.VideoTunes;
			string newLevel = this.Profile.VideoLevel;

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

			this.RaisePropertyChanged(MvvmUtilities.GetPropertyName(() => this.PresetMaxIndex));
			this.RaisePropertyChanged(MvvmUtilities.GetPropertyName(() => this.PresetVisible));

			// Make sure to notify of the collection change before the index change. So that the system knows that the new value
			// is from the new collection.
			this.RaisePropertyChanged(MvvmUtilities.GetPropertyName(() => this.ProfileChoices));
			this.RaisePropertyChanged(MvvmUtilities.GetPropertyName(() => this.ProfileVisible));

			this.RaisePropertyChanged(MvvmUtilities.GetPropertyName(() => this.TuneChoices));
			this.RaisePropertyChanged(MvvmUtilities.GetPropertyName(() => this.TuneVisible));

			this.RaisePropertyChanged(MvvmUtilities.GetPropertyName(() => this.LevelChoices));
			this.RaisePropertyChanged(MvvmUtilities.GetPropertyName(() => this.LevelVisible));
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
			this.RaisePropertyChanged(MvvmUtilities.GetPropertyName(() => this.PresetIndex));

			this.RaisePropertyChanged(MvvmUtilities.GetPropertyName(() => this.VideoProfile));

			this.RaisePropertyChanged(MvvmUtilities.GetPropertyName(() => this.Tune));

			this.RaisePropertyChanged(MvvmUtilities.GetPropertyName(() => this.VideoLevel));

			this.EncodingWindowViewModel.AutomaticChange = false;
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

		/// <summary>
		/// Re-calculates video bitrate if needed.
		/// </summary>
		public void UpdateVideoBitrate()
		{
            if (this.VideoEncodeRateType == VCVideoEncodeRateType.TargetSize)
			{
				this.RaisePropertyChanged(MvvmUtilities.GetPropertyName(() => this.VideoBitrate));
			}
		}

		/// <summary>
		/// Re-calculates target size if needed.
		/// </summary>
		public void UpdateTargetSize()
		{
            if (this.VideoEncodeRateType == VCVideoEncodeRateType.AverageBitrate)
			{
				this.RaisePropertyChanged(MvvmUtilities.GetPropertyName(() => this.TargetSize));
			}
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
					var main = this.EncodingWindowViewModel.MainViewModel;
					var picturePanel = this.EncodingWindowViewModel.PicturePanelViewModel;
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

			this.UpdateProfileProperty(() => this.Profile.VideoTunes, tunes, raisePropertyChanged: false);
		}
	}
}
