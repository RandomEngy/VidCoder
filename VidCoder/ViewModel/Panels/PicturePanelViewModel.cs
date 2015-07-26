using System;
using System.Collections.Generic;
using GalaSoft.MvvmLight.Messaging;
using HandBrake.ApplicationServices.Interop.Json.Shared;
using VidCoder.Messages;
using VidCoder.Model;
using VidCoder.Resources;
using VidCoderCommon.Model;
using ReactiveUI;

namespace VidCoder.ViewModel
{
	public class PicturePanelViewModel : PanelViewModel
	{
		private const int DimensionsAutoSetModulus = 2;

        private List<ComboChoice<VCScaleMethod>> scaleChoices; 

		public PicturePanelViewModel(EncodingWindowViewModel encodingWindowViewModel)
			: base(encodingWindowViewModel)
		{
			this.AutomaticChange = true;

			this.RegisterProfileProperties();

			// WidthEnabled
			this.WhenAnyValue(x => x.Anamorphic, anamorphic => anamorphic != VCAnamorphic.Strict)
				.ToProperty(this, x => x.WidthEnabled, out this.widthEnabled);

			// HeightEnabled
			this.WhenAnyValue(x => x.Anamorphic, anamorphic =>
			{
				if (anamorphic == VCAnamorphic.Strict || anamorphic == VCAnamorphic.Loose)
				{
					return false;
				}

				return true;
			}).ToProperty(this, x => x.HeightEnabled, out this.heightEnabled);

			// ModulusVisible
			this.WhenAnyValue(x => x.Anamorphic, anamorphic =>
			{
				return anamorphic == VCAnamorphic.Custom || anamorphic == VCAnamorphic.Loose;
			}).ToProperty(this, x => x.ModulusVisible, out this.modulusVisible);

			// KeepDisplayAspectEnabled
			this.WhenAnyValue(x => x.Anamorphic, anamorphic =>
			{
				if (anamorphic == VCAnamorphic.Strict || anamorphic == VCAnamorphic.Loose)
				{
					return false;
				}

				return true;
			}).ToProperty(this, x => x.KeepDisplayAspectEnabled, out this.keepDisplayAspectEnabled);

			// CustomAnamorphicFieldsVisible
			this.WhenAnyValue(x => x.Anamorphic, anamorphic =>
			{
				return anamorphic == VCAnamorphic.Custom;
			}).ToProperty(this, x => x.CustomAnamorphicFieldsVisible, out this.customAnamorphicFieldsVisible);

			// PixelAspectVisible
			this.WhenAnyValue(x => x.Anamorphic, x => x.KeepDisplayAspect, (anamorphic, keepDisplayAspect) =>
			{
				if (anamorphic != VCAnamorphic.Custom)
				{
					return false;
				}

				return !keepDisplayAspect;
			}).ToProperty(this, x => x.PixelAspectVisible, out this.pixelAspectVisible);

			// InputSourceResolution
			this.MainViewModel.WhenAnyValue(x => x.SelectedTitle, selectedTitle =>
			{
				if (selectedTitle != null)
				{
					return selectedTitle.Geometry.Width + " x " + selectedTitle.Geometry.Height;
				}

				return string.Empty;
			}).ToProperty(this, x => x.InputSourceResolution, out this.inputSourceResolution);

			// InputPixelAspectRatio
			this.MainViewModel.WhenAnyValue(x => x.SelectedTitle, selectedTitle =>
			{
				if (selectedTitle != null)
				{
					return this.CreateParDisplayString(selectedTitle.Geometry.PAR.Num, selectedTitle.Geometry.PAR.Den);
				}

				return string.Empty;
			}).ToProperty(this, x => x.InputPixelAspectRatio, out this.inputPixelAspectRatio);

			// InputDisplayResolution
			this.MainViewModel.WhenAnyValue(x => x.SelectedTitle, selectedTitle =>
			{
				if (selectedTitle != null)
				{
					double pixelAspectRatio = ((double) selectedTitle.Geometry.PAR.Num) / selectedTitle.Geometry.PAR.Den;
					double displayWidth = selectedTitle.Geometry.Width * pixelAspectRatio;
					int displayWidthRounded = (int) Math.Round(displayWidth);

					return displayWidthRounded + " x " + selectedTitle.Geometry.Height;
				}

				return string.Empty;
			}).ToProperty(this, x => x.InputDisplayResolution, out this.inputDisplayResolution);

			// CroppingUIEnabled
			this.WhenAnyValue(x => x.CroppingType, croppingType =>
			{
				return croppingType == VCCroppingType.Custom;
			}).ToProperty(this, x => x.CroppingUIEnabled, out this.croppingUIEnabled);

			// Update the underlying profile when our local Crop properties change. This only applies for
			// CroppingType.Custom
			this.WhenAnyValue(x => x.CropTop)
				.Subscribe(cropTop =>
				{
					this.UpdateProfileProperty(
						() => this.Profile.Cropping,
						MvvmUtilities.GetPropertyName(() => this.Profile.Cropping.Top),
						MvvmUtilities.GetPropertyName(() => this.CropTop), 
						cropTop,
						raisePropertyChanged: false);
				});
			this.WhenAnyValue(x => x.CropBottom)
				.Subscribe(cropBottom =>
				{
					this.UpdateProfileProperty(
						() => this.Profile.Cropping,
						MvvmUtilities.GetPropertyName(() => this.Profile.Cropping.Bottom),
						MvvmUtilities.GetPropertyName(() => this.CropBottom),
						cropBottom,
						raisePropertyChanged: false);

					//this.UpdateProfileProperty(MvvmUtilities.GetPropertyName(() => this.CropBottom), () => { this.Profile.Cropping.Bottom = cropBottom; }, raisePropertyChanged: false);
				});
			this.WhenAnyValue(x => x.CropLeft)
				.Subscribe(cropLeft =>
				{
					this.UpdateProfileProperty(
						() => this.Profile.Cropping,
						MvvmUtilities.GetPropertyName(() => this.Profile.Cropping.Left),
						MvvmUtilities.GetPropertyName(() => this.CropLeft),
						cropLeft,
						raisePropertyChanged: false);

					//this.UpdateProfileProperty(MvvmUtilities.GetPropertyName(() => this.CropLeft), () => { this.Profile.Cropping.Left = cropLeft; }, raisePropertyChanged: false);
				});
			this.WhenAnyValue(x => x.CropRight)
				.Subscribe(cropRight =>
				{
					this.UpdateProfileProperty(
						() => this.Profile.Cropping,
						MvvmUtilities.GetPropertyName(() => this.Profile.Cropping.Right),
						MvvmUtilities.GetPropertyName(() => this.CropRight),
						cropRight,
						raisePropertyChanged: false);

					//this.UpdateProfileProperty(MvvmUtilities.GetPropertyName(() => this.CropRight), () => { this.Profile.Cropping.Right = cropRight; }, raisePropertyChanged: false);
				});

			// Auto-fill the cropping properties when type is Auto or None
			this.WhenAnyValue(
				x => x.CroppingType, 
				x => x.MainViewModel.SelectedTitle, 
				(croppingType, selectedTitle) =>
				{
					return new { croppingType, selectedTitle };
				})
				.Subscribe(x =>
				{
					if (x.croppingType == VCCroppingType.None)
					{
						bool oldAutoValue = this.AutomaticChange;
						this.AutomaticChange = true;
						this.CropTop = 0;
						this.CropBottom = 0;
						this.CropLeft = 0;
						this.CropRight = 0;
						this.AutomaticChange = oldAutoValue;
					}
					else if (x.croppingType == VCCroppingType.Automatic)
					{
						bool oldAutoValue = this.AutomaticChange;
						this.AutomaticChange = true;

						if (x.selectedTitle == null)
						{
							this.CropTop = 0;
							this.CropBottom = 0;
							this.CropLeft = 0;
							this.CropRight = 0;
						}
						else
						{
							this.CropTop = x.selectedTitle.Crop[0];
							this.CropBottom = x.selectedTitle.Crop[1];
							this.CropLeft = x.selectedTitle.Crop[2];
							this.CropRight = x.selectedTitle.Crop[3];
						}

						this.AutomaticChange = oldAutoValue;
					}
				});

            this.scaleChoices = new List<ComboChoice<VCScaleMethod>>
			{
				new ComboChoice<VCScaleMethod>(VCScaleMethod.Lanczos, EncodingRes.ScaleMethod_Lanczos),
				new ComboChoice<VCScaleMethod>(VCScaleMethod.Bicubic, EncodingRes.ScaleMethod_BicubicOpenCL)
			};

			Messenger.Default.Register<RotationChangedMessage>(
				this,
				message =>
				{
					this.RefreshOutputSize();
				});

			this.AutomaticChange = false;
		}

