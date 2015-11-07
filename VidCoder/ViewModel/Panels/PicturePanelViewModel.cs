using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reactive.Linq;
using HandBrake.ApplicationServices.Interop.Json.Shared;
using VidCoder.Model;
using VidCoder.Resources;
using VidCoder.Services;
using VidCoderCommon.Model;
using ReactiveUI;

namespace VidCoder.ViewModel
{
	public class PicturePanelViewModel : PanelViewModel
	{
		private const int DimensionsAutoSetModulus = 2;

        private List<ComboChoice<VCScaleMethod>> scaleChoices;

		private OutputSizeService outputSizeService = Ioc.Get<OutputSizeService>();

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
				return anamorphic == VCAnamorphic.None;
			}).ToProperty(this, x => x.KeepDisplayAspectEnabled, out this.keepDisplayAspectEnabled);

			// CustomAnamorphicFieldsVisible
			this.WhenAnyValue(x => x.Anamorphic, anamorphic =>
			{
				return anamorphic == VCAnamorphic.Custom;
			}).ToProperty(this, x => x.CustomAnamorphicFieldsVisible, out this.customAnamorphicFieldsVisible);

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

			// OutputSourceResolution
			this.outputSizeService.WhenAnyValue(x => x.Size, size =>
			{
				if (size == null)
				{
					return string.Empty;
				}

				return size.Width + " x " + size.Height;
			}).ToProperty(this, x => x.OutputSourceResolution, out this.outputSourceResolution);

			// OutputPixelAspectRatio
			this.outputSizeService.WhenAnyValue(x => x.Size, size =>
			{
				if (size == null)
				{
					return string.Empty;
				}

				return size.PAR.Num + "/" + size.PAR.Den;
			}).ToProperty(this, x => x.OutputPixelAspectRatio, out this.outputPixelAspectRatio);

			// OutputDisplayResolution
			this.outputSizeService.WhenAnyValue(x => x.Size, size =>
			{
				if (size == null)
				{
					return string.Empty;
				}

				return Math.Round(size.Width * (((double) size.PAR.Num) / size.PAR.Den)) + " x " + size.Height;
			}).ToProperty(this, x => x.OutputDisplayResolution, out this.outputDisplayResolution);

			// CroppingUIEnabled
			this.WhenAnyValue(x => x.CroppingType, croppingType =>
			{
				return croppingType == VCCroppingType.Custom;
			}).ToProperty(this, x => x.CroppingUIEnabled, out this.croppingUIEnabled);

			// Update the underlying profile when our local Crop properties change. This only applies for
			// CroppingType.Custom
			this.WhenAnyValue(x => x.CropTop)
				.Skip(1)
				.Subscribe(cropTop =>
				{
					this.UpdateProfileProperty(
						() => this.Profile.Cropping,
						nameof(this.Profile.Cropping.Top),
						nameof(this.CropTop), 
						cropTop,
						raisePropertyChanged: false);
				});
			this.WhenAnyValue(x => x.CropBottom)
				.Skip(1)
				.Subscribe(cropBottom =>
				{
					this.UpdateProfileProperty(
						() => this.Profile.Cropping,
						nameof(this.Profile.Cropping.Bottom),
						nameof(this.CropBottom),
						cropBottom,
						raisePropertyChanged: false);
				});
			this.WhenAnyValue(x => x.CropLeft)
				.Skip(1)
				.Subscribe(cropLeft =>
				{
					this.UpdateProfileProperty(
						() => this.Profile.Cropping,
						nameof(this.Profile.Cropping.Left),
						nameof(this.CropLeft),
						cropLeft,
						raisePropertyChanged: false);
				});
			this.WhenAnyValue(x => x.CropRight)
				.Skip(1)
				.Subscribe(cropRight =>
				{
					this.UpdateProfileProperty(
						() => this.Profile.Cropping,
						nameof(this.Profile.Cropping.Right),
						nameof(this.CropRight),
						cropRight,
						raisePropertyChanged: false);
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
					bool oldAutoValue = this.AutomaticChange;

					if (x.croppingType == VCCroppingType.None)
					{
						this.AutomaticChange = true;
						this.CropTop = 0;
						this.CropBottom = 0;
						this.CropLeft = 0;
						this.CropRight = 0;
					}
					else if (x.croppingType == VCCroppingType.Automatic)
					{
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
					}

					this.AutomaticChange = oldAutoValue;
				});

			// When the preset changes, update the local crop values for Custom.
			this.PresetsService.WhenAnyValue(x => x.SelectedPreset.Preset.EncodingProfile)
				.Subscribe(_ =>
				{
					if (this.PresetsService.SelectedPreset.Preset.EncodingProfile.CroppingType == VCCroppingType.Custom)
					{
						bool oldAutoValue = this.AutomaticChange;
						this.AutomaticChange = true;
						var cropping = this.PresetsService.SelectedPreset.Preset.EncodingProfile.Cropping;
						this.CropLeft = cropping.Left;
						this.CropTop = cropping.Top;
						this.CropRight = cropping.Right;
						this.CropBottom = cropping.Bottom;
						this.AutomaticChange = oldAutoValue;
					}
				});

			this.scaleChoices = new List<ComboChoice<VCScaleMethod>>
			{
				new ComboChoice<VCScaleMethod>(VCScaleMethod.Lanczos, EncodingRes.ScaleMethod_Lanczos),
				new ComboChoice<VCScaleMethod>(VCScaleMethod.Bicubic, EncodingRes.ScaleMethod_BicubicOpenCL)
			};

			this.AutomaticChange = false;
		}

		private void RegisterProfileProperties()
		{
			// These actions fire when the user changes a property.
			this.RegisterProfileProperty(nameof(this.Profile.Width), () =>
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
			});

			this.RegisterProfileProperty(nameof(this.Profile.Height), () =>
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
			});

			this.RegisterProfileProperty(nameof(this.Profile.MaxWidth), () =>
			{
				this.RefreshOutputSize();
			});

			this.RegisterProfileProperty(nameof(this.Profile.MaxHeight), () =>
			{
				this.RefreshOutputSize();
			});

			this.RegisterProfileProperty(nameof(this.Profile.Anamorphic), () =>
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
					this.KeepDisplayAspect = false;
					this.UseDisplayWidth = true;

					if (this.DisplayWidth == 0)
					{
						this.PopulateDisplayWidth();
					}
				}

				this.RefreshOutputSize();
			});

			this.RegisterProfileProperty(nameof(this.Profile.KeepDisplayAspect), () =>
			{
				this.RefreshOutputSize();
			});

			this.RegisterProfileProperty(nameof(this.Profile.ScaleMethod));

			this.RegisterProfileProperty(nameof(this.Profile.Modulus), () =>
			{
				this.RefreshOutputSize();
			});

			this.RegisterProfileProperty(nameof(this.Profile.UseDisplayWidth), () =>
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
			});


			this.RegisterProfileProperty(nameof(this.Profile.DisplayWidth), () =>
			{
				this.RefreshOutputSize();
			});

			this.RegisterProfileProperty(nameof(this.Profile.PixelAspectX), () =>
			{
				this.RefreshOutputSize();
			});

			this.RegisterProfileProperty(nameof(this.Profile.PixelAspectY), () =>
			{
				this.RefreshOutputSize();
			});

			this.RegisterProfileProperty(nameof(this.Profile.CroppingType), () =>
			{
				this.RefreshOutputSize();
			});

			this.RegisterProfileProperty(nameof(this.CropTop), () =>
			{
				this.RefreshOutputSize();
			});

			this.RegisterProfileProperty(nameof(this.CropBottom), () =>
			{
				this.RefreshOutputSize();
			});

			this.RegisterProfileProperty(nameof(this.CropLeft), () =>
			{
				this.RefreshOutputSize();
			});

			this.RegisterProfileProperty(nameof(this.CropRight), () =>
			{
				this.RefreshOutputSize();
			});
		}

		private ObservableAsPropertyHelper<string> inputSourceResolution;
		public string InputSourceResolution => this.inputSourceResolution.Value;

		private ObservableAsPropertyHelper<string> inputPixelAspectRatio;
		public string InputPixelAspectRatio => this.inputPixelAspectRatio.Value;

		private ObservableAsPropertyHelper<string> inputDisplayResolution;
		public string InputDisplayResolution => this.inputDisplayResolution.Value;

		private ObservableAsPropertyHelper<string> outputSourceResolution;
		public string OutputSourceResolution => this.outputSourceResolution.Value;

		private ObservableAsPropertyHelper<string> outputPixelAspectRatio;
		public string OutputPixelAspectRatio => this.outputPixelAspectRatio.Value;

		private ObservableAsPropertyHelper<string> outputDisplayResolution;
		public string OutputDisplayResolution => this.outputDisplayResolution.Value;

		public int Width
		{
			get { return this.Profile.Width; }
			set { this.UpdateProfileProperty(nameof(this.Profile.Width), value); }
		}

		private ObservableAsPropertyHelper<bool> widthEnabled;
		public bool WidthEnabled => this.widthEnabled.Value;

		public int Height
		{
			get { return this.Profile.Height; }
			set { this.UpdateProfileProperty(nameof(this.Profile.Height), value); }
		}

		private ObservableAsPropertyHelper<bool> heightEnabled;
		public bool HeightEnabled => this.heightEnabled.Value;

		public int MaxWidth
		{
			get { return this.Profile.MaxWidth; }
			set { this.UpdateProfileProperty(nameof(this.Profile.MaxWidth), value); }
		}

		public int MaxHeight
		{
			get { return this.Profile.MaxHeight; }
			set { this.UpdateProfileProperty(nameof(this.Profile.MaxHeight), value); }
		}

		public bool KeepDisplayAspect
		{
			get { return this.Profile.KeepDisplayAspect; }
			set { this.UpdateProfileProperty(nameof(this.Profile.KeepDisplayAspect), value); }
		}

		private ObservableAsPropertyHelper<bool> keepDisplayAspectEnabled;
		public bool KeepDisplayAspectEnabled => this.keepDisplayAspectEnabled.Value;

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
			set { this.UpdateProfileProperty(nameof(this.Profile.ScaleMethod), value); }
		}

		public VCAnamorphic Anamorphic
		{
			get { return this.Profile.Anamorphic; }
			set { this.UpdateProfileProperty(nameof(this.Profile.Anamorphic), value); }
		}

		private ObservableAsPropertyHelper<bool> customAnamorphicFieldsVisible;
		public bool CustomAnamorphicFieldsVisible => this.customAnamorphicFieldsVisible.Value;

		public int Modulus
		{
			get { return this.Profile.Modulus; }
			set { this.UpdateProfileProperty(nameof(this.Profile.Modulus), value); }
		}

		private ObservableAsPropertyHelper<bool> modulusVisible;
		public bool ModulusVisible => this.modulusVisible.Value;

		public bool UseDisplayWidth
		{
			get { return this.Profile.UseDisplayWidth; }
			set { this.UpdateProfileProperty(nameof(this.Profile.UseDisplayWidth), value); }
		}

		public int DisplayWidth
		{
			get { return this.Profile.DisplayWidth; }
			set { this.UpdateProfileProperty(nameof(this.Profile.DisplayWidth), value); }
		}

		public int PixelAspectX
		{
			get { return this.Profile.PixelAspectX; }
			set { this.UpdateProfileProperty(nameof(this.Profile.PixelAspectX), value); }
		}

		public int PixelAspectY
		{
			get { return this.Profile.PixelAspectY; }
			set { this.UpdateProfileProperty(nameof(this.Profile.PixelAspectY), value); }
		}

		public VCCroppingType CroppingType
		{
			get { return this.Profile.CroppingType; }
			set { this.UpdateProfileProperty(nameof(this.Profile.CroppingType), value); }
		}

		private ObservableAsPropertyHelper<bool> croppingUIEnabled;
		public bool CroppingUIEnabled => this.croppingUIEnabled.Value;

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
			this.outputSizeService.Refresh();
		}

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
