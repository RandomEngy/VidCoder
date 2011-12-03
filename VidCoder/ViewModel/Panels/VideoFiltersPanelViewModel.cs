using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HandBrake.Interop;
using HandBrake.Interop.Model.Encoding;

namespace VidCoder.ViewModel
{
	public class VideoFiltersPanelViewModel : PanelViewModel
	{
		private const int MinDeblock = 5;

		public VideoFiltersPanelViewModel(EncodingViewModel encodingViewModel)
			: base(encodingViewModel)
		{
		}

		public Detelecine Detelecine
		{
			get
			{
				return this.Profile.Detelecine;
			}

			set
			{
				this.Profile.Detelecine = value;
				this.RaisePropertyChanged(() => this.Detelecine);
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
				return this.Detelecine == Detelecine.Custom;
			}
		}

		public Deinterlace Deinterlace
		{
			get
			{
				return this.Profile.Deinterlace;
			}

			set
			{
				this.Profile.Deinterlace = value;
				if (value != Deinterlace.Off)
				{
					this.Decomb = Decomb.Off;
				}

				this.RaisePropertyChanged(() => this.Deinterlace);
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
				return this.Deinterlace == Deinterlace.Custom;
			}
		}

		public Decomb Decomb
		{
			get
			{
				return this.Profile.Decomb;
			}

			set
			{
				this.Profile.Decomb = value;
				if (value != Decomb.Off)
				{
					this.Deinterlace = Deinterlace.Off;
				}

				this.RaisePropertyChanged(() => this.Decomb);
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
				return this.Decomb == Decomb.Custom;
			}
		}

		public Denoise Denoise
		{
			get
			{
				return this.Profile.Denoise;
			}

			set
			{
				this.Profile.Denoise = value;
				this.RaisePropertyChanged(() => this.Denoise);
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
				return this.Denoise == Denoise.Custom;
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

				return "Off";
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

		public void NotifyAllChanged()
		{
			this.RaisePropertyChanged(() => this.Detelecine);
			this.RaisePropertyChanged(() => this.CustomDetelecine);
			this.RaisePropertyChanged(() => this.CustomDetelecineVisible);
			this.RaisePropertyChanged(() => this.Deinterlace);
			this.RaisePropertyChanged(() => this.CustomDeinterlace);
			this.RaisePropertyChanged(() => this.CustomDeinterlaceVisible);
			this.RaisePropertyChanged(() => this.Decomb);
			this.RaisePropertyChanged(() => this.CustomDecomb);
			this.RaisePropertyChanged(() => this.CustomDecombVisible);
			this.RaisePropertyChanged(() => this.Denoise);
			this.RaisePropertyChanged(() => this.CustomDenoise);
			this.RaisePropertyChanged(() => this.CustomDenoiseVisible);
			this.RaisePropertyChanged(() => this.Deblock);
			this.RaisePropertyChanged(() => this.DeblockText);
			this.RaisePropertyChanged(() => this.Grayscale);
		}
	}
}
