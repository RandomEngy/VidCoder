using System.Collections.Generic;
using System.Globalization;
using ReactiveUI;
using VidCoder.Model;
using VidCoder.Resources;
using VidCoder.Services;
using VidCoder.ViewModel.DataModels;
using VidCoderCommon.Model;

namespace VidCoder.ViewModel
{
	public class VideoFiltersPanelViewModel : PanelViewModel
	{
		private const string CustomDenoisePreset = "custom";
		private const int MinDeblock = 5;

		private OutputSizeService outputSizeService = Ioc.Get<OutputSizeService>();

        private List<ComboChoice<VCDenoise>> denoiseChoices; 
		private List<ComboChoice> denoisePresetChoices;
		private List<ComboChoice> denoiseTuneChoices; 
		private List<RotationViewModel> rotationChoices;

		public VideoFiltersPanelViewModel(EncodingWindowViewModel encodingWindowViewModel)
			: base(encodingWindowViewModel)
		{
			this.AutomaticChange = true;

			this.RegisterProfileProperties();

            this.denoiseChoices = new List<ComboChoice<VCDenoise>>
            {
                new ComboChoice<VCDenoise>(VCDenoise.Off, CommonRes.Off),
                new ComboChoice<VCDenoise>(VCDenoise.hqdn3d, EnumsRes.Denoise_HQDN3D),
                new ComboChoice<VCDenoise>(VCDenoise.NLMeans, EnumsRes.Denoise_NLMeans),
            };

			// Right now both hqdn3d and NL-Means have the same preset choices.
			this.denoisePresetChoices = new List<ComboChoice>
			{
				new ComboChoice("ultralight", EnumsRes.DenoisePreset_Ultralight),
				new ComboChoice("light", EnumsRes.DenoisePreset_Light),
				new ComboChoice("medium", EnumsRes.DenoisePreset_Medium),
				new ComboChoice("strong", EnumsRes.DenoisePreset_Strong),
				new ComboChoice("custom", CommonRes.Custom),
			};

			this.denoiseTuneChoices = new List<ComboChoice>
			{
				new ComboChoice("none", CommonRes.None),
				new ComboChoice("film", EnumsRes.DenoiseTune_Film),
				new ComboChoice("grain", EnumsRes.DenoiseTune_Grain),
				new ComboChoice("highmotion", EnumsRes.DenoiseTune_HighMotion),
				new ComboChoice("animation", EnumsRes.DenoiseTune_Animation),
			};

			this.rotationChoices = new List<RotationViewModel>
			{
				new RotationViewModel { Rotation = VCPictureRotation.None, Display = CommonRes.None },
				new RotationViewModel { Rotation = VCPictureRotation.Clockwise90, Display = EncodingRes.Rotation_Clockwise90, Image = "/Icons/rotate_90_cw.png"},
				new RotationViewModel { Rotation = VCPictureRotation.Clockwise270, Display = EncodingRes.Rotation_Counterclockwise90, Image = "/Icons/rotate_90_ccw.png" },
				new RotationViewModel { Rotation = VCPictureRotation.Clockwise180, Display = EncodingRes.Rotation_180, Image = "/Icons/rotate_180.png" }
			};

			// CustomDetelecineVisible
			this.WhenAnyValue(x => x.Detelecine, detelecine =>
			{
				return detelecine == VCDetelecine.Custom;
			}).ToProperty(this, x => x.CustomDetelecineVisible, out this.customDetelecineVisible);

			// CustomDeinterlaceVisible
			this.WhenAnyValue(x => x.Deinterlace, deinterlace =>
			{
				return deinterlace == VCDeinterlace.Custom;
			}).ToProperty(this, x => x.CustomDeinterlaceVisible, out this.customDeinterlaceVisible);

			// CustomDecombVisible
			this.WhenAnyValue(x => x.Decomb, decomb =>
			{
				return decomb == VCDecomb.Custom;
			}).ToProperty(this, x => x.CustomDecombVisible, out this.customDecombVisible);

			// DenoisePresetVisible
			this.WhenAnyValue(x => x.DenoiseType, denoise =>
			{
				return denoise != VCDenoise.Off;
			}).ToProperty(this, x => x.DenoisePresetVisible, out this.denoisePresetVisible);

			// DenoiseTuneVisible
			this.WhenAnyValue(x => x.DenoiseType, x => x.Profile.UseCustomDenoise, (denoise, useCustomDenoise) =>
			{
				return denoise == VCDenoise.NLMeans && !useCustomDenoise;
			}).ToProperty(this, x => x.DenoiseTuneVisible, out this.denoiseTuneVisible);

			// CustomDenoiseVisible
			this.WhenAnyValue(x => x.DenoiseType, x => x.Profile.UseCustomDenoise, (denoise, useCustomDenoise) =>
			{
				return denoise != VCDenoise.Off && useCustomDenoise;
			}).ToProperty(this, x => x.CustomDenoiseVisible, out this.customDenoiseVisible);

			// DeblockText
			this.WhenAnyValue(x => x.Deblock, deblock =>
			{
				if (deblock >= MinDeblock)
				{
					return deblock.ToString(CultureInfo.CurrentCulture);
				}

				return CommonRes.Off;
			}).ToProperty(this, x => x.DeblockText, out this.deblockText);

			this.AutomaticChange = false;
		}

