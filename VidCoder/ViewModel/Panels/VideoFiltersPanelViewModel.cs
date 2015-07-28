using System.Collections.Generic;
using GalaSoft.MvvmLight.Messaging;
using VidCoder.Messages;
using VidCoder.Model;
using VidCoder.Resources;
using VidCoder.Services;
using VidCoder.ViewModel.DataModels;
using VidCoderCommon.Model;

namespace VidCoder.ViewModel
{
	public class VideoFiltersPanelViewModel : PanelViewModelOld
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
		}

		public VCDetelecine SelectedDetelecine
		{
			get
			{
			    return this.Profile.Detelecine;
			}

			set
			{
			    this.Profile.Detelecine = value;
				this.RaisePropertyChanged(() => this.SelectedDetelecine);
				this.RaisePropertyChanged(() => this.CustomDetelecineVisible);
				this.IsModified = true;
			}
		}

		public string CustomDetelecine
		{
			get
			{
				return this.Profile.CustomDetelecine;
			}

			set
			{
				this.Profile.CustomDetelecine = value;
				this.RaisePropertyChanged(() => this.CustomDetelecine);
				this.IsModified = true;
			}
		}

		public bool CustomDetelecineVisible
		{
			get
			{
				return this.Profile.Detelecine == VCDetelecine.Custom;
			}
		}

		public VCDeinterlace SelectedDeinterlace
		{
			get
			{
			    return this.Profile.Deinterlace;
			}

			set
			{
			    this.Profile.Deinterlace = value;
                if (value != VCDeinterlace.Off)
				{
					this.SelectedDecomb = VCDecomb.Off;
				}

				this.RaisePropertyChanged(() => this.SelectedDeinterlace);
				this.RaisePropertyChanged(() => this.CustomDeinterlaceVisible);

				this.IsModified = true;
			}
		}

		public string CustomDeinterlace
		{
			get
			{
				return this.Profile.CustomDeinterlace;
			}

			set
			{
				this.Profile.CustomDeinterlace = value;
				this.RaisePropertyChanged(() => this.CustomDeinterlace);
				this.IsModified = true;
			}
		}

		public bool CustomDeinterlaceVisible
		{
			get
			{
                return this.Profile.Deinterlace == VCDeinterlace.Custom;
			}
		}

		public VCDecomb SelectedDecomb
		{
			get
			{
			    return this.Profile.Decomb;
			}

			set
			{
				this.Profile.Decomb = value;
                if (value != VCDecomb.Off)
				{
					this.SelectedDeinterlace = VCDeinterlace.Off;
				}

				this.RaisePropertyChanged(() => this.SelectedDecomb);
				this.RaisePropertyChanged(() => this.CustomDecombVisible);
				this.IsModified = true;
			}
		}

		public string CustomDecomb
		{
			get
			{
				return this.Profile.CustomDecomb;
			}

			set
			{
				this.Profile.CustomDecomb = value;
				this.RaisePropertyChanged(() => this.CustomDecomb);
				this.IsModified = true;
			}
		}

		public bool CustomDecombVisible
		{
			get
			{
                return this.Profile.Decomb == VCDecomb.Custom;
			}
		}

	    public List<ComboChoice<VCDenoise>> DenoiseChoices
	    {
	        get { return this.denoiseChoices; }
	    } 

		public VCDenoise SelectedDenoise
		{
			get
			{
			    return this.Profile.DenoiseType;
			}

			set
			{
			    this.Profile.DenoiseType = value;

				if (value != VCDenoise.Off && string.IsNullOrEmpty(this.DenoisePreset))
				{
					this.DenoisePreset = "medium";
				}

                if (value == VCDenoise.NLMeans && string.IsNullOrEmpty(this.DenoiseTune))
				{
					this.DenoiseTune = "none";
				}

				this.RaisePropertyChanged(() => this.SelectedDenoise);
				this.RaisePropertyChanged(() => this.DenoisePresetVisible);
				this.RaisePropertyChanged(() => this.DenoiseTuneVisible);
				this.RaisePropertyChanged(() => this.CustomDenoiseVisible);
				this.IsModified = true;
			}
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
				if (value == CustomDenoisePreset)
				{
					this.Profile.DenoisePreset = null;
					this.Profile.UseCustomDenoise = true;
				}
				else
				{
					this.Profile.DenoisePreset = value;
					this.Profile.UseCustomDenoise = false;
				}

				this.RaisePropertyChanged(() => this.DenoisePreset);
				this.RaisePropertyChanged(() => this.CustomDenoiseVisible);
				this.RaisePropertyChanged(() => this.DenoiseTuneVisible);
				this.IsModified = true;
			}
		}

		public bool DenoisePresetVisible
		{
			get
			{
				return this.SelectedDenoise != VCDenoise.Off;
			}
		}

		public List<ComboChoice> DenoiseTuneChoices
		{
			get
			{
				return this.denoiseTuneChoices;
			}
		} 

		public string DenoiseTune
		{
			get
			{
				return this.Profile.DenoiseTune;
			}

			set
			{
				this.Profile.DenoiseTune = value;
				this.RaisePropertyChanged(() => this.DenoiseTune);
				this.IsModified = true;
			}
		}

		public bool DenoiseTuneVisible
		{
			get
			{
				return this.SelectedDenoise == VCDenoise.NLMeans && !this.Profile.UseCustomDenoise;
			}
		}

		public string CustomDenoise
		{
			get
			{
				return this.Profile.CustomDenoise;
			}

			set
			{
				this.Profile.CustomDenoise = value;
				this.RaisePropertyChanged(() => this.CustomDenoise);
				this.IsModified = true;
			}
		}

		public bool CustomDenoiseVisible
		{
			get
			{
				return this.Profile.DenoiseType != VCDenoise.Off && this.Profile.UseCustomDenoise;
			}
		}

		public int Deblock
		{
			get
			{
				if (this.Profile.Deblock >= MinDeblock)
				{
					return this.Profile.Deblock;
				}

				return MinDeblock - 1;
			}

			set
			{
				if (value < MinDeblock)
				{
					this.Profile.Deblock = 0;
				}
				else
				{
					this.Profile.Deblock = value;
				}

				this.RaisePropertyChanged(() => this.Deblock);
				this.RaisePropertyChanged(() => this.DeblockText);
				this.IsModified = true;
			}
		}

		public string DeblockText
		{
			get
			{
				if (this.Deblock >= MinDeblock)
				{
					return this.Deblock.ToString();
				}

				return CommonRes.Off;
			}
		}

		public bool Grayscale
		{
			get
			{
				return this.Profile.Grayscale;
			}

			set
			{
				this.Profile.Grayscale = value;
				this.RaisePropertyChanged(() => this.Grayscale);
				this.IsModified = true;
			}
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
			get
			{
				return this.Profile.Rotation;
			}

			set
			{
				this.Profile.Rotation = value;
				this.RaisePropertyChanged(() => this.Rotation);
				this.IsModified = true;
				this.NotifyRotationChanged();
			}
		}

		public bool FlipHorizontal
		{
			get
			{
				return this.Profile.FlipHorizontal;
			}

			set
			{
				this.Profile.FlipHorizontal = value;
				this.RaisePropertyChanged(() => this.FlipHorizontal);
				this.IsModified = true;
				this.NotifyRotationChanged();
			}
		}

		public bool FlipVertical
		{
			get
			{
				return this.Profile.FlipVertical;
			}

			set
			{
				this.Profile.FlipVertical = value;
				this.RaisePropertyChanged(() => this.FlipVertical);
				this.IsModified = true;
				this.NotifyRotationChanged();
			}
		}

		public void NotifyAllChanged()
		{
			this.RaisePropertyChanged(() => this.SelectedDetelecine);
			this.RaisePropertyChanged(() => this.CustomDetelecine);
			this.RaisePropertyChanged(() => this.CustomDetelecineVisible);
			this.RaisePropertyChanged(() => this.SelectedDeinterlace);
			this.RaisePropertyChanged(() => this.CustomDeinterlace);
			this.RaisePropertyChanged(() => this.CustomDeinterlaceVisible);
			this.RaisePropertyChanged(() => this.SelectedDecomb);
			this.RaisePropertyChanged(() => this.CustomDecomb);
			this.RaisePropertyChanged(() => this.CustomDecombVisible);
			this.RaisePropertyChanged(() => this.SelectedDenoise);
			this.RaisePropertyChanged(() => this.DenoisePreset);
			this.RaisePropertyChanged(() => this.DenoisePresetVisible);
			this.RaisePropertyChanged(() => this.DenoiseTune);
			this.RaisePropertyChanged(() => this.DenoiseTuneVisible);
			this.RaisePropertyChanged(() => this.CustomDenoise);
			this.RaisePropertyChanged(() => this.CustomDenoiseVisible);
			this.RaisePropertyChanged(() => this.Deblock);
			this.RaisePropertyChanged(() => this.DeblockText);
			this.RaisePropertyChanged(() => this.Grayscale);
			this.RaisePropertyChanged(() => this.Rotation);
			this.RaisePropertyChanged(() => this.FlipHorizontal);
			this.RaisePropertyChanged(() => this.FlipVertical);
		}

		private void NotifyRotationChanged()
		{
			this.outputSizeService.Refresh();
		}
	}
}
