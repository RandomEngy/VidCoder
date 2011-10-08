using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HandBrake.Interop;
using HandBrake.Interop.Model;
using HandBrake.Interop.Model.Encoding;

namespace VidCoder.ViewModel
{
	public class PicturePanelViewModel : PanelViewModel
	{
		private const int DimensionsAutoSetModulus = 2;

		private string outputSourceResolution;
		private string outputPixelAspectRatio;
		private string outputDisplayResolution;

		public PicturePanelViewModel(EncodingViewModel encodingViewModel)
			: base(encodingViewModel)
		{
		}

		public string InputSourceResolution
		{
			get
			{
				if (this.HasSourceData)
				{
					return this.SelectedTitle.Resolution.Width + " x " + this.SelectedTitle.Resolution.Height;
				}

				return string.Empty;
			}
		}

		public string InputPixelAspectRatio
		{
			get
			{
				if (this.HasSourceData)
				{
					return this.CreateParDisplayString(this.SelectedTitle.ParVal.Width, this.SelectedTitle.ParVal.Height);
				}

				return string.Empty;
			}
		}

		public string InputDisplayResolution
		{
			get
			{
				if (this.HasSourceData)
				{
					double pixelAspectRatio = ((double)this.SelectedTitle.ParVal.Width) / this.SelectedTitle.ParVal.Height;
					double displayWidth = this.SelectedTitle.Resolution.Width * pixelAspectRatio;
					int displayWidthRounded = (int)Math.Round(displayWidth);

					return displayWidthRounded + " x " + this.SelectedTitle.Resolution.Height;
				}

				return string.Empty;
			}
		}

		public string OutputSourceResolution
		{
			get
			{
				return this.outputSourceResolution;
			}

			set
			{
				this.outputSourceResolution = value;
				this.RaisePropertyChanged("OutputSourceResolution");
			}
		}

		public string OutputPixelAspectRatio
		{
			get
			{
				return this.outputPixelAspectRatio;
			}

			set
			{
				this.outputPixelAspectRatio = value;
				this.RaisePropertyChanged("OutputPixelAspectRatio");
			}
		}

		public string OutputDisplayResolution
		{
			get
			{
				return this.outputDisplayResolution;
			}

			set
			{
				this.outputDisplayResolution = value;
				this.RaisePropertyChanged("OutputDisplayResolution");
			}
		}

		public int Width
		{
			get
			{
				return this.Profile.Width;
			}

			set
			{
				if (this.Profile.Width != value)
				{
					this.Profile.Width = value;
					this.RaisePropertyChanged("Width");
					if (this.Anamorphic == Anamorphic.None && this.KeepDisplayAspect && this.HasSourceData && this.Height > 0 && value > 0)
					{
						var cropWidthAmount = this.CropLeft + this.CropRight;
						var cropHeightAmount = this.CropTop + this.CropBottom;
						var sourceWidth = this.SelectedTitle.Resolution.Width;
						var sourceHeight = this.SelectedTitle.Resolution.Height;
						var parWidth = this.SelectedTitle.ParVal.Width;
						var parHeight = this.SelectedTitle.ParVal.Height;

						double finalDisplayAspect = ((double)(sourceWidth - cropWidthAmount) * parWidth) / ((sourceHeight - cropHeightAmount) * parHeight);

						int newHeight = (int)(value / finalDisplayAspect);
						newHeight = GetNearestValue(newHeight, DimensionsAutoSetModulus);
						if (newHeight > 0)
						{
							this.Profile.Height = newHeight;
							this.RaisePropertyChanged("Height");
						}
					}

					this.RefreshOutputSize();
					this.IsModified = true;
					this.UpdatePreviewWindow();
				}
			}
		}

		public bool WidthEnabled
		{
			get
			{
				if (this.Anamorphic == Anamorphic.Strict)
				{
					return false;
				}

				return true;
			}
		}