		private void RegisterProfileProperties()
		{
			this.RegisterProfileProperty(nameof(this.Detelecine));
			this.RegisterProfileProperty(nameof(this.CustomDetelecine));

			this.RegisterProfileProperty(nameof(this.Deinterlace), () =>
			{
				if (this.Deinterlace != VCDeinterlace.Off)
				{
					this.Decomb = VCDecomb.Off;
				}
			});
			this.RegisterProfileProperty(nameof(this.CustomDeinterlace));

			this.RegisterProfileProperty(nameof(this.Decomb), () =>
			{
				if (this.Decomb != VCDecomb.Off)
				{
					this.Deinterlace = VCDeinterlace.Off;
				}
			});
			this.RegisterProfileProperty(nameof(this.CustomDecomb));
			
			this.RegisterProfileProperty(nameof(this.DenoiseType), () =>
			{
				if (this.DenoiseType != VCDenoise.Off && string.IsNullOrEmpty(this.DenoisePreset))
				{
					this.DenoisePreset = "medium";
				}

				if (this.DenoiseType == VCDenoise.NLMeans && string.IsNullOrEmpty(this.DenoiseTune))
				{
					this.DenoiseTune = "none";
				}
			});

			this.RegisterProfileProperty(nameof(this.DenoisePreset));
			this.RegisterProfileProperty(nameof(this.DenoiseTune));
			this.RegisterProfileProperty(nameof(this.CustomDenoise));
			this.RegisterProfileProperty(nameof(this.Deblock));
			this.RegisterProfileProperty(nameof(this.Grayscale));
			this.RegisterProfileProperty(nameof(this.Rotation), () => this.outputSizeService.Refresh());
			this.RegisterProfileProperty(nameof(this.FlipHorizontal));
			this.RegisterProfileProperty(nameof(this.FlipVertical));
		}

		public VCDetelecine Detelecine
		{
			get { return this.Profile.Detelecine; }
			set { this.UpdateProfileProperty(nameof(this.Profile.Detelecine), value); }
		}

		public string CustomDetelecine
		{
			get { return this.Profile.CustomDetelecine; }
			set { this.UpdateProfileProperty(nameof(this.Profile.CustomDetelecine), value); }
		}

		private ObservableAsPropertyHelper<bool> customDetelecineVisible;
		public bool CustomDetelecineVisible => this.customDetelecineVisible.Value;

		public VCDeinterlace Deinterlace
		{
			get { return this.Profile.Deinterlace; }
			set { this.UpdateProfileProperty(nameof(this.Profile.Deinterlace), value); }
		}

		public string CustomDeinterlace
		{
			get { return this.Profile.CustomDeinterlace; }
			set { this.UpdateProfileProperty(nameof(this.Profile.CustomDeinterlace), value); }
		}