		private void RegisterProfileProperties()
		{
			// These actions fire when the user changes a property.
			this.RegisterProfileProperty(() => this.Profile.Width, () =>
			{
				if (this.Profile.Anamorphic == VCAnamorphic.None && this.KeepDisplayAspect && this.HasSourceData && this.Height > 0 && this.Width > 0)
				{
					var cropWidthAmount = this.CropLeft + this.CropRight;
					var cropHeightAmount = this.CropTop + this.CropBottom;
					var sourceWidth = this.SelectedTitle.Geometry.Width;
					var sourceHeight = this.SelectedTitle.Geometry.Height;
					var parWidth = this.SelectedTitle.Geometry.PAR.Num;
					var parHeight = this.SelectedTitle.Geometry.PAR.Den;

					double finalDisplayAspect = ((double)(sourceWidth - cropWidthAmount) * parWidth) / ((sourceHeight - cropHeightAmount) * parHeight);

					int newHeight = (int)(this.Width / finalDisplayAspect);
					newHeight = GetNearestValue(newHeight, DimensionsAutoSetModulus);
					if (newHeight > 0)
					{
						this.Height = newHeight;
					}
				}

				this.RefreshOutputSize();
				this.UpdatePreviewWindow();
			});

			this.RegisterProfileProperty(() => this.Profile.Height, () =>
			{
				if (this.Profile.Anamorphic == VCAnamorphic.None && this.KeepDisplayAspect && this.HasSourceData && this.Width > 0 && this.Height > 0)
				{
					var cropWidthAmount = this.CropLeft + this.CropRight;
					var cropHeightAmount = this.CropTop + this.CropBottom;
					var sourceWidth = this.SelectedTitle.Geometry.Width;
					var sourceHeight = this.SelectedTitle.Geometry.Height;
					var parWidth = this.SelectedTitle.Geometry.PAR.Num;
					var parHeight = this.SelectedTitle.Geometry.PAR.Den;

					double finalDisplayAspect = ((double)(sourceWidth - cropWidthAmount) * parWidth) / ((sourceHeight - cropHeightAmount) * parHeight);

					int newWidth = (int)(this.Height * finalDisplayAspect);
					newWidth = GetNearestValue(newWidth, DimensionsAutoSetModulus);
					if (newWidth > 0)
					{
						this.Width = newWidth;
					}
				}

				this.RefreshOutputSize();
				this.UpdatePreviewWindow();
			});

			this.RegisterProfileProperty(() => this.Profile.MaxWidth, () =>
			{
				this.RefreshOutputSize();
				this.UpdatePreviewWindow();
			});

			this.RegisterProfileProperty(() => this.Profile.MaxHeight, () =>
			{
				this.RefreshOutputSize();
				this.UpdatePreviewWindow();
			});

			this.RegisterProfileProperty(() => this.Profile.Anamorphic, () =>
			{
				if (this.Anamorphic == VCAnamorphic.Strict)
				{
					this.Width = 0;
					this.MaxWidth = 0;
				}

				if (this.Anamorphic == VCAnamorphic.Strict || this.Anamorphic == VCAnamorphic.Loose)
				{
					this.KeepDisplayAspect = true;

					this.Height = 0;
					this.MaxHeight = 0;
				}

				if (this.Profile.Anamorphic == VCAnamorphic.Custom)
				{
					this.KeepDisplayAspect = true;
					this.UseDisplayWidth = true;
				}

				this.RefreshOutputSize();
			});

			this.RegisterProfileProperty(() => this.Profile.KeepDisplayAspect, () =>
			{
				if (this.Profile.Anamorphic == VCAnamorphic.Custom)
				{
					if (this.KeepDisplayAspect && !this.UseDisplayWidth)
					{
						this.UseDisplayWidth = true;
					}

					if (this.UseDisplayWidth && this.DisplayWidth == 0)
					{
						this.PopulateDisplayWidth();
					}
				}

				this.RefreshOutputSize();
				this.UpdatePreviewWindow();
			});

			this.RegisterProfileProperty(() => this.Profile.ScaleMethod);

			this.RegisterProfileProperty(() => this.Profile.Modulus, () =>
			{
				this.RefreshOutputSize();
				this.UpdatePreviewWindow();
			});

			this.RegisterProfileProperty(() => this.Profile.UseDisplayWidth, () =>
			{
				if (this.UseDisplayWidth)
				{
					this.PopulateDisplayWidth();
				}
				else
				{
					this.PopulatePixelAspect();
				}

				this.RefreshOutputSize();
				this.UpdatePreviewWindow();
			});

			this.RegisterProfileProperty(() => this.Profile.DisplayWidth, () =>
			{
				this.RefreshOutputSize();
				this.UpdatePreviewWindow();
			});

			this.RegisterProfileProperty(() => this.Profile.PixelAspectX, () =>
			{
				this.RefreshOutputSize();
				this.UpdatePreviewWindow();
			});

			this.RegisterProfileProperty(() => this.Profile.PixelAspectY, () =>
			{
				this.RefreshOutputSize();
				this.UpdatePreviewWindow();
			});

			this.RegisterProfileProperty(() => this.Profile.CroppingType, () =>
			{
				this.RefreshOutputSize();
				this.UpdatePreviewWindow();
			});

			this.RegisterProfileProperty(() => this.CropTop, () =>
			{
				this.RefreshOutputSize();
				this.UpdatePreviewWindow();
			});

			this.RegisterProfileProperty(() => this.CropBottom, () =>
			{
				this.RefreshOutputSize();
				this.UpdatePreviewWindow();
			});

			this.RegisterProfileProperty(() => this.CropLeft, () =>
			{
				this.RefreshOutputSize();
				this.UpdatePreviewWindow();
			});

			this.RegisterProfileProperty(() => this.CropRight, () =>
			{
				this.RefreshOutputSize();
				this.UpdatePreviewWindow();
			});
		}