		public int Height
		{
			get
			{
				return this.Profile.Height;
			}

			set
			{
				if (this.Profile.Height != value)
				{
					this.Profile.Height = value;
					this.RaisePropertyChanged("Height");
					if (this.Anamorphic == Anamorphic.None && this.KeepDisplayAspect && this.HasSourceData && this.Width > 0 && value > 0)
					{
						var cropWidthAmount = this.CropLeft + this.CropRight;
						var cropHeightAmount = this.CropTop + this.CropBottom;
						var sourceWidth = this.SelectedTitle.Resolution.Width;
						var sourceHeight = this.SelectedTitle.Resolution.Height;
						var parWidth = this.SelectedTitle.ParVal.Width;
						var parHeight = this.SelectedTitle.ParVal.Height;

						double finalDisplayAspect = ((double)(sourceWidth - cropWidthAmount) * parWidth) / ((sourceHeight - cropHeightAmount) * parHeight);

						int newWidth = (int)(value * finalDisplayAspect);
						newWidth = GetNearestValue(newWidth, DimensionsAutoSetModulus);
						if (newWidth > 0)
						{
							this.Profile.Width = newWidth;
							this.RaisePropertyChanged("Width");
						}
					}

					this.RefreshOutputSize();
					this.IsModified = true;
					this.UpdatePreviewWindow();
				}
			}
		}

		public bool HeightEnabled
		{
			get
			{
				if (this.Anamorphic == Anamorphic.Strict || this.Anamorphic == Anamorphic.Loose)
				{
					return false;
				}

				return true;
			}
		}

		public int MaxWidth
		{
			get
			{
				return this.Profile.MaxWidth;
			}

			set
			{
				if (this.Profile.MaxWidth != value)
				{
					this.Profile.MaxWidth = value;
					this.RaisePropertyChanged("MaxWidth");
					this.RefreshOutputSize();

					this.IsModified = true;
					this.UpdatePreviewWindow();
				}
			}
		}

		public int MaxHeight
		{
			get
			{
				return this.Profile.MaxHeight;
			}

			set
			{
				if (this.Profile.MaxHeight != value)
				{
					this.Profile.MaxHeight = value;
					this.RaisePropertyChanged("MaxHeight");
					this.RefreshOutputSize();

					this.IsModified = true;
					this.UpdatePreviewWindow();
				}
			}
		}

		public bool KeepDisplayAspect
		{
			get
			{
				if (this.Anamorphic == Anamorphic.Strict || this.Anamorphic == Anamorphic.Loose)
				{
					return true;
				}

				return this.Profile.KeepDisplayAspect;
			}

			set
			{
				this.Profile.KeepDisplayAspect = value;
				this.RaisePropertyChanged("KeepDisplayAspect");
				this.RaisePropertyChanged("PixelAspectVisibile");
				this.RefreshOutputSize();
				if (this.Anamorphic == Anamorphic.Custom)
				{
					if (value && !this.UseDisplayWidth)
					{
						this.UseDisplayWidth = true;
					}

					if (this.UseDisplayWidth && this.DisplayWidth == 0)
					{
						this.PopulateDisplayWidth();
					}
				}

				this.IsModified = true;
				this.UpdatePreviewWindow();
			}
		}

		public bool KeepDisplayAspectEnabled
		{
			get
			{
				if (this.Anamorphic == Anamorphic.Strict || this.Anamorphic == Anamorphic.Loose)
				{
					return false;
				}

				return true;
			}
		}

		public Anamorphic Anamorphic
		{
			get
			{
				return this.Profile.Anamorphic;
			}

			set
			{
				this.Profile.Anamorphic = value;

				if (value == Anamorphic.Strict || value == Anamorphic.Loose)
				{
					this.KeepDisplayAspect = true;
				}

				if (this.Profile.Anamorphic == Anamorphic.Custom)
				{
					this.KeepDisplayAspect = true;
					this.UseDisplayWidth = true;
				}

				this.RaisePropertyChanged("Anamorphic");
				this.RaisePropertyChanged("CustomAnamorphicFieldsVisible");
				this.RaisePropertyChanged("PixelAspectVisibile");
				this.RaisePropertyChanged("KeepDisplayAspect");
				this.RaisePropertyChanged("KeepDisplayAspectEnabled");
				this.RaisePropertyChanged("WidthEnabled");
				this.RaisePropertyChanged("HeightEnabled");
				this.RefreshOutputSize();

				this.IsModified = true;
				this.UpdatePreviewWindow();
			}
		}

		public bool CustomAnamorphicFieldsVisible
		{
			get
			{
				return this.Anamorphic == Anamorphic.Custom;
			}
		}

		public int Modulus
		{
			get
			{
				return this.Profile.Modulus;
			}

			set
			{
				this.Profile.Modulus = value;
				this.RaisePropertyChanged("Modulus");

				this.IsModified = true;
				this.UpdatePreviewWindow();
			}
		}

