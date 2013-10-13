
namespace VidCoder.ViewModel
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;
	using GalaSoft.MvvmLight.Messaging;
	using HandBrake.Interop;
	using HandBrake.Interop.Model;
	using HandBrake.Interop.Model.Encoding;
	using Messages;
	using Model;
	using Resources;

	public class PicturePanelViewModel : PanelViewModel
	{
		private const int DimensionsAutoSetModulus = 2;

		private List<ComboChoice<ScaleMethod>> scaleChoices; 

		private string outputSourceResolution;
		private string outputPixelAspectRatio;
		private string outputDisplayResolution;

		public PicturePanelViewModel(EncodingViewModel encodingViewModel)
			: base(encodingViewModel)
		{
			this.scaleChoices = new List<ComboChoice<ScaleMethod>>
			{
				new ComboChoice<ScaleMethod>(ScaleMethod.Lanczos, EncodingRes.ScaleMethod_Lanczos),
				new ComboChoice<ScaleMethod>(ScaleMethod.Bicubic, EncodingRes.ScaleMethod_BicubicOpenCL)
			};
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
				this.RaisePropertyChanged(() => this.OutputSourceResolution);
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
				this.RaisePropertyChanged(() => this.OutputPixelAspectRatio);
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
				this.RaisePropertyChanged(() => this.OutputDisplayResolution);
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
					this.RaisePropertyChanged(() => this.Width);
					if (this.Profile.Anamorphic == Anamorphic.None && this.KeepDisplayAspect && this.HasSourceData && this.Height > 0 && value > 0)
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
							this.RaisePropertyChanged(() => this.Height);
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
				if (this.Profile.Anamorphic == Anamorphic.Strict)
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
					this.RaisePropertyChanged(() => this.Height);
					if (this.Profile.Anamorphic == Anamorphic.None && this.KeepDisplayAspect && this.HasSourceData && this.Width > 0 && value > 0)
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
							this.RaisePropertyChanged(() => this.Width);
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
				if (this.Profile.Anamorphic == Anamorphic.Strict || this.Profile.Anamorphic == Anamorphic.Loose)
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
					this.RaisePropertyChanged(() => this.MaxWidth);
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
					this.RaisePropertyChanged(() => this.MaxHeight);
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
				if (this.Profile.Anamorphic == Anamorphic.Strict || this.Profile.Anamorphic == Anamorphic.Loose)
				{
					return true;
				}

				return this.Profile.KeepDisplayAspect;
			}

			set
			{
				this.Profile.KeepDisplayAspect = value;
				this.RaisePropertyChanged(() => this.KeepDisplayAspect);
				this.RaisePropertyChanged(() => this.PixelAspectVisibile);
				this.RefreshOutputSize();
				if (this.Profile.Anamorphic == Anamorphic.Custom)
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
				if (this.Profile.Anamorphic == Anamorphic.Strict || this.Profile.Anamorphic == Anamorphic.Loose)
				{
					return false;
				}

				return true;
			}
		}

		public List<ComboChoice<ScaleMethod>> ScaleChoices
		{
			get
			{
				return this.scaleChoices;
			}
		}

		public ScaleMethod ScaleMethod
		{
			get
			{
				return this.Profile.ScaleMethod;
			}

			set
			{
				this.Profile.ScaleMethod = value;
				this.RaisePropertyChanged(() => this.ScaleMethod);
				this.IsModified = true;
			}
		}

		public AnamorphicCombo SelectedAnamorphic
		{
			get
			{
				return EnumConverter.Convert<Anamorphic, AnamorphicCombo>(this.Profile.Anamorphic);
			}

			set
			{
				Anamorphic newValue = EnumConverter.Convert<AnamorphicCombo, Anamorphic>(value);
				this.Profile.Anamorphic = newValue;

				if (newValue == Anamorphic.Strict)
				{
					this.Profile.Width = 0;
					this.Profile.MaxWidth = 0;

					this.RaisePropertyChanged(() => this.Width);
					this.RaisePropertyChanged(() => this.MaxWidth);
				}

				if (newValue == Anamorphic.Strict || newValue == Anamorphic.Loose)
				{
					this.KeepDisplayAspect = true;

					this.Profile.Height = 0;
					this.Profile.MaxHeight = 0;

					this.RaisePropertyChanged(() => this.Height);
					this.RaisePropertyChanged(() => this.MaxHeight);
				}

				if (this.Profile.Anamorphic == Anamorphic.Custom)
				{
					this.KeepDisplayAspect = true;
					this.UseDisplayWidth = true;
				}

				this.RaisePropertyChanged(() => this.SelectedAnamorphic);
				this.RaisePropertyChanged(() => this.CustomAnamorphicFieldsVisible);
				this.RaisePropertyChanged(() => this.ModulusVisible);
				this.RaisePropertyChanged(() => this.PixelAspectVisibile);
				this.RaisePropertyChanged(() => this.KeepDisplayAspect);
				this.RaisePropertyChanged(() => this.KeepDisplayAspectEnabled);
				this.RaisePropertyChanged(() => this.WidthEnabled);
				this.RaisePropertyChanged(() => this.HeightEnabled);
				this.RefreshOutputSize();

				this.IsModified = true;
				this.UpdatePreviewWindow();
			}
		}

		public bool CustomAnamorphicFieldsVisible
		{
			get
			{
				return this.Profile.Anamorphic == Anamorphic.Custom;
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
				this.RaisePropertyChanged(() => this.Modulus);
				this.RefreshOutputSize();

				this.IsModified = true;
				this.UpdatePreviewWindow();
			}
		}

		public bool ModulusVisible
		{
			get
			{
				return this.Profile.Anamorphic == Anamorphic.Custom || this.Profile.Anamorphic == Anamorphic.Loose;
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

				this.RaisePropertyChanged(() => this.UseDisplayWidth);
				this.RaisePropertyChanged(() => this.KeepDisplayAspect);
				this.RaisePropertyChanged(() => this.KeepDisplayAspectEnabled);
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
				this.RaisePropertyChanged(() => this.DisplayWidth);
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
					this.RaisePropertyChanged(() => this.PixelAspectX);
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
					this.RaisePropertyChanged(() => this.PixelAspectY);
					this.RefreshOutputSize();

					this.IsModified = true;
					this.UpdatePreviewWindow();
				}
			}
		}

		public CroppingType CroppingType
		{
			get
			{
				return this.Profile.CroppingType;
			}

			set
			{
				if (value == CroppingType.Custom)
				{
					// Set initial custom cropping values to previous values to make tweaking easier
					this.Profile.Cropping.Left = this.CropLeft;
					this.Profile.Cropping.Top = this.CropTop;
					this.Profile.Cropping.Right = this.CropRight;
					this.Profile.Cropping.Bottom = this.CropBottom;
				}

				this.Profile.CroppingType = value;

				this.RaisePropertyChanged(() => this.CroppingType);
				this.RaisePropertyChanged(() => this.CroppingUIEnabled);
				this.RaisePropertyChanged(() => this.CropLeft);
				this.RaisePropertyChanged(() => this.CropTop);
				this.RaisePropertyChanged(() => this.CropRight);
				this.RaisePropertyChanged(() => this.CropBottom);
				this.RefreshOutputSize();

				this.IsModified = true;
				this.UpdatePreviewWindow();
			}
		}

		public bool CroppingUIEnabled
		{
			get
			{
				return this.CroppingType == CroppingType.Custom;
			}
		}

		public int CropLeft
		{
			get
			{
				switch (this.CroppingType)
				{
					case CroppingType.Automatic:
						if (this.HasSourceData)
						{
							return this.SelectedTitle.AutoCropDimensions.Left;
						}
						break;
					case CroppingType.Custom:
						return this.Profile.Cropping.Left;
				}

				return 0;
			}

			set
			{
				this.Profile.Cropping.Left = value;
				this.RaisePropertyChanged(() => this.CropLeft);
				this.RefreshOutputSize();

				this.IsModified = true;
				this.UpdatePreviewWindow();
			}
		}

		public int CropTop
		{
			get
			{
				switch (this.CroppingType)
				{
					case CroppingType.Automatic:
						if (this.HasSourceData)
						{
							return this.SelectedTitle.AutoCropDimensions.Top;
						}
						break;
					case CroppingType.Custom:
						return this.Profile.Cropping.Top;
				}

				return 0;
			}

			set
			{
				this.Profile.Cropping.Top = value;
				this.RaisePropertyChanged(() => this.CropTop);
				this.RefreshOutputSize();

				this.IsModified = true;
				this.UpdatePreviewWindow();
			}
		}

		public int CropRight
		{
			get
			{
				switch (this.CroppingType)
				{
					case CroppingType.Automatic:
						if (this.HasSourceData)
						{
							return this.SelectedTitle.AutoCropDimensions.Right;
						}
						break;
					case CroppingType.Custom:
						return this.Profile.Cropping.Right;
				}

				return 0;
			}

			set
			{
				this.Profile.Cropping.Right = value;
				this.RaisePropertyChanged(() => this.CropRight);
				this.RefreshOutputSize();

				this.IsModified = true;
				this.UpdatePreviewWindow();
			}
		}

		public int CropBottom
		{
			get
			{
				switch (this.CroppingType)
				{
					case CroppingType.Automatic:
						if (this.HasSourceData)
						{
							return this.SelectedTitle.AutoCropDimensions.Bottom;
						}
						break;
					case CroppingType.Custom:
						return this.Profile.Cropping.Bottom;
				}

				return 0;
			}

			set
			{
				this.Profile.Cropping.Bottom = value;
				this.RaisePropertyChanged(() => this.CropBottom);
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
				VCJob job = this.MainViewModel.EncodeJob;
				job.EncodingProfile = this.Profile;

				int width, height, parWidth, parHeight;
				this.MainViewModel.ScanInstance.GetSize(job.HbJob, out width, out height, out parWidth, out parHeight);

				this.StorageWidth = width;
				this.StorageHeight = height;

				Messenger.Default.Send(new OutputSizeChangedMessage());

				this.OutputSourceResolution = width + " x " + height;
				this.OutputPixelAspectRatio = parWidth + "/" + parHeight;
				this.OutputDisplayResolution = Math.Round(width * (((double)parWidth) / parHeight)) + " x " + height;
			}
		}

		public int StorageWidth { get; set; }
		public int StorageHeight { get; set; }

		public void NotifyAllChanged()
		{
			this.RaisePropertyChanged(() => this.Width);
			this.RaisePropertyChanged(() => this.WidthEnabled);
			this.RaisePropertyChanged(() => this.Height);
			this.RaisePropertyChanged(() => this.HeightEnabled);
			this.RaisePropertyChanged(() => this.MaxWidth);
			this.RaisePropertyChanged(() => this.MaxHeight);
			this.RaisePropertyChanged(() => this.ScaleMethod);
			this.RaisePropertyChanged(() => this.KeepDisplayAspect);
			this.RaisePropertyChanged(() => this.KeepDisplayAspectEnabled);
			this.RaisePropertyChanged(() => this.SelectedAnamorphic);
			this.RaisePropertyChanged(() => this.CustomAnamorphicFieldsVisible);
			this.RaisePropertyChanged(() => this.ModulusVisible);
			this.RaisePropertyChanged(() => this.Modulus);
			this.RaisePropertyChanged(() => this.UseDisplayWidth);
			this.RaisePropertyChanged(() => this.DisplayWidth);
			this.RaisePropertyChanged(() => this.PixelAspectX);
			this.RaisePropertyChanged(() => this.PixelAspectY);
			this.RaisePropertyChanged(() => this.PixelAspectVisibile);
			this.RaisePropertyChanged(() => this.CroppingType);
			this.RaisePropertyChanged(() => this.CroppingUIEnabled);
			this.RaisePropertyChanged(() => this.CropLeft);
			this.RaisePropertyChanged(() => this.CropTop);
			this.RaisePropertyChanged(() => this.CropRight);
			this.RaisePropertyChanged(() => this.CropBottom);
		}

		public override void NotifySelectedTitleChanged()
		{
			this.RefreshOutputSize();

			this.RaisePropertyChanged(() => this.CropTop);
			this.RaisePropertyChanged(() => this.CropBottom);
			this.RaisePropertyChanged(() => this.CropLeft);
			this.RaisePropertyChanged(() => this.CropRight);
			this.RaisePropertyChanged(() => this.InputSourceResolution);
			this.RaisePropertyChanged(() => this.InputPixelAspectRatio);
			this.RaisePropertyChanged(() => this.InputDisplayResolution);

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

			this.RaisePropertyChanged(() => this.PixelAspectX);
			this.RaisePropertyChanged(() => this.PixelAspectY);
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

			this.RaisePropertyChanged(() => this.DisplayWidth);
		}

		private string CreateParDisplayString(int parWidth, int parHeight)
		{
			double pixelAspectRatio = ((double)parWidth) / parHeight;
			return pixelAspectRatio.ToString("F2") + " (" + parWidth + "/" + parHeight + ")";
		}
	}
}