		private ObservableAsPropertyHelper<string> inputSourceResolution;
		public string InputSourceResolution
		{
			get { return this.inputSourceResolution.Value; }
		}

		private ObservableAsPropertyHelper<string> inputPixelAspectRatio;
		public string InputPixelAspectRatio
		{
			get { return this.inputPixelAspectRatio.Value; }
		}

		private ObservableAsPropertyHelper<string> inputDisplayResolution;
		public string InputDisplayResolution
		{
			get { return this.inputDisplayResolution.Value; }
		}

		private string outputSourceResolution;
		public string OutputSourceResolution
		{
			get { return this.outputSourceResolution; }
			set { this.RaiseAndSetIfChanged(ref this.outputSourceResolution, value); }
		}

		private string outputPixelAspectRatio;
		public string OutputPixelAspectRatio
		{
			get { return this.outputPixelAspectRatio; }
			set { this.RaiseAndSetIfChanged(ref this.outputPixelAspectRatio, value); }
		}

		private string outputDisplayResolution;
		public string OutputDisplayResolution
		{
			get { return this.outputDisplayResolution; }
			set { this.RaiseAndSetIfChanged(ref this.outputDisplayResolution, value); }
		}

		public int Width
		{
			get { return this.Profile.Width; }
			set { this.UpdateProfileProperty(() => this.Profile.Width, value); }
		}

