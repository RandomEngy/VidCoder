using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using GalaSoft.MvvmLight.Messaging;
using HandBrake.Interop;
using HandBrake.Interop.Model;
using HandBrake.Interop.Model.Encoding;
using Microsoft.Practices.Unity;
using VidCoder.Messages;
using VidCoder.Model;
using VidCoder.ViewModel.Components;

namespace VidCoder.ViewModel
{
	using LocalResources;
	using Properties;

	public class VideoPanelViewModel : PanelViewModel
	{
		private const int DefaultVideoBitrateKbps = 900;
		private const int DefaultTargetSizeMB = 700;

		private OutputPathViewModel outputPathVM = Unity.Container.Resolve<OutputPathViewModel>();

		private List<HBVideoEncoder> encoderChoices;

		private HBVideoEncoder selectedEncoder;
		private List<double> framerateChoices;
		private int displayTargetSize;
		private int displayVideoBitrate;

		private List<string> x264Presets;
		private List<ComboChoice> x264ProfileChoices;
		private List<LevelChoiceViewModel> h264LevelChoices;
		private List<ComboChoice> x264TuneChoices;

		public VideoPanelViewModel(EncodingViewModel encodingViewModel)
			: base(encodingViewModel)
		{
			this.encoderChoices = new List<HBVideoEncoder>();

			this.framerateChoices = new List<double>
			{
				0,
				5,
				10,
				12,
				15,
				23.976,
				24,
				25,
				29.97,
				30,
				50,
				59.94,
				60
			};

			this.x264Presets = new List<string>
			{
				"ultrafast",
				"superfast",
				"veryfast",
				"faster",
				"fast",
				"medium",
				"slow",
				"slower",
				"veryslow",
				"placebo"
			};

			this.x264ProfileChoices = new List<ComboChoice>
			{
				new ComboChoice(null, Resources.Automatic),
				new ComboChoice("baseline", EncodingRes.Profile_Baseline),
				new ComboChoice("main", EncodingRes.Profile_Main),
				new ComboChoice("high", EncodingRes.Profile_High),
				//new ComboChoice("high10", EncodingRes.Profile_High),
				//new ComboChoice("high422", EncodingRes.Profile_High),
				new ComboChoice("high444", EncodingRes.Profile_High444),
			};

			this.h264LevelChoices = new List<LevelChoiceViewModel>
			{
				new LevelChoiceViewModel(null, Resources.Automatic),
				new LevelChoiceViewModel("1.0"),
				new LevelChoiceViewModel("1b"),
				new LevelChoiceViewModel("1.1"),
				new LevelChoiceViewModel("1.2"),
				new LevelChoiceViewModel("1.3"),
				new LevelChoiceViewModel("2.0"),
				new LevelChoiceViewModel("2.1"),
				new LevelChoiceViewModel("2.2"),
				new LevelChoiceViewModel("3.0"),
				new LevelChoiceViewModel("3.1"),
				new LevelChoiceViewModel("3.2"),
				new LevelChoiceViewModel("4.0"),
				new LevelChoiceViewModel("4.1"),
				new LevelChoiceViewModel("4.2"),
				new LevelChoiceViewModel("5.0"),
				new LevelChoiceViewModel("5.1"),
				new LevelChoiceViewModel("5.2")
			};

			this.x264TuneChoices = new List<ComboChoice>
			{
				new ComboChoice(null, Resources.None),
				new ComboChoice("film", EncodingRes.Tune_Film),
				new ComboChoice("animation", EncodingRes.Tune_Animation),
				new ComboChoice("grain", EncodingRes.Tune_Grain),
				new ComboChoice("stillimage", EncodingRes.Tune_StillImage),
				new ComboChoice("psnr", EncodingRes.Tune_Psnr),
				new ComboChoice("ssim", EncodingRes.Tune_Ssim),
				//new ComboChoice("fastdecode", EncodingRes.Tune_FastDecode),
				//new ComboChoice("zerolatency", EncodingRes.Tune_ZeroLatency),
			};

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
		}

		public string InputType
		{
			get
			{
				if (this.HasSourceData)
				{
					return DisplayConversions.DisplayInputType(this.SelectedTitle.InputType);
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
					return DisplayConversions.DisplayVideoCodecName(this.SelectedTitle.VideoCodecName);
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
					return string.Format(EncodingRes.FpsFormat, this.SelectedTitle.Framerate);
				}

				return string.Empty;
			}
		}

		public List<HBVideoEncoder> EncoderChoices
		{
			get
			{
				return this.encoderChoices;
			}
		}

