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

	public class VideoPanelViewModel : PanelViewModel
	{
		private const int DefaultVideoBitrateKbps = 900;
		private const int DefaultTargetSizeMB = 700;

		private bool closed;

		private OutputPathViewModel outputPathVM = Unity.Container.Resolve<OutputPathViewModel>();

		private List<HBVideoEncoder> encoderChoices;

		private HBVideoEncoder selectedEncoder;
		private List<double> framerateChoices;
		private int displayTargetSize;
		private int displayVideoBitrate;

		private List<ComboChoice> x264ProfileChoices;
		private List<ComboChoice> x264PresetChoices;
		private List<ComboChoice> x264TuneChoices;

		public VideoPanelViewModel(EncodingViewModel encodingViewModel)
			: base(encodingViewModel)
		{
			this.encoderChoices = new List<HBVideoEncoder>();

			this.x264ProfileChoices = new List<ComboChoice>
			{
				new ComboChoice(null, EncodingRes.NoneX264Choice),
				new ComboChoice("baseline", EncodingRes.Profile_Baseline),
				new ComboChoice("main", EncodingRes.Profile_Main),
				new ComboChoice("high", EncodingRes.Profile_High),
			};

			this.x264PresetChoices = new List<ComboChoice>
			{
				new ComboChoice(null, EncodingRes.NoneX264Choice),
				new ComboChoice("ultrafast", EncodingRes.Preset_UltraFast),
				new ComboChoice("superfast", EncodingRes.Preset_SuperFast),
				new ComboChoice("veryfast", EncodingRes.Preset_VeryFast),
				new ComboChoice("faster", EncodingRes.Preset_Faster),
				new ComboChoice("fast", EncodingRes.Preset_Fast),
				new ComboChoice("medium", EncodingRes.Preset_Medium),
				new ComboChoice("slow", EncodingRes.Preset_Slow),
				new ComboChoice("slower", EncodingRes.Preset_Slower),
				new ComboChoice("veryslow", EncodingRes.Preset_VerySlow),
				new ComboChoice("placebo", EncodingRes.Preset_Placebo),
			};

			this.x264TuneChoices = new List<ComboChoice>
			{
				new ComboChoice(null, EncodingRes.NoneX264Choice),
				new ComboChoice("film", EncodingRes.Tune_Film),
				new ComboChoice("animation", EncodingRes.Tune_Animation),
				new ComboChoice("grain", EncodingRes.Tune_Grain),
				new ComboChoice("stillimage", EncodingRes.Tune_StillImage),
				new ComboChoice("psnr", EncodingRes.Tune_Psnr),
				new ComboChoice("ssim", EncodingRes.Tune_Ssim),
				new ComboChoice("fastdecode", EncodingRes.Tune_FastDecode),
				new ComboChoice("zerolatency", EncodingRes.Tune_ZeroLatency),
			};

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
		}

		public void OnClose()
		{
			this.closed = true;
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
						this.displayTargetSize = (int)Math.Round(this.MainViewModel.ScanInstance.CalculateFileSize(this.MainViewModel.EncodeJob, this.VideoBitrate));
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
						this.displayVideoBitrate = this.MainViewModel.ScanInstance.CalculateBitrate(this.MainViewModel.EncodeJob, this.TargetSize);
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

		public List<ComboChoice> X264PresetChoices
		{
			get
			{
				return this.x264PresetChoices;
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

		public string X264Preset
		{
			get
			{
				return this.Profile.X264Preset;
			}

			set
			{
				this.Profile.X264Preset = value;
				this.RaisePropertyChanged(() => this.X264Preset);
				this.IsModified = true;
			}
		}

		public string X264Tune
		{
			get
			{
				return this.Profile.X264Tune;
			}

			set
			{
				this.Profile.X264Tune = value;
				this.RaisePropertyChanged(() => this.X264Tune);
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
			this.RaisePropertyChanged(() => this.X264Profile);
			this.RaisePropertyChanged(() => this.X264Preset);
			this.RaisePropertyChanged(() => this.X264Tune);
			this.RaisePropertyChanged(() => this.X264SettingsVisible);
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
	}
}