		public bool UseDisplayWidth
		{
			get
			{
				return this.Profile.UseDisplayWidth;
			}

			set
			{
				this.Profile.UseDisplayWidth = value;
				if (value)
				{
					this.PopulateDisplayWidth();
				}
				else
				{
					this.PopulatePixelAspect();
				}

				this.RaisePropertyChanged("UseDisplayWidth");
				this.RaisePropertyChanged("KeepDisplayAspect");
				this.RaisePropertyChanged("KeepDisplayAspectEnabled");
				this.RefreshOutputSize();

				this.IsModified = true;
				this.UpdatePreviewWindow();
			}
		}

		public int DisplayWidth
		{
			get
			{
				return this.Profile.DisplayWidth;
			}

			set
			{
				this.Profile.DisplayWidth = value;
				this.RaisePropertyChanged("DisplayWidth");
				this.RefreshOutputSize();

				this.IsModified = true;
				this.UpdatePreviewWindow();
			}
		}

		public bool PixelAspectVisibile
		{
			get
			{
				if (!this.CustomAnamorphicFieldsVisible)
				{
					return false;
				}

				return !this.KeepDisplayAspect;
			}
		}

		public int PixelAspectX
		{
			get
			{
				return this.Profile.PixelAspectX;
			}

			set
			{
				if (this.Profile.PixelAspectX != value)
				{
					this.Profile.PixelAspectX = value;
					this.RaisePropertyChanged("PixelAspectX");
					this.RefreshOutputSize();

					this.IsModified = true;
					this.UpdatePreviewWindow();
				}
			}
		}

		public int PixelAspectY
		{
			get
			{
				return this.Profile.PixelAspectY;
			}

			set
			{
				if (this.Profile.PixelAspectY != value)
				{
					this.Profile.PixelAspectY = value;
					this.RaisePropertyChanged("PixelAspectY");
					this.RefreshOutputSize();

					this.IsModified = true;
					this.UpdatePreviewWindow();
				}
			}
		}

		public bool CustomCropping
		{
			get
			{
				return this.Profile.CustomCropping;
			}

			set
			{
				this.Profile.CustomCropping = value;

				this.RaisePropertyChanged("CustomCropping");
				this.RaisePropertyChanged("CropLeft");
				this.RaisePropertyChanged("CropTop");
				this.RaisePropertyChanged("CropRight");
				this.RaisePropertyChanged("CropBottom");
				this.RefreshOutputSize();

				this.IsModified = true;
				this.UpdatePreviewWindow();
			}
		}

		public int CropLeft
		{
			get
			{
				if (this.CustomCropping)
				{
					return this.Profile.Cropping.Left;
				}

				if (this.HasSourceData)
				{
					return this.SelectedTitle.AutoCropDimensions.Left;
				}

				return 0;
			}

			set
			{
				this.Profile.Cropping.Left = value;
				this.RaisePropertyChanged("CropLeft");
				this.RefreshOutputSize();

				this.IsModified = true;
				this.UpdatePreviewWindow();
			}
		}

		public int CropTop
		{
			get
			{
				if (this.CustomCropping)
				{
					return this.Profile.Cropping.Top;
				}

				if (this.HasSourceData)
				{
					return this.SelectedTitle.AutoCropDimensions.Top;
				}

				return 0;
			}

			set
			{
				this.Profile.Cropping.Top = value;
				this.RaisePropertyChanged("CropTop");
				this.RefreshOutputSize();

				this.IsModified = true;
				this.UpdatePreviewWindow();
			}
		}

		public int CropRight
		{
			get
			{
				if (this.CustomCropping)
				{
					return this.Profile.Cropping.Right;
				}

				if (this.HasSourceData)
				{
					return this.SelectedTitle.AutoCropDimensions.Right;
				}

				return 0;
			}

			set
			{
				this.Profile.Cropping.Right = value;
				this.RaisePropertyChanged("CropRight");
				this.RefreshOutputSize();

				this.IsModified = true;
				this.UpdatePreviewWindow();
			}
		}

		public int CropBottom
		{
			get
			{
				if (this.CustomCropping)
				{
					return this.Profile.Cropping.Bottom;
				}

				if (this.HasSourceData)
				{
					return this.SelectedTitle.AutoCropDimensions.Bottom;
				}

				return 0;
			}

			set
			{
				this.Profile.Cropping.Bottom = value;
				this.RaisePropertyChanged("CropBottom");
				this.RefreshOutputSize();

				this.IsModified = true;
				this.UpdatePreviewWindow();
			}
		}