		public HBVideoEncoder SelectedEncoder
		{
			get
			{
				return this.selectedEncoder;
			}

			set
			{
				if (value != null && value != this.selectedEncoder)
				{
					HBVideoEncoder oldEncoder = this.selectedEncoder;

					this.selectedEncoder = value;
					this.Profile.VideoEncoder = this.selectedEncoder.ShortName;
					this.RaisePropertyChanged(() => this.SelectedEncoder);
					this.RaisePropertyChanged(() => this.X264SettingsVisible);
					this.RaisePropertyChanged(() => this.QualitySliderMin);
					this.RaisePropertyChanged(() => this.QualitySliderMax);
					this.RaisePropertyChanged(() => this.QualitySliderLeftText);
					this.RaisePropertyChanged(() => this.QualitySliderRightText);
					this.IsModified = true;

					// Move the quality number to something equivalent for the new encoder.
					if (oldEncoder != null)
					{
						double oldQualityFraction = 0.0;

						switch (oldEncoder.ShortName)
						{
							case "x264":
								oldQualityFraction = 1.0 - this.Quality / 51.0;
								break;
							case "ffmpeg4":
							case "ffmpeg2":
								oldQualityFraction = 1.0 - this.Quality / 31.0;
								break;
							case "theora":
								oldQualityFraction = this.Quality / 63.0;
								break;
							default:
								throw new InvalidOperationException("Unrecognized encoder.");
						}

						switch (value.ShortName)
						{
							case "x264":
								this.Quality = Math.Round((1.0 - oldQualityFraction) * 51.0);
								break;
							case "ffmpeg4":
							case "ffmpeg2":
								this.Quality = Math.Max(1.0, Math.Round((1.0 - oldQualityFraction) * 31.0));
								break;
							case "theora":
								this.Quality = Math.Round(oldQualityFraction * 63);
								break;
							default:
								throw new InvalidOperationException("Unrecognized encoder.");
						}
					}
				}
			}
		}

		public bool X264SettingsVisible
		{
			get
			{
				return this.SelectedEncoder.ShortName == "x264";
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
				return this.VideoEncodeRateType != VideoEncodeRateType.ConstantQuality;
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
				return this.VideoEncodeRateType != VideoEncodeRateType.ConstantQuality && this.TwoPassEncoding;
			}
		}

