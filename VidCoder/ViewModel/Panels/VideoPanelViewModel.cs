using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reactive.Linq;
using System.Resources;
using DynamicData;
using HandBrake.Interop.Interop;
using HandBrake.Interop.Interop.Interfaces.Model;
using HandBrake.Interop.Interop.Interfaces.Model.Encoders;
using HandBrake.Interop.Utilities;
using Microsoft.AnyContainer;
using ReactiveUI;
using VidCoder.Extensions;
using VidCoder.Model;
using VidCoder.Resources;
using VidCoder.Services;
using VidCoderCommon.Extensions;
using VidCoderCommon.Model;

namespace VidCoder.ViewModel;

public class VideoPanelViewModel : PanelViewModel
{
	private const int DefaultVideoBitrateKbps = 900;
	private const int DefaultTargetSizeMB = 700;

	private static readonly HashSet<string> CheckBoxTunes = new HashSet<string> { "fastdecode", "zerolatency" }; 

	private ResourceManager resourceManager = new ResourceManager(typeof(EncodingRes));

	private OutputPathService outputPathService = StaticResolver.Resolve<OutputPathService>();
	private MainViewModel main = StaticResolver.Resolve<MainViewModel>();


	private VideoEncoderViewModel selectedEncoder;
	private List<double> framerateChoices;
	private double displayTargetSize;
	private int displayVideoBitrate;

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
		this.WhenAnyValue(x => x.HasSourceData, x => x.MainViewModel.SelectedTitle.Title.Type, (hasSourceData, titleType) =>
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

		// MultiPassVisible
		this.WhenAnyValue(x => x.VideoEncodeRateType, x => x.SelectedEncoder, (videoEncodeRateType, selectedEncoder) =>
		{
			if (selectedEncoder != null)
			{
				if (!HandBrakeEncoderHelpers.VideoEncoderSupportsMultiPass(selectedEncoder.Encoder.Id, videoEncodeRateType == VCVideoEncodeRateType.ConstantQuality))
				{
					return false;
				}
			}

			return videoEncodeRateType != VCVideoEncodeRateType.ConstantQuality;
		}).ToProperty(this, x => x.MultiPassVisible, out this.multiPassVisible);

		// TurboFirstPassEnabled
		this.WhenAnyValue(x => x.VideoEncodeRateType, x => x.MultiPass, (videoEncodeRateType, multiPass) =>
		{
			return videoEncodeRateType != VCVideoEncodeRateType.ConstantQuality && multiPass;
		}).ToProperty(this, x => x.TurboFirstPassEnabled, out this.turboFirstPassEnabled);

		// TurboFirstPassVisible
		this.WhenAnyValue(x => x.VideoEncodeRateType, x => x.SelectedEncoder, (videoEncodeRateType, selectedEncoder) =>
		{
			if (selectedEncoder == null)
			{
				return false;
			}

			string videoEncoderShortName = selectedEncoder.Encoder.ShortName;

			return videoEncodeRateType != VCVideoEncodeRateType.ConstantQuality && videoEncoderShortName.StartsWith("x26", StringComparison.Ordinal);
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

		// QualityName
		this.WhenAnyValue(x => x.SelectedEncoder, selectedEncoder =>
		{
			if (selectedEncoder == null)
			{
				return string.Empty;
			}

			return HandBrakeEncoderHelpers.GetVideoQualityRateControlName(selectedEncoder.Encoder.ShortName);
		}).ToProperty(this, x => x.QualityName, out this.qualityName);

		// PresetName
		this.WhenAnyValue(x => x.SelectedEncoder, x => x.Presets, x => x.PresetIndex, (selectedEncoder, localPresets, presetIndex) =>
		{
			if (selectedEncoder == null)
			{
				return string.Empty;
			}

			string currentPresetName = null;
			if (localPresets != null && presetIndex >= 0 && presetIndex < localPresets.Count)
			{
				currentPresetName = localPresets[presetIndex];
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

				case "ll":
					return EncodingRes.NVEncPreset_ll;
				case "hq":
					return EncodingRes.NVEncPreset_hq;
				case "hp":
					return EncodingRes.NVEncPreset_hp;

				default:
					return currentPresetName;
			}
		}).ToProperty(this, x => x.PresetName, out this.presetName);

		// FullParameterList
		this.WhenAnyValue(
			x => x.SelectedEncoder,
			x => x.PresetsService.SelectedPreset.Preset.EncodingProfile.VideoPreset,
			x => x.PresetsService.SelectedPreset.Preset.EncodingProfile.VideoTunes,
			x => x.VideoOptions,
			x => x.VideoProfile, 
			x => x.VideoLevel,
			x => x.OutputSizeService.Size,
			(selectedEncoder, videoPreset, videoTunes, videoOptions, videoProfile, videoLevel, outputSize) =>
			{
				if (selectedEncoder == null || selectedEncoder.Encoder.ShortName != "x264")
				{
					return string.Empty;
				}

				int width, height;
				if (outputSize != null)
				{
					width = outputSize.OutputWidth;
					height = outputSize.OutputHeight;
				}
				else
				{
					width = 640;
					height = 480;
				}

				if (width <= 0)
				{
					width = 640;
				}

				if (height <= 0)
				{
					height = 480;
				}

				string parameterList = HandBrakeUtils.CreateX264OptionsString(
					videoPreset,
					videoTunes,
					videoOptions?.Trim(),
					videoProfile,
					videoLevel,
					width,
					height);

				return parameterList;
			}).ToProperty(this, x => x.FullParameterList, out this.fullParameterList);

		this.PresetsService.WhenAnyValue(x => x.SelectedPreset.Preset.EncodingProfile)
			.Subscribe(_ =>
			{
				this.ReadTuneListFromProfile();
				this.RaisePropertyChanged(nameof(this.Tune));
				this.RaisePropertyChanged(nameof(this.FastDecode));
				this.RaisePropertyChanged(nameof(this.PresetIndex));
			});

		this.PresetsService.WhenAnyValue(x => x.SelectedPreset.Preset.EncodingProfile.VideoEncoder)
			.Subscribe(_ =>
			{
				if (!this.userVideoEncoderChange)
				{
					this.RefreshEncoderChoices(this.PresetsService.SelectedPreset.Preset.EncodingProfile.ContainerName, EncoderChoicesRefreshSource.ProfileChange);
				}
			});

		this.PresetsService.WhenAnyValue(x => x.SelectedPreset.Preset.EncodingProfile.VideoOptions)
			.Subscribe(_ =>
			{
				if (!this.updatingLocalVideoOptions)
				{
					this.RaisePropertyChanged(nameof(this.VideoOptions));
				}
			});

		this.PresetsService.WhenAnyValue(x => x.SelectedPreset.Preset.EncodingProfile.ContainerName)
			.Skip(1)
			.Subscribe(containerName =>
			{
				this.RefreshEncoderChoices(containerName, EncoderChoicesRefreshSource.ContainerChange);

				// Refresh preset/profile/tune/level choices and values
				this.RefreshEncoderSettings(applyDefaults: false);
			});

	    this.PresetsService.WhenAnyValue(x => x.SelectedPreset.Preset.EncodingProfile.AudioCopyMask)
	        .Skip(1)
	        .Subscribe(copyMask =>
	        {
                    this.NotifyAudioChanged();
	        });

		this.PresetsService.WhenAnyValue(x => x.SelectedPreset.Preset.EncodingProfile.AudioEncodings)
			.Skip(1)
			.Subscribe(copyMask =>
			{
				this.NotifyAudioChanged();
			});

		this.WhenAnyValue(x => x.SelectedEncoder.Encoder.ShortName)
			.Subscribe(videoEncoder =>
			{
				this.RefreshEncoderSettings(applyDefaults: false);
			});

		this.main.AudioTracks
			.Connect()
			.WhenAnyPropertyChanged()
			.Subscribe(_ =>
			{
				this.NotifyAudioChanged();
			});

		this.main.WhenAnyValue(x => x.SelectedTitle)
			.Skip(1)
			.Subscribe(_ =>
			{
				this.NotifyAudioChanged();
			});

		this.AutomaticChange = false;
	}

	public OutputSizeService OutputSizeService { get; } = StaticResolver.Resolve<OutputSizeService>();

	private void RegisterProfileProperties()
	{
		this.RegisterProfileProperty(nameof(this.Profile.VideoEncoder), () =>
		{
			if (!HandBrakeEncoderHelpers.VideoEncoderSupportsMultiPass(HandBrakeEncoderHelpers.GetVideoEncoder(
				this.Profile.VideoEncoder).Id,
				this.Profile.VideoEncodeRateType == VCVideoEncodeRateType.ConstantQuality))
			{
				this.MultiPass = false;
				this.TurboFirstPass = false;
			}
		});

		this.RegisterProfileProperty(nameof(this.Profile.Framerate));
		this.RegisterProfileProperty(nameof(this.Profile.ConstantFramerate));
		this.RegisterProfileProperty(nameof(this.Profile.MultiPass), () =>
		{
			if (!this.MultiPass && this.TurboFirstPass)
			{
				this.TurboFirstPass = false;
			}
		});

		this.RegisterProfileProperty(nameof(this.Profile.TurboFirstPass));
		this.RegisterProfileProperty(nameof(this.VideoEncodeRateType), oldValue =>
		{
			VCVideoEncodeRateType oldRateType = (VCVideoEncodeRateType)oldValue;

			if (this.VideoEncodeRateType == VCVideoEncodeRateType.ConstantQuality)
			{
				this.SetDefaultQuality();

				// Disable two-pass options
				this.MultiPass = false;
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
					else
					{
						// If we already have a bitrate, update the UI
						this.RaisePropertyChanged(nameof(this.VideoBitrate));
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

		this.RegisterProfileProperty(nameof(this.Profile.Quality), () =>
		{
			this.outputPathService.GenerateOutputFileName();
		});

		this.RegisterProfileProperty(nameof(this.Profile.VideoProfile));
		this.RegisterProfileProperty(nameof(this.Profile.VideoLevel));
		this.RegisterProfileProperty(nameof(this.Profile.VideoPreset));
		this.RegisterProfileProperty(nameof(this.Profile.VideoOptions));
		this.RegisterProfileProperty(nameof(this.Profile.TargetSize), () =>
		{
			this.RaisePropertyChanged(nameof(this.VideoBitrate));
			this.outputPathService.GenerateOutputFileName();
		});

		this.RegisterProfileProperty(nameof(this.Profile.VideoBitrate), () =>
		{
			this.RaisePropertyChanged(nameof(this.TargetSize));
			this.outputPathService.GenerateOutputFileName();
		});

		this.RegisterProfileProperty(nameof(this.Profile.VideoTunes));
	}

	private ObservableAsPropertyHelper<string> inputType;
	public string InputType => this.inputType.Value;

	private ObservableAsPropertyHelper<string> inputVideoCodec;
	public string InputVideoCodec => this.inputVideoCodec.Value;

	private ObservableAsPropertyHelper<string> inputFramerate;
	public string InputFramerate => this.inputFramerate.Value;

	private List<VideoEncoderViewModel> encoderChoices;
	public List<VideoEncoderViewModel> EncoderChoices => this.encoderChoices;

	// True if a VideoEncoder change was done by a user explicitly on the combobox.
	private bool userVideoEncoderChange;

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
				this.selectedEncoder = value;

				this.userVideoEncoderChange = true;
				this.UpdateProfileProperty(nameof(this.Profile.VideoEncoder), this.selectedEncoder.Encoder.ShortName, raisePropertyChanged: false);
				this.userVideoEncoderChange = false;

				// Refresh preset/profile/tune/level choices and values
				this.RefreshEncoderSettings(applyDefaults: false);

				this.RaisePropertyChanged();

				this.SetDefaultQuality();
			}
		}
	}

	private ObservableAsPropertyHelper<bool> x264SettingsVisible;
	public bool X264SettingsVisible => this.x264SettingsVisible.Value;

	public bool QsvSettingsVisible => HandBrakeHardwareEncoderHelper.IsQsvAvailable;

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
		set { this.UpdateProfileProperty(nameof(this.Profile.Framerate), value); }
	}

	public bool ConstantFramerate
	{
		get { return this.Profile.ConstantFramerate; }
		set { this.UpdateProfileProperty(nameof(this.Profile.ConstantFramerate), value); }
	}

	private ObservableAsPropertyHelper<string> constantFramerateToolTip;
	public string ConstantFramerateToolTip => this.constantFramerateToolTip.Value;

	private ObservableAsPropertyHelper<string> variableFramerateToolTip;
	public string VariableFramerateToolTip => this.variableFramerateToolTip.Value;

	private ObservableAsPropertyHelper<string> vfrChoiceText;
	public string VfrChoiceText => this.vfrChoiceText.Value;

	public bool MultiPass
	{
		get { return this.Profile.MultiPass; }
		set { this.UpdateProfileProperty(nameof(this.Profile.MultiPass), value); }
	}

	private ObservableAsPropertyHelper<bool> multiPassVisible;
	public bool MultiPassVisible => this.multiPassVisible.Value;

	public bool TurboFirstPass
	{
		get { return this.Profile.TurboFirstPass; }
		set { this.UpdateProfileProperty(nameof(this.Profile.TurboFirstPass), value); }
	}

	private ObservableAsPropertyHelper<bool> turboFirstPassEnabled;
	public bool TurboFirstPassEnabled => this.turboFirstPassEnabled.Value;

	private ObservableAsPropertyHelper<bool> turboFirstPassVisible;
	public bool TurboFirstPassVisible => this.turboFirstPassVisible.Value;

	public VCVideoEncodeRateType VideoEncodeRateType
	{
		get { return this.Profile.VideoEncodeRateType; }
		set { this.UpdateProfileProperty(nameof(this.Profile.VideoEncodeRateType), value); }
	}

	public double TargetSize
	{
		get
		{
                if (this.VideoEncodeRateType == VCVideoEncodeRateType.AverageBitrate)
			{
				if (this.MainViewModel.HasVideoSource && this.MainViewModel.JobCreationAvailable && this.VideoBitrate > 0)
				{
					JsonEncodeFactory factory = new JsonEncodeFactory(new StubLogger());

					double estimatedSizeBytes = factory.CalculateFileSize(this.MainViewModel.EncodeJob, this.MainViewModel.SelectedTitle.Title, this.VideoBitrate);
					this.displayTargetSize = Math.Round(estimatedSizeBytes, 1);
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
			this.UpdateProfileProperty(nameof(this.Profile.TargetSize), value);
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
					JsonEncodeFactory factory = new JsonEncodeFactory(new StubLogger());
					this.displayVideoBitrate = factory.CalculateBitrate(this.MainViewModel.EncodeJob, this.MainViewModel.SelectedTitle.Title, this.TargetSize);
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
			this.UpdateProfileProperty(nameof(this.Profile.VideoBitrate), value);
		}
	}

	public decimal Quality
	{
		get { return this.Profile.Quality; }
		set { this.UpdateProfileProperty(nameof(this.Profile.Quality), value); }
	}

	private ObservableAsPropertyHelper<double> qualityModulus;
	public double QualityModulus => this.qualityModulus.Value;

	private ObservableAsPropertyHelper<float> qualitySliderMin;
	public float QualitySliderMin => this.qualitySliderMin.Value;

	private ObservableAsPropertyHelper<float> qualitySliderMax;
	public float QualitySliderMax => this.qualitySliderMax.Value;

	private ObservableAsPropertyHelper<string> qualitySliderLeftText;
	public string QualitySliderLeftText => this.qualitySliderLeftText.Value;

	private ObservableAsPropertyHelper<string> qualitySliderRightText;
	public string QualitySliderRightText => this.qualitySliderRightText.Value;

	private ObservableAsPropertyHelper<string> qualityName;
	public string QualityName => this.qualityName.Value;

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

	private string videoProfileTempOverride;
	public string VideoProfile
	{
		get
		{
			if (this.videoProfileTempOverride != null)
			{
				return this.videoProfileTempOverride;
			}

			return this.Profile.VideoProfile;
		}
		set { this.UpdateProfileProperty(nameof(this.Profile.VideoProfile), value); }
	}

	private string videoLevelTempOverride;
	public string VideoLevel
	{
		get
		{
			if (this.videoLevelTempOverride != null)
			{
				return this.videoLevelTempOverride;
			}

			return this.Profile.VideoLevel;
		}
		set { this.UpdateProfileProperty(nameof(this.Profile.VideoLevel), value); }
	}

	private string tuneTempOverride;
	private string tune;
	public string Tune
	{
		get
		{
			if (this.tuneTempOverride != null)
			{
				return this.tuneTempOverride;
			}

			return this.tune;
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

	private List<string> presets;
	public List<string> Presets
	{
		get { return this.presets; }
		set { this.RaiseAndSetIfChanged(ref this.presets, value); }
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
			this.UpdateProfileProperty(nameof(this.Profile.VideoPreset), this.presets[value], raisePropertyChanged: false);

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
	public string PresetName => this.presetName.Value;

	private ObservableAsPropertyHelper<string> fullParameterList;
	public string FullParameterList => this.fullParameterList.Value;

	private bool updatingLocalVideoOptions;

	public string VideoOptions
	{
		get { return this.Profile.VideoOptions; }
		set
		{
			this.updatingLocalVideoOptions = true;
			this.UpdateProfileProperty(nameof(this.Profile.VideoOptions), value);
			this.updatingLocalVideoOptions = false;
		}
	}

	private enum EncoderChoicesRefreshSource
	{
		// The selected container changed, requiring the list to be rebuilt
		ContainerChange,

		// The profile changed, bringing with a new selected value
		ProfileChange
	}

	private void RefreshEncoderChoices(string containerName, EncoderChoicesRefreshSource refreshSource)
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

		this.RaisePropertyChanged(nameof(this.EncoderChoices));
		
		HBVideoEncoder targetEncoder;

		switch (refreshSource)
		{
			case EncoderChoicesRefreshSource.ContainerChange:
				targetEncoder = oldEncoder;
				break;
			case EncoderChoicesRefreshSource.ProfileChange:
				targetEncoder = HandBrakeEncoderHelpers.GetVideoEncoder(this.Profile.VideoEncoder);
				break;
			default:
				throw new ArgumentOutOfRangeException(nameof(refreshSource), refreshSource, null);
		}

		this.selectedEncoder = this.EncoderChoices.FirstOrDefault(e => e.Encoder == targetEncoder);

		if (this.selectedEncoder == null)
		{
			this.selectedEncoder = this.EncoderChoices[0];
		}

		// If it's a container change we need to commit the new encoder change.
		if (refreshSource == EncoderChoicesRefreshSource.ContainerChange && this.selectedEncoder.Encoder != oldEncoder)
		{
			this.UpdateProfileProperty(nameof(this.Profile.VideoEncoder), this.selectedEncoder.Encoder.ShortName, raisePropertyChanged: false);
			this.SetDefaultQuality();
		}

		this.RaisePropertyChanged(nameof(this.SelectedEncoder));
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

		this.RaisePropertyChanged(nameof(this.PresetMaxIndex));
		this.RaisePropertyChanged(nameof(this.PresetVisible));

		// Make sure to notify of the collection change before the index change. So that the system knows that the new value
		// is from the new collection.
		this.RaisePropertyChanged(nameof(this.ProfileChoices));
		this.RaisePropertyChanged(nameof(this.ProfileVisible));

		this.RaisePropertyChanged(nameof(this.TuneChoices));
		this.RaisePropertyChanged(nameof(this.TuneVisible));

		this.RaisePropertyChanged(nameof(this.LevelChoices));
		this.RaisePropertyChanged(nameof(this.LevelVisible));

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
		this.RaisePropertyChanged(nameof(this.PresetIndex));

		// Temporary workaround for ReactiveUI8 issue: we have to change it to another valid choice, then change back to get it to pick the correct option.
		if (this.profileChoices != null && this.profileChoices.Count > 1)
		{
			if (this.profileChoices[this.profileChoices.Count - 1].Value != this.VideoProfile)
			{
				this.videoProfileTempOverride = this.profileChoices[this.profileChoices.Count - 1].Value;
			}
			else
			{
				this.videoProfileTempOverride = this.profileChoices[this.profileChoices.Count - 2].Value;
			}

			this.RaisePropertyChanged(nameof(this.VideoProfile));
			this.videoProfileTempOverride = null;
			this.RaisePropertyChanged(nameof(this.VideoProfile));
		}

		if (this.tuneChoices != null && this.tuneChoices.Count > 1)
		{
			if (this.tuneChoices[this.tuneChoices.Count - 1].Value != this.Tune)
			{
				this.tuneTempOverride = this.tuneChoices[this.tuneChoices.Count - 1].Value;
			}
			else
			{
				this.tuneTempOverride = this.tuneChoices[this.tuneChoices.Count - 2].Value;
			}

			this.RaisePropertyChanged(nameof(this.Tune));
			this.tuneTempOverride = null;
			this.ReadTuneListFromProfile();
			this.RaisePropertyChanged(nameof(this.Tune));
		}

		if (this.levelChoices != null && this.levelChoices.Count > 1)
		{
			if (this.levelChoices[this.levelChoices.Count - 1].Value != this.VideoLevel)
			{
				this.videoLevelTempOverride = this.levelChoices[this.levelChoices.Count - 1].Value;
			}
			else
			{
				this.videoLevelTempOverride = this.levelChoices[this.levelChoices.Count - 2].Value;
			}

			this.RaisePropertyChanged(nameof(this.VideoLevel));
			this.videoLevelTempOverride = null;
			this.RaisePropertyChanged(nameof(this.VideoLevel));
		}

		this.EncodingWindowViewModel.AutomaticChange = false;
	}

	/// <summary>
	/// Re-calculates video bitrate if needed.
	/// </summary>
	public void UpdateVideoBitrate()
	{
		if (this.VideoEncodeRateType == VCVideoEncodeRateType.TargetSize)
		{
			this.RaisePropertyChanged(nameof(this.VideoBitrate));
		}
	}

	/// <summary>
	/// Re-calculates target size if needed.
	/// </summary>
	public void UpdateTargetSize()
	{
		if (this.VideoEncodeRateType == VCVideoEncodeRateType.AverageBitrate)
		{
			this.RaisePropertyChanged(nameof(this.TargetSize));
		}
	}

	private static string GetDefaultPreset(string encoderName)
	{
		if (encoderName.StartsWith("qsv", StringComparison.Ordinal))
		{
			return "balanced";
		}

		if (encoderName.StartsWith("x26", StringComparison.Ordinal) || encoderName.StartsWith("VP", StringComparison.Ordinal) || encoderName.StartsWith("nvenc", StringComparison.Ordinal))
		{
			return "medium";
		}

		if (encoderName.StartsWith("vce", StringComparison.Ordinal))
		{
			return "main";
		}

		return null;
	}

	private void NotifyAudioChanged()
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

		this.Presets = this.SelectedEncoder.Encoder.Presets;
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

	private void SetDefaultQuality()
	{
		string encoderName = this.SelectedEncoder.Encoder.ShortName;

		int quality = 22;
		if (encoderName.StartsWith("x26", StringComparison.Ordinal) || encoderName.StartsWith("qsv_h26", StringComparison.Ordinal))
		{
			quality = 22;
		}
		else if (encoderName.StartsWith("mpeg", StringComparison.Ordinal))
		{
			quality = 12;
		}
		else if (encoderName == "VP8")
		{
			quality = 10;
		}
		else if (encoderName == "VP9")
		{
			quality = 33;
		}
		else if (encoderName == "theora")
		{
			quality = 38;
		}

		this.Quality = quality;
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

		this.UpdateProfileProperty(nameof(this.Profile.VideoTunes), tunes, raisePropertyChanged: false);
	}
}