		private static int GetNearestValue(int number, int modulus)
		{
			int remainder = number % modulus;

			if (remainder == 0)
			{
				return number;
			}

			return remainder >= (modulus / 2) ? number + (modulus - remainder) : number - remainder;
		}

		public void RefreshOutputSize()
		{
			if (this.HasSourceData)
			{
				EncodeJob job = this.MainViewModel.EncodeJob;
				job.EncodingProfile = this.Profile;

				int width, height, parWidth, parHeight;
				this.MainViewModel.ScanInstance.GetSize(job, out width, out height, out parWidth, out parHeight);

				this.OutputSourceResolution = width + " x " + height;
				this.OutputPixelAspectRatio = parWidth + "/" + parHeight;
				this.OutputDisplayResolution = Math.Round(width * (((double)parWidth) / parHeight)) + " x " + height;
			}
		}

		public void NotifyAllChanged()
		{
			this.RaisePropertyChanged("Width");
			this.RaisePropertyChanged("WidthEnabled");
			this.RaisePropertyChanged("Height");
			this.RaisePropertyChanged("HeightEnabled");
			this.RaisePropertyChanged("MaxWidth");
			this.RaisePropertyChanged("MaxHeight");
			this.RaisePropertyChanged("KeepDisplayAspect");
			this.RaisePropertyChanged("KeepDisplayAspectEnabled");
			this.RaisePropertyChanged("Anamorphic");
			this.RaisePropertyChanged("CustomAnamorphicFieldsVisible");
			this.RaisePropertyChanged("Modulus");
			this.RaisePropertyChanged("UseDisplayWidth");
			this.RaisePropertyChanged("DisplayWidth");
			this.RaisePropertyChanged("PixelAspectX");
			this.RaisePropertyChanged("PixelAspectY");
			this.RaisePropertyChanged("PixelAspectVisibile");
			this.RaisePropertyChanged("CustomCropping");
			this.RaisePropertyChanged("CropLeft");
			this.RaisePropertyChanged("CropTop");
			this.RaisePropertyChanged("CropRight");
			this.RaisePropertyChanged("CropBottom");
		}

		public override void NotifySelectedTitleChanged()
		{
			this.RefreshOutputSize();

			this.RaisePropertyChanged("CropTop");
			this.RaisePropertyChanged("CropBottom");
			this.RaisePropertyChanged("CropLeft");
			this.RaisePropertyChanged("CropRight");
			this.RaisePropertyChanged("InputSourceResolution");
			this.RaisePropertyChanged("InputPixelAspectRatio");
			this.RaisePropertyChanged("InputDisplayResolution");

			base.NotifySelectedTitleChanged();
		}

		private void PopulatePixelAspect()
		{
			if (this.SelectedTitle == null)
			{
				this.Profile.PixelAspectX = 1;
				this.Profile.PixelAspectY = 1;
			}
			else
			{
				this.Profile.PixelAspectX = this.SelectedTitle.ParVal.Width;
				this.Profile.PixelAspectY = this.SelectedTitle.ParVal.Height;
			}

			this.RaisePropertyChanged("PixelAspectX");
			this.RaisePropertyChanged("PixelAspectY");
		}

		private void PopulateDisplayWidth()
		{
			if (this.KeepDisplayAspect)
			{
				// Let display width be "auto" if we're set to keep the aspect ratio
				this.Profile.DisplayWidth = 0;
			}
			else
			{
				// Populate it with something sensible if the set display width must be used
				if (this.SelectedTitle == null)
				{
					this.Profile.DisplayWidth = 853;
				}
				else
				{
					this.Profile.DisplayWidth = (int)Math.Round(((double)((this.SelectedTitle.Resolution.Width - this.CropLeft - this.CropRight) * this.SelectedTitle.ParVal.Width)) / this.SelectedTitle.ParVal.Height);
				}
			}

			this.RaisePropertyChanged("DisplayWidth");
		}

		private string CreateParDisplayString(int parWidth, int parHeight)
		{
			double pixelAspectRatio = ((double)parWidth) / parHeight;
			return pixelAspectRatio.ToString("F2") + " (" + parWidth + "/" + parHeight + ")";
		}
	}
}
