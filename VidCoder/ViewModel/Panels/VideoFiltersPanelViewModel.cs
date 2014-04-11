using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HandBrake.Interop;
using HandBrake.Interop.Model.Encoding;

namespace VidCoder.ViewModel
{
	using DataModels;
	using GalaSoft.MvvmLight.Messaging;
	using Messages;
	using Model;
	using Properties;
	using Resources;

	public class VideoFiltersPanelViewModel : PanelViewModel
	{
		private const int MinDeblock = 5;
		private List<RotationViewModel> rotationChoices; 

		public VideoFiltersPanelViewModel(EncodingViewModel encodingViewModel)
			: base(encodingViewModel)
		{
			this.rotationChoices = new List<RotationViewModel>
			{
				new RotationViewModel { Rotation = PictureRotation.None, Display = CommonRes.None },
				new RotationViewModel { Rotation = PictureRotation.Clockwise90, Display = EncodingRes.Rotation_Clockwise90, Image = "/Icons/rotate_90_cw.png"},
				new RotationViewModel { Rotation = PictureRotation.Clockwise270, Display = EncodingRes.Rotation_Counterclockwise90, Image = "/Icons/rotate_90_ccw.png" },
				new RotationViewModel { Rotation = PictureRotation.Clockwise180, Display = EncodingRes.Rotation_180, Image = "/Icons/rotate_180.png" }
			};
		}

		public DetelecineCombo SelectedDetelecine
		{
			get
			{
				return EnumConverter.Convert<Detelecine, DetelecineCombo>(this.Profile.Detelecine);
			}

			set
			{
				this.Profile.Detelecine = EnumConverter.Convert<DetelecineCombo, Detelecine>(value);
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
				return this.Profile.Detelecine == Detelecine.Custom;
			}
		}

		public DeinterlaceCombo SelectedDeinterlace
		{
			get
			{
				return EnumConverter.Convert<Deinterlace, DeinterlaceCombo>(this.Profile.Deinterlace);
			}

			set
			{
				Deinterlace newValue = EnumConverter.Convert<DeinterlaceCombo, Deinterlace>(value);
				this.Profile.Deinterlace = newValue;
				if (newValue != Deinterlace.Off)
				{
					this.SelectedDecomb = DecombCombo.Off;
				}

				this.RaisePropertyChanged(() => this.SelectedDeinterlace);
				this.RaisePropertyChanged(() => this.CustomDeinterlaceVisible);

				this.IsModified = true;
				this.UpdatePreviewWindow();
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
				return this.Profile.Deinterlace == Deinterlace.Custom;
			}
		}

		public DecombCombo SelectedDecomb
		{
			get
			{
				return EnumConverter.Convert<Decomb, DecombCombo>(this.Profile.Decomb);
			}

			set
			{
				Decomb newValue = EnumConverter.Convert<DecombCombo, Decomb>(value);
				this.Profile.Decomb = newValue;
				if (newValue != Decomb.Off)
				{
					this.SelectedDeinterlace = DeinterlaceCombo.Off;
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
				return this.Profile.Decomb == Decomb.Custom;
			}
		}

		public DenoiseCombo SelectedDenoise
		{
			get
			{
				return EnumConverter.Convert<Denoise, DenoiseCombo>(this.Profile.Denoise);
			}

			set
			{
				this.Profile.Denoise = EnumConverter.Convert<DenoiseCombo, Denoise>(value);
				this.RaisePropertyChanged(() => this.SelectedDenoise);
				this.RaisePropertyChanged(() => this.CustomDenoiseVisible);
				this.IsModified = true;
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
				return this.Profile.Denoise == Denoise.Custom;
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

		public PictureRotation Rotation
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
			Messenger.Default.Send(new RefreshPreviewMessage());
			Messenger.Default.Send(new RotationChangedMessage());
		}
	}
}