		private ObservableAsPropertyHelper<bool> widthEnabled;
		public bool WidthEnabled
		{
			get { return this.widthEnabled.Value; }
		}

		public int Height
		{
			get { return this.Profile.Height; }
			set { this.UpdateProfileProperty(() => this.Profile.Height, value); }
		}

		private ObservableAsPropertyHelper<bool> heightEnabled;
		public bool HeightEnabled
		{
			get { return this.heightEnabled.Value; }
		}

		public int MaxWidth
		{
			get { return this.Profile.MaxWidth; }
			set { this.UpdateProfileProperty(() => this.Profile.MaxWidth, value); }
		}

		public int MaxHeight
		{
			get { return this.Profile.MaxHeight; }
			set { this.UpdateProfileProperty(() => this.Profile.MaxHeight, value); }
		}

		public bool KeepDisplayAspect
		{
			get { return this.Profile.KeepDisplayAspect; }
			set { this.UpdateProfileProperty(() => this.Profile.KeepDisplayAspect, value); }
		}

		private ObservableAsPropertyHelper<bool> keepDisplayAspectEnabled;
		public bool KeepDisplayAspectEnabled
		{
			get { return this.keepDisplayAspectEnabled.Value; }
		}

		public List<ComboChoice<VCScaleMethod>> ScaleChoices
		{
			get
			{
				return this.scaleChoices;
			}
		}

		public VCScaleMethod ScaleMethod
		{
			get { return this.Profile.ScaleMethod; }
			set { this.UpdateProfileProperty(() => this.Profile.ScaleMethod, value); }
		}

		public VCAnamorphic Anamorphic
		{
			get { return this.Profile.Anamorphic; }
			set { this.UpdateProfileProperty(() => this.Profile.Anamorphic, value); }
		}

