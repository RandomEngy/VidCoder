using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using HandBrake.Interop;

namespace VidCoder.ViewModel
{
	using LocalResources;
	using Model;
	using Properties;

	public class AdvancedPanelViewModel : PanelViewModel
	{
		private List<ComboChoice> x264ProfileChoices;
		private List<ComboChoice> h264LevelChoices;
		private List<ComboChoice> x264PresetChoices;
		private List<ComboChoice> x264TuneChoices;

		public AdvancedPanelViewModel(EncodingViewModel encodingViewModel) : base(encodingViewModel)
		{
			this.x264ProfileChoices = new List<ComboChoice>
			{
				new ComboChoice(null, EncodingRes.NoneX264Choice),
				new ComboChoice("baseline", EncodingRes.Profile_Baseline),
				new ComboChoice("main", EncodingRes.Profile_Main),
				new ComboChoice("high", EncodingRes.Profile_High),
				//new ComboChoice("high10", EncodingRes.Profile_High),
				//new ComboChoice("high422", EncodingRes.Profile_High),
				//new ComboChoice("high444", EncodingRes.Profile_High),
			};

			this.h264LevelChoices = new List<ComboChoice>
			{
				new ComboChoice(null, Resources.Automatic),
				new ComboChoice("1.0"),
				new ComboChoice("1b"),
				new ComboChoice("1.1"),
				new ComboChoice("1.2"),
				new ComboChoice("1.3"),
				new ComboChoice("2.0"),
				new ComboChoice("2.1"),
				new ComboChoice("2.2"),
				new ComboChoice("3.0"),
				new ComboChoice("3.1"),
				new ComboChoice("3.2"),
				new ComboChoice("4.0"),
				new ComboChoice("4.1"),
				new ComboChoice("4.2"),
				new ComboChoice("5.0"),
				new ComboChoice("5.1"),
				new ComboChoice("5.2")
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
		}

		public List<ComboChoice> X264ProfileChoices
		{
			get
			{
				return this.x264ProfileChoices;
			}
		}

		public List<ComboChoice> H264LevelChoices
		{
			get
			{
				return this.h264LevelChoices;
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

		public string AdvancedOptionsString
		{
			get
			{
				return this.Profile.X264Options;
			}

			set
			{
				this.Profile.X264Options = value;
				this.RaisePropertyChanged(() => this.AdvancedOptionsString);
				this.IsModified = true;
			}
		}

		public void NotifyAllChanged()
		{
			this.RaisePropertyChanged(() => this.X264Profile);
			this.RaisePropertyChanged(() => this.H264Level);
			this.RaisePropertyChanged(() => this.X264Preset);
			this.RaisePropertyChanged(() => this.X264Tune);
			this.RaisePropertyChanged(() => this.AdvancedOptionsString);
		}
	}
}