		private ObservableAsPropertyHelper<bool> customDeinterlaceVisible;
		public bool CustomDeinterlaceVisible => this.customDeinterlaceVisible.Value;

		public VCDecomb Decomb
		{
			get { return this.Profile.Decomb; }
			set { this.UpdateProfileProperty(nameof(this.Profile.Decomb), value); }
		}

		public string CustomDecomb
		{
			get { return this.Profile.CustomDecomb; }
			set { this.UpdateProfileProperty(nameof(this.Profile.CustomDecomb), value); }
		}

		private ObservableAsPropertyHelper<bool> customDecombVisible;
		public bool CustomDecombVisible => this.customDecombVisible.Value;

		public List<ComboChoice<VCDenoise>> DenoiseChoices
	    {
	        get { return this.denoiseChoices; }
	    }

		public VCDenoise DenoiseType
		{
			get { return this.Profile.DenoiseType; }
			set { this.UpdateProfileProperty(nameof(this.Profile.DenoiseType), value); }
		}

		public List<ComboChoice> DenoisePresetChoices
		{
			get
			{
				return this.denoisePresetChoices;
			}
		} 

		public string DenoisePreset
		{
			get
			{
				if (this.Profile.UseCustomDenoise)
				{
					return CustomDenoisePreset;
				}

				return this.Profile.DenoisePreset;
			}

			set
			{
				this.UpdateProfileProperties(
					() => this.Profile,
					(profile, newValue) =>
					{
						if (newValue == CustomDenoisePreset)
						{
							this.Profile.DenoisePreset = null;
							this.Profile.UseCustomDenoise = true;
						}
						else
						{
							this.Profile.DenoisePreset = newValue;
							this.Profile.UseCustomDenoise = false;
						}
					},
					nameof(this.DenoisePreset),
					value);
			}
		}

		public bool UseCustomDenoise
		{
			get { return this.Profile.UseCustomDenoise; }
			set { this.UpdateProfileProperty(nameof(this.Profile.UseCustomDenoise), value); }
		}

		private ObservableAsPropertyHelper<bool> denoisePresetVisible;
		public bool DenoisePresetVisible => this.denoisePresetVisible.Value;

		public List<ComboChoice> DenoiseTuneChoices
		{
			get
			{
				return this.denoiseTuneChoices;
			}
		}

		public string DenoiseTune
		{
			get { return this.Profile.DenoiseTune; }
			set { this.UpdateProfileProperty(nameof(this.Profile.DenoiseTune), value); }
		}

		private ObservableAsPropertyHelper<bool> denoiseTuneVisible;
		public bool DenoiseTuneVisible => this.denoiseTuneVisible.Value;

		public string CustomDenoise
		{
			get { return this.Profile.CustomDenoise; }
			set { this.UpdateProfileProperty(nameof(this.Profile.CustomDenoise), value); }
		}

		private ObservableAsPropertyHelper<bool> customDenoiseVisible;
		public bool CustomDenoiseVisible => this.customDenoiseVisible.Value;

		public int Deblock
		{
			get { return this.Profile.Deblock; }
			set { this.UpdateProfileProperty(nameof(this.Profile.Deblock), value); }
		}

		private ObservableAsPropertyHelper<string> deblockText;
		public string DeblockText => this.deblockText.Value;

		public bool Grayscale
		{
			get { return this.Profile.Grayscale; }
			set { this.UpdateProfileProperty(nameof(this.Profile.Grayscale), value); }
		}

		public List<RotationViewModel> RotationChoices
		{
			get
			{
				return this.rotationChoices;
			}
		}

		public VCPictureRotation Rotation
		{
			get { return this.Profile.Rotation; }
			set { this.UpdateProfileProperty(nameof(this.Profile.Rotation), value); }
		}

		public bool FlipHorizontal
		{
			get { return this.Profile.FlipHorizontal; }
			set { this.UpdateProfileProperty(nameof(this.Profile.FlipHorizontal), value); }
		}

		public bool FlipVertical
		{
			get { return this.Profile.FlipVertical; }
			set { this.UpdateProfileProperty(nameof(this.Profile.FlipVertical), value); }
		}
	}
}