		private ObservableAsPropertyHelper<bool> customAnamorphicFieldsVisible;
		public bool CustomAnamorphicFieldsVisible
		{
			get { return this.customAnamorphicFieldsVisible.Value; }
		}

		public int Modulus
		{
			get { return this.Profile.Modulus; }
			set { this.UpdateProfileProperty(() => this.Profile.Modulus, value); }
		}

		private ObservableAsPropertyHelper<bool> modulusVisible;
		public bool ModulusVisible
		{
			get { return this.modulusVisible.Value; }
		}

		public bool UseDisplayWidth
		{
			get { return this.Profile.UseDisplayWidth; }
			set { this.UpdateProfileProperty(() => this.Profile.UseDisplayWidth, value); }
		}

		public int DisplayWidth
		{
			get { return this.Profile.DisplayWidth; }
			set { this.UpdateProfileProperty(() => this.Profile.DisplayWidth, value); }
		}

		private ObservableAsPropertyHelper<bool> pixelAspectVisible;
		public bool PixelAspectVisible
		{
			get { return this.pixelAspectVisible.Value; }
		}

		public int PixelAspectX
		{
			get { return this.Profile.PixelAspectX; }
			set { this.UpdateProfileProperty(() => this.Profile.PixelAspectX, value); }
		}

		public int PixelAspectY
		{
			get { return this.Profile.PixelAspectY; }
			set { this.UpdateProfileProperty(() => this.Profile.PixelAspectY, value); }
		}

		public VCCroppingType CroppingType
		{
			get { return this.Profile.CroppingType; }
			set { this.UpdateProfileProperty(() => this.Profile.CroppingType, value); }
		}

		private ObservableAsPropertyHelper<bool> croppingUIEnabled;
		public bool CroppingUIEnabled
		{
			get { return this.croppingUIEnabled.Value; }
		}

		private int cropTop;
		public int CropTop
		{
			get { return this.cropTop; }
			set { this.RaiseAndSetIfChanged(ref this.cropTop, value); }
		}

		private int cropBottom;
		public int CropBottom
		{
			get { return this.cropBottom; }
			set { this.RaiseAndSetIfChanged(ref this.cropBottom, value); }
		}

		private int cropLeft;
		public int CropLeft
		{
			get { return this.cropLeft; }
			set { this.RaiseAndSetIfChanged(ref this.cropLeft, value); }
		}

		private int cropRight;
		public int CropRight
		{
			get { return this.cropRight; }
			set { this.RaiseAndSetIfChanged(ref this.cropRight, value); }
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

				Geometry outputGeometry = JsonEncodeFactory.GetAnamorphicSize(this.Profile, this.SelectedTitle);

				int width = outputGeometry.Width;
				int height = outputGeometry.Height;
				int parWidth = outputGeometry.PAR.Num;
				int parHeight = outputGeometry.PAR.Den;

                if (this.Profile.Rotation == VCPictureRotation.Clockwise90 || this.Profile.Rotation == VCPictureRotation.Clockwise270)
				{
					int temp = width;
					width = height;
					height = temp;

					temp = parWidth;
					parWidth = parHeight;
					parHeight = temp;
				}

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

		private void PopulatePixelAspect()
		{
			if (this.SelectedTitle == null)
			{
				this.PixelAspectX = 1;
				this.PixelAspectY = 1;
			}
			else
			{
				this.PixelAspectX = this.SelectedTitle.Geometry.PAR.Num;
				this.PixelAspectY = this.SelectedTitle.Geometry.PAR.Den;
			}
		}

		private void PopulateDisplayWidth()
		{
			if (this.KeepDisplayAspect)
			{
				// Let display width be "auto" if we're set to keep the aspect ratio
				this.DisplayWidth = 0;
			}
			else
			{
				// Populate it with something sensible if the set display width must be used
				if (this.SelectedTitle == null)
				{
					this.DisplayWidth = 853;
				}
				else
				{
					this.DisplayWidth = (int)Math.Round(((double)((this.SelectedTitle.Geometry.Width - this.CropLeft - this.CropRight) * this.SelectedTitle.Geometry.PAR.Num)) / this.SelectedTitle.Geometry.PAR.Den);
				}
			}
		}

		private string CreateParDisplayString(int parWidth, int parHeight)
		{
			double pixelAspectRatio = ((double)parWidth) / parHeight;
			return pixelAspectRatio.ToString("F2") + " (" + parWidth + "/" + parHeight + ")";
		}
	}
}
