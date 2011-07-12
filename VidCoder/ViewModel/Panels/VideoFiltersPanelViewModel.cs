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
				this.NotifyPropertyChanged("Detelecine");
				this.NotifyPropertyChanged("CustomDetelecineVisible");
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
				this.NotifyPropertyChanged("CustomDetelecine");
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

				this.NotifyPropertyChanged("Deinterlace");
				this.NotifyPropertyChanged("CustomDeinterlaceVisible");

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
				this.NotifyPropertyChanged("CustomDeinterlace");
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

				this.NotifyPropertyChanged("Decomb");
				this.NotifyPropertyChanged("CustomDecombVisible");
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
				this.NotifyPropertyChanged("CustomDecomb");
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
				this.NotifyPropertyChanged("Denoise");
				this.NotifyPropertyChanged("CustomDenoiseVisible");
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
				this.NotifyPropertyChanged("CustomDenoise");
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
				return this.Profile.Deblock;
			}

			set
			{
				this.Profile.Deblock = value;
				this.NotifyPropertyChanged("Deblock");
				this.NotifyPropertyChanged("DeblockText");
				this.IsModified = true;
			}
		}

		public string DeblockText
		{
			get
			{
				if (this.Deblock >= 5)
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
				this.NotifyPropertyChanged("Grayscale");
				this.IsModified = true;
			}
		}

		public void NotifyAllChanged()
		{
			this.NotifyPropertyChanged("Detelecine");
			this.NotifyPropertyChanged("CustomDetelecine");
			this.NotifyPropertyChanged("CustomDetelecineVisible");
			this.NotifyPropertyChanged("Deinterlace");
			this.NotifyPropertyChanged("CustomDeinterlace");
			this.NotifyPropertyChanged("CustomDeinterlaceVisible");
			this.NotifyPropertyChanged("Decomb");
			this.NotifyPropertyChanged("CustomDecomb");
			this.NotifyPropertyChanged("CustomDecombVisible");
			this.NotifyPropertyChanged("Denoise");
			this.NotifyPropertyChanged("CustomDenoise");
			this.NotifyPropertyChanged("CustomDenoiseVisible");
			this.NotifyPropertyChanged("Deblock");
			this.NotifyPropertyChanged("DeblockText");
			this.NotifyPropertyChanged("Grayscale");
		}
	}
}