		public VideoEncodeRateType VideoEncodeRateType
		{
			get
			{
				return this.Profile.VideoEncodeRateType;
			}

			set
			{
				VideoEncodeRateType oldRateType = this.Profile.VideoEncodeRateType;

				this.Profile.VideoEncodeRateType = value;
				this.RaisePropertyChanged(() => this.VideoEncodeRateType);

				if (value == VideoEncodeRateType.ConstantQuality)
				{
					// Set up a default quality.
					switch (this.SelectedEncoder.ShortName)
					{
						case "x264":
							this.Quality = 20;
							break;
						case "ffmpeg4":
						case "ffmpeg2":
							this.Quality = 12;
							break;
						case "theora":
							this.Quality = 38;
							break;
						default:
							break;
					}

					// Disable two-pass options
					this.Profile.TwoPass = false;
					this.Profile.TurboFirstPass = false;
					this.RaisePropertyChanged(() => this.TwoPassEncoding);
					this.RaisePropertyChanged(() => this.TurboFirstPass);
				}

				this.RaisePropertyChanged(() => this.TwoPassEncodingEnabled);
				this.RaisePropertyChanged(() => this.TurboFirstPassEnabled);

				if (value == VideoEncodeRateType.AverageBitrate)
				{
					if (oldRateType == VideoEncodeRateType.ConstantQuality)
					{
						if (this.Profile.VideoBitrate == 0)
						{
							this.VideoBitrate = DefaultVideoBitrateKbps;
						}
					}
					else if (oldRateType == VideoEncodeRateType.TargetSize)
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

				if (value == VideoEncodeRateType.TargetSize)
				{
					if (oldRateType == VideoEncodeRateType.ConstantQuality)
					{
						if (this.Profile.TargetSize == 0)
						{
							this.TargetSize = DefaultTargetSizeMB;
						}
					}
					else if (oldRateType == VideoEncodeRateType.AverageBitrate)
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

				this.outputPathVM.GenerateOutputFileName();
				this.IsModified = true;
			}
		}

		public int TargetSize
		{
			get
			{
				if (this.VideoEncodeRateType == VideoEncodeRateType.AverageBitrate)
				{
					if (this.MainViewModel.HasVideoSource && this.MainViewModel.JobCreationAvailable && this.VideoBitrate > 0)
					{
						this.displayTargetSize = (int)Math.Round(this.MainViewModel.ScanInstance.CalculateFileSize(this.MainViewModel.EncodeJob.HbJob, this.VideoBitrate));
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
				this.outputPathVM.GenerateOutputFileName();
				this.IsModified = true;
			}
		}

		public int VideoBitrate
		{
			get
			{
				if (this.VideoEncodeRateType == VideoEncodeRateType.ConstantQuality)
				{
					return DefaultVideoBitrateKbps;
				}

				if (this.VideoEncodeRateType == VideoEncodeRateType.TargetSize)
				{
					if (this.MainViewModel.HasVideoSource && this.MainViewModel.JobCreationAvailable && this.TargetSize > 0)
					{
						this.displayVideoBitrate = this.MainViewModel.ScanInstance.CalculateBitrate(this.MainViewModel.EncodeJob.HbJob, this.TargetSize);
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
				this.outputPathVM.GenerateOutputFileName();
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
				this.outputPathVM.GenerateOutputFileName();
				this.IsModified = true;
			}
		}

		public int QualitySliderMin
		{
			get
			{
				switch (this.SelectedEncoder.ShortName)
				{
					case "x264":
						return 0;
					case "ffmpeg4":
					case "ffmpeg2":
						return 1;
					case "theora":
						return 0;
					default:
						return 0;
				}
			}
		}

		public int QualitySliderMax
		{
			get
			{
				switch (this.SelectedEncoder.ShortName)
				{
					case "x264":
						return 51;
					case "ffmpeg4":
					case "ffmpeg2":
						return 31;
					case "theora":
						return 63;
					default:
						return 0;
				}
			}
		}

		public string QualitySliderLeftText
		{
			get
			{
				switch (this.SelectedEncoder.ShortName)
				{
					case "x264":
					case "ffmpeg4":
					case "ffmpeg2":
						return EncodingRes.HighQuality;
					case "theora":
						return EncodingRes.LowQuality;
					default:
						return string.Empty;
				}
			}
		}

		public string QualitySliderRightText
		{
			get
			{
				switch (this.SelectedEncoder.ShortName)
				{
					case "x264":
					case "ffmpeg4":
					case "ffmpeg2":
						return EncodingRes.LowQuality;
					case "theora":
						return EncodingRes.HighQuality;
					default:
						return string.Empty;
				}
			}
		}

		public List<ComboChoice> X264ProfileChoices
		{
			get
			{
				return this.x264ProfileChoices;
			}
		}

		public List<LevelChoiceViewModel> H264LevelChoices
		{
			get
			{
				return this.h264LevelChoices;
			}
		}

		public List<ComboChoice> X264TuneChoices
		{
			get
			{
				return this.x264TuneChoices;
			}
		}

		public string X264Profile
		{
			get
			{
				return this.Profile.X264Profile;
			}

			set
			{
				this.Profile.X264Profile = value;
				this.RaisePropertyChanged(() => this.X264Profile);
				this.IsModified = true;
			}
		}

		public string H264Level
		{
			get
			{
				return this.Profile.H264Level;
			}

			set
			{
				this.Profile.H264Level = value;
				this.RaisePropertyChanged(() => this.H264Level);
				this.IsModified = true;
			}
		}

		private string x264Tune;
		public string X264Tune
		{
			get
			{
				return x264Tune;
				//return this.Profile.X264Tune;
			}

			set
			{
				//this.Profile.X264Tune = value;
				this.x264Tune = value;
				this.WriteTuneListToProfile();
				this.RaisePropertyChanged(() => this.X264Tune);
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

		private bool zeroLatency;
		public bool ZeroLatency
		{
			get
			{
				return this.zeroLatency;
			}

			set
			{
				this.zeroLatency = value;
				this.WriteTuneListToProfile();
				this.RaisePropertyChanged(() => this.ZeroLatency);
				this.IsModified = true;
			}
		}

		public int X264PresetIndex
		{
			get
			{
				string preset = this.Profile.X264Preset;
				if (string.IsNullOrWhiteSpace(preset))
				{
					preset = "medium";
				}

				return this.x264Presets.IndexOf(preset);
			}

			set
			{
				this.Profile.X264Preset = this.GetX264PresetFromIndex(value);
				this.RaisePropertyChanged(() => this.X264PresetIndex);
				this.RaisePropertyChanged(() => this.X264PresetName);
				this.IsModified = true;
			}
		}

		public string X264PresetName
		{
			get
			{
				switch (this.Profile.X264Preset)
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
					default:
						return EncodingRes.Preset_Medium;
				}
			}
		}

		public string X264AdditionalOptions
		{
			get
			{
				return this.Profile.X264Options;
			}

			set
			{
				this.Profile.X264Options = value;

				// Property notification will happen in response to the message
				Messenger.Default.Send(new AdvancedOptionsChangedMessage());
				this.IsModified = true;
			}
		}

		public void NotifyOutputFormatChanged(Container outputFormat)
		{
			// Refresh encoder choices based on output format change
			HBVideoEncoder oldEncoder = this.selectedEncoder;

			this.RefreshEncoderChoices(outputFormat);

			if (this.EncoderChoices.Contains(oldEncoder))
			{
				// Review: is this needed to set this?
				this.SelectedEncoder = oldEncoder;
			}
			else
			{
				this.SelectedEncoder = this.EncoderChoices[0];
			}
		}

		private void RefreshEncoderChoices(Container outputFormat)
		{
			this.encoderChoices = new List<HBVideoEncoder>();

			foreach (HBVideoEncoder encoder in Encoders.VideoEncoders)
			{
				if ((encoder.CompatibleContainers & outputFormat) > 0)
				{
					this.EncoderChoices.Add(encoder);
				}
			}

			this.RaisePropertyChanged(() => this.EncoderChoices);
		}

		public void NotifyProfileChanged()
		{
			this.RefreshEncoderChoices(this.Profile.OutputFormat);

			this.selectedEncoder = this.EncoderChoices.Single(e => e.ShortName == this.Profile.VideoEncoder);
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
			this.RaisePropertyChanged(() => this.VideoEncodeRateType);
			this.RaisePropertyChanged(() => this.TargetSize);
			this.RaisePropertyChanged(() => this.VideoBitrate);
			this.RaisePropertyChanged(() => this.Quality);
			this.RaisePropertyChanged(() => this.QualitySliderMin);
			this.RaisePropertyChanged(() => this.QualitySliderMax);
			this.RaisePropertyChanged(() => this.QualitySliderLeftText);
			this.RaisePropertyChanged(() => this.QualitySliderRightText);
			this.RaisePropertyChanged(() => this.X264SettingsVisible);
			this.RaisePropertyChanged(() => this.X264PresetIndex);
			this.RaisePropertyChanged(() => this.X264PresetName);
			this.RaisePropertyChanged(() => this.X264Profile);
			this.RaisePropertyChanged(() => this.H264Level);
			this.RaisePropertyChanged(() => this.X264Tune);
			this.RaisePropertyChanged(() => this.X264AdditionalOptions);
		}

		/// <summary>
		/// Re-calculates video bitrate if needed.
		/// </summary>
		public void UpdateVideoBitrate()
		{
			if (this.VideoEncodeRateType == VideoEncodeRateType.TargetSize)
			{
				this.RaisePropertyChanged(() => this.VideoBitrate);
			}
		}

		/// <summary>
		/// Re-calculates target size if needed.
		/// </summary>
		public void UpdateTargetSize()
		{
			if (this.VideoEncodeRateType == VideoEncodeRateType.AverageBitrate)
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

		private void NotifyAudioInputChanged()
		{
			this.UpdateVideoBitrate();
			this.UpdateTargetSize();
		}

		private void RefreshLevelCompatibility()
		{
			foreach (LevelChoiceViewModel levelChoice in this.H264LevelChoices)
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
							fpsNumerator = main.SelectedTitle.FramerateNumerator;
							fpsDenominator = main.SelectedTitle.FramerateDenominator;
						}
						else
						{
							fpsNumerator = 27000000;
							fpsDenominator = HandBrake.Interop.Converters.FramerateToVrate(this.Profile.Framerate);
						}

						bool interlaced = false;
						bool fakeInterlaced = false;

						Dictionary<string, string> advancedOptions = AdvancedOptionsParsing.ParseOptions(this.Profile.X264Options);
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

		private void ReadTuneListFromProfile()
		{
			this.x264Tune = null;
			this.fastDecode = false;
			this.zeroLatency = false;

			if (this.Profile.X264Tunes != null)
			{
				this.ReadTuneList(this.Profile.X264Tunes);
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
						this.zeroLatency = true;
						break;
					default:
						this.x264Tune = tune;
						break;
				}
			}
		}

		private void WriteTuneListToProfile()
		{
			var tunes = new List<string>();
			if (this.x264Tune != null)
			{
				tunes.Add(this.x264Tune);
			}

			if (this.fastDecode)
			{
				tunes.Add("fastdecode");
			}

			if (this.zeroLatency)
			{
				tunes.Add("zerolatency");
			}

			this.Profile.X264Tunes = tunes;
		}

		private string GetX264PresetFromIndex(int index)
		{
			return this.x264Presets[index];
		}
	}
}
