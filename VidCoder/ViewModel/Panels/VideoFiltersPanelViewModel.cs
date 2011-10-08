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
				this.RaisePropertyChanged("Detelecine");
				this.RaisePropertyChanged("CustomDetelecineVisible");
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
				this.RaisePropertyChanged("CustomDetelecine");
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

				this.RaisePropertyChanged("Deinterlace");
				this.RaisePropertyChanged("CustomDeinterlaceVisible");

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
				this.RaisePropertyChanged("CustomDeinterlace");
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

				this.RaisePropertyChanged("Decomb");
				this.RaisePropertyChanged("CustomDecombVisible");
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
				this.RaisePropertyChanged("CustomDecomb");
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
				this.RaisePropertyChanged("Denoise");
				this.RaisePropertyChanged("CustomDenoiseVisible");
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
				this.RaisePropertyChanged("CustomDenoise");
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

				this.RaisePropertyChanged("Deblock");
				this.RaisePropertyChanged("DeblockText");
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
				this.RaisePropertyChanged("Grayscale");
				this.IsModified = true;
			}
		}

		public void NotifyAllChanged()
		{
			this.RaisePropertyChanged("Detelecine");
			this.RaisePropertyChanged("CustomDetelecine");
			this.RaisePropertyChanged("CustomDetelecineVisible");
			this.RaisePropertyChanged("Deinterlace");
			this.RaisePropertyChanged("CustomDeinterlace");
			this.RaisePropertyChanged("CustomDeinterlaceVisible");
			this.RaisePropertyChanged("Decomb");
			this.RaisePropertyChanged("CustomDecomb");
			this.RaisePropertyChanged("CustomDecombVisible");
			this.RaisePropertyChanged("Denoise");
			this.RaisePropertyChanged("CustomDenoise");
			this.RaisePropertyChanged("CustomDenoiseVisible");
			this.RaisePropertyChanged("Deblock");
			this.RaisePropertyChanged("DeblockText");
			this.RaisePropertyChanged("Grayscale");
		}
	}
}
