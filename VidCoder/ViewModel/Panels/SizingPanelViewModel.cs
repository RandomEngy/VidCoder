using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reactive.Linq;
using System.Windows.Media;
using HandBrake.ApplicationServices.Interop.Json.Shared;
using VidCoder.Model;
using VidCoder.Resources;
using VidCoder.Services;
using VidCoderCommon.Model;
using ReactiveUI;
using VidCoder.ViewModel.DataModels;

namespace VidCoder.ViewModel
{
	public class SizingPanelViewModel : PanelViewModel
	{
		private const int DimensionsAutoSetModulus = 2;

		private OutputSizeService outputSizeService = Ioc.Get<OutputSizeService>();
		private PreviewUpdateService previewUpdateService = Ioc.Get<PreviewUpdateService>();

		public SizingPanelViewModel(EncodingWindowViewModel encodingWindowViewModel)
			: base(encodingWindowViewModel)
		{
			this.AutomaticChange = true;

			this.RotationChoices = new List<RotationViewModel>
			{
				new RotationViewModel { Rotation = VCPictureRotation.None, Display = CommonRes.None, Image = "/Icons/Empty.png", ShowImage = false },
				new RotationViewModel { Rotation = VCPictureRotation.Clockwise90, Display = EncodingRes.Rotation_Clockwise90, Image = "/Icons/rotate_90_cw.png"},
				new RotationViewModel { Rotation = VCPictureRotation.Clockwise270, Display = EncodingRes.Rotation_Counterclockwise90, Image = "/Icons/rotate_90_ccw.png" },
				new RotationViewModel { Rotation = VCPictureRotation.Clockwise180, Display = EncodingRes.Rotation_180, Image = "/Icons/rotate_180.png" }
			};

			this.SizingModeChoices = new List<ComboChoice<VCSizingMode>>
			{
				new ComboChoice<VCSizingMode>(VCSizingMode.Automatic, CommonRes.Automatic),
				new ComboChoice<VCSizingMode>(VCSizingMode.Manual, EncodingRes.SizingModeManual),
			};

			this.ScalingModeChoices = new List<ComboChoice<VCScalingMode>>
			{
				new ComboChoice<VCScalingMode>(VCScalingMode.DownscaleOnly, EncodingRes.ScalingMode_DownscaleOnly),
				new ComboChoice<VCScalingMode>(VCScalingMode.UpscaleFill, EncodingRes.ScalingMode_Fill),
				new ComboChoice<VCScalingMode>(VCScalingMode.Upscale2X, string.Format(EncodingRes.UpscaleMaxFormat, 2)),
				new ComboChoice<VCScalingMode>(VCScalingMode.Upscale3X, string.Format(EncodingRes.UpscaleMaxFormat, 3)),
				new ComboChoice<VCScalingMode>(VCScalingMode.Upscale4X, string.Format(EncodingRes.UpscaleMaxFormat, 4)),
			};

			this.PaddingModeChoices = new List<ComboChoice<VCPaddingMode>>
			{
				new ComboChoice<VCPaddingMode>(VCPaddingMode.None, CommonRes.None),
				new ComboChoice<VCPaddingMode>(VCPaddingMode.Fill, EncodingRes.PaddingMode_Fill),
				new ComboChoice<VCPaddingMode>(VCPaddingMode.Width, EncodingRes.PaddingMode_Width),
				new ComboChoice<VCPaddingMode>(VCPaddingMode.Height, EncodingRes.PaddingMode_Height),
				new ComboChoice<VCPaddingMode>(VCPaddingMode.Custom, CommonRes.Custom),
			};

			this.RegisterProfileProperties();

			// WidthLabel
			this.WhenAnyValue(x => x.SizingMode, x => x.PaddingMode, x => x.ScalingMode, (sizingMode, paddingMode, scalingMode) =>
			{
				if (sizingMode == VCSizingMode.Manual)
				{
					return EncodingRes.WidthLabel;
				}

				switch (paddingMode)
				{
					case VCPaddingMode.Fill:
					case VCPaddingMode.Width:
						return EncodingRes.WidthLabel;
					case VCPaddingMode.Height:
						return EncodingRes.MaxWidthLabel;
					case VCPaddingMode.None:
					case VCPaddingMode.Custom:
						switch (scalingMode)
						{
							case VCScalingMode.UpscaleFill:
								return EncodingRes.TargetWidthLabel;
							default:
								return EncodingRes.MaxWidthLabel;
						}
				}

				return string.Empty;
			}).ToProperty(this, x => x.WidthLabel, out this.widthLabel);

			// HeightLabel
			this.WhenAnyValue(x => x.SizingMode, x => x.PaddingMode, x => x.ScalingMode, (sizingMode, paddingMode, scalingMode) =>
			{
				if (sizingMode == VCSizingMode.Manual)
				{
					return EncodingRes.HeightLabel;
				}

				switch (paddingMode)
				{
					case VCPaddingMode.Fill:
					case VCPaddingMode.Height:
						return EncodingRes.HeightLabel;
					case VCPaddingMode.Width:
						return EncodingRes.MaxHeightLabel;
					case VCPaddingMode.None:
					case VCPaddingMode.Custom:
						switch (scalingMode)
						{
							case VCScalingMode.UpscaleFill:
								return EncodingRes.TargetHeightLabel;
							default:
								return EncodingRes.MaxHeightLabel;
						}
				}

				return string.Empty;
			}).ToProperty(this, x => x.HeightLabel, out this.heightLabel);

			// AllowEmptyResolution
			this.WhenAnyValue(x => x.SizingMode, x => x.PaddingMode, x => x.ScalingMode, (sizingMode, paddingMode, scalingMode) =>
			{
				if (sizingMode == VCSizingMode.Manual)
				{
					return false;
				}

				switch (paddingMode)
				{
					case VCPaddingMode.None:
					case VCPaddingMode.Custom:
						return scalingMode != VCScalingMode.UpscaleFill;;
					default:
						return false;
				}
			}).ToProperty(this, x => x.AllowEmptyResolution, out this.allowEmptyResolution);

			// InputPreview
			this.MainViewModel.WhenAnyValue(x => x.SelectedTitle).Select(selectedTitle =>
			{
				return ResolutionUtilities.GetResolutionInfoLines(selectedTitle);
			}).ToProperty(this, x => x.InputPreview, out this.inputPreview);

			// OutputPreview
			this.outputSizeService.WhenAnyValue(x => x.Size).Select(size =>
			{
				var previewLines = new List<InfoLineViewModel>();
				if (size == null)
				{
					return previewLines;
				}

				string outputStorageResolutionString = size.OutputWidth + " x " + size.OutputHeight;
				if (size.Par.Num == size.Par.Den)
				{
					previewLines.Add(new InfoLineViewModel(EncodingRes.ResolutionLabel, outputStorageResolutionString));
				}
				else
				{
					previewLines.Add(new InfoLineViewModel(EncodingRes.StorageResolutionLabel, outputStorageResolutionString));
					previewLines.Add(new InfoLineViewModel(EncodingRes.PixelAspectRatioLabel, ResolutionUtilities.CreateParDisplayString(size.Par.Num, size.Par.Den)));

					string displayResolutionString = Math.Round(size.OutputWidth * (((double)size.Par.Num) / size.Par.Den)) + " x " + size.OutputHeight;
					previewLines.Add(new InfoLineViewModel(EncodingRes.DisplayResolutionLabel, displayResolutionString));
				}

				return previewLines;
			}).ToProperty(this, x => x.OutputPreview, out this.outputPreview);

			// PadColorEnabled
			this.WhenAnyValue(x => x.PaddingMode, paddingMode =>
			{
				return paddingMode != VCPaddingMode.None;
			}).ToProperty(this, x => x.PadColorEnabled, out this.padColorEnabled);

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

			// PaddingUIEnabled
			this.WhenAnyValue(x => x.SizingMode, x => x.PaddingMode, (sizingMode, paddingMode) =>
			{
				if (sizingMode == VCSizingMode.Manual)
				{
					return true;
				}

				return paddingMode == VCPaddingMode.Custom;
			}).ToProperty(this, x => x.PaddingUIEnabled, out this.paddingUIEnabled);

			// Update the underlying profile when our local Pad properties change.
			this.WhenAnyValue(x => x.PadTop)
				.Skip(1)
				.Subscribe(padTop =>
				{
					this.UpdateProfileProperty(
						() => this.Profile.Padding,
						nameof(this.Profile.Padding.Top),
						nameof(this.PadTop),
						padTop,
						raisePropertyChanged: false);
				});
			this.WhenAnyValue(x => x.PadBottom)
				.Skip(1)
				.Subscribe(padBottom =>
				{
					this.UpdateProfileProperty(
						() => this.Profile.Padding,
						nameof(this.Profile.Padding.Bottom),
						nameof(this.PadBottom),
						padBottom,
						raisePropertyChanged: false);
				});
			this.WhenAnyValue(x => x.PadLeft)
				.Skip(1)
				.Subscribe(padLeft =>
				{
					this.UpdateProfileProperty(
						() => this.Profile.Padding,
						nameof(this.Profile.Padding.Left),
						nameof(this.PadLeft),
						padLeft,
						raisePropertyChanged: false);
				});
			this.WhenAnyValue(x => x.PadRight)
				.Skip(1)
				.Subscribe(padRight =>
				{
					this.UpdateProfileProperty(
						() => this.Profile.Padding,
						nameof(this.Profile.Padding.Right),
						nameof(this.PadRight),
						padRight,
						raisePropertyChanged: false);
				});

			// Auto-fill the padding properties when UI is disabled
			this.WhenAnyValue(
				x => x.SizingMode,
				x => x.ScalingMode,
				x => x.PaddingMode,
				x => x.Width,
				x => x.Height,
				x => x.MainViewModel.SelectedTitle,
				(sizingMode, scalingMode, paddingMode, width, height, selectedTitle) =>
				{
					return new { sizingMode, scalingMode, paddingMode, width, height, selectedTitle };
				})
				.Subscribe(x =>
				{
					bool oldAutoValue = this.AutomaticChange;

					if (x.sizingMode == VCSizingMode.Automatic && x.paddingMode == VCPaddingMode.None)
					{
						this.AutomaticChange = true;
						this.PadTop = 0;
						this.PadBottom = 0;
						this.PadLeft = 0;
						this.PadRight = 0;
					}
					else if (x.sizingMode == VCSizingMode.Automatic && x.paddingMode != VCPaddingMode.Custom)
					{
						if (x.selectedTitle == null)
						{
							this.PadTop = 0;
							this.PadBottom = 0;
							this.PadLeft = 0;
							this.PadRight = 0;
						}
						else
						{
							// Calculate the correct padding from input variables
							OutputSizeInfo outputSize = JsonEncodeFactory.GetOutputSize(this.Profile, x.selectedTitle);
							this.PadTop = outputSize.Padding.Top;
							this.PadBottom = outputSize.Padding.Bottom;
							this.PadLeft = outputSize.Padding.Left;
							this.PadRight = outputSize.Padding.Right;
						}
					}

					this.AutomaticChange = oldAutoValue;
				});

			// When the preset changes, update the local pad values.
			this.PresetsService.WhenAnyValue(x => x.SelectedPreset.Preset.EncodingProfile)
				.Subscribe(_ =>
				{
					VCProfile profile = this.PresetsService.SelectedPreset.Preset.EncodingProfile;

					if (profile.SizingMode == VCSizingMode.Manual || profile.PaddingMode == VCPaddingMode.Custom)
					{
						bool oldAutoValue = this.AutomaticChange;
						this.AutomaticChange = true;
						var padding = profile.Padding;
						this.PadLeft = padding.Left;
						this.PadTop = padding.Top;
						this.PadRight = padding.Right;
						this.PadBottom = padding.Bottom;
						this.AutomaticChange = oldAutoValue;
					}
				});


			this.AutomaticChange = false;
		}

		private void RegisterProfileProperties()
		{
			// These actions fire when the user changes a property.
			this.RegisterProfileProperty(nameof(this.Profile.Width), this.RefreshOutputSize);
			this.RegisterProfileProperty(nameof(this.Profile.Height), this.RefreshOutputSize);
			this.RegisterProfileProperty(nameof(this.Profile.Modulus), this.RefreshOutputSize);
			this.RegisterProfileProperty(nameof(this.Profile.PixelAspectX), this.RefreshOutputSize);
			this.RegisterProfileProperty(nameof(this.Profile.PixelAspectY), this.RefreshOutputSize);
			this.RegisterProfileProperty(nameof(this.Profile.CroppingType), this.RefreshOutputSize);
			this.RegisterProfileProperty(nameof(this.CropTop), this.RefreshOutputSize);
			this.RegisterProfileProperty(nameof(this.CropBottom), this.RefreshOutputSize);
			this.RegisterProfileProperty(nameof(this.CropLeft), this.RefreshOutputSize);
			this.RegisterProfileProperty(nameof(this.CropRight), this.RefreshOutputSize);
			this.RegisterProfileProperty(nameof(this.Rotation), this.RefreshOutputSize);
			this.RegisterProfileProperty(nameof(this.FlipHorizontal), () => this.previewUpdateService.RefreshPreview());
			this.RegisterProfileProperty(nameof(this.FlipVertical), () => this.previewUpdateService.RefreshPreview());
			this.RegisterProfileProperty(nameof(this.SizingMode), () =>
			{
				if (this.SizingMode == VCSizingMode.Manual)
				{
					if (this.Width == 0 || this.Height == 0)
					{
						this.Width = JsonEncodeFactory.DefaultMaxWidth;
						this.Height = JsonEncodeFactory.DefaultMaxHeight;
					}

					this.PixelAspectX = 1;
					this.PixelAspectY = 1;
				}

				this.RefreshOutputSize();
			});
			this.RegisterProfileProperty(nameof(this.ScalingMode), () =>
			{
				this.EnsureResolutionPopulatedIfRequired();
				this.RefreshOutputSize();
			});
			this.RegisterProfileProperty(nameof(this.PaddingMode), () =>
			{
				this.EnsureResolutionPopulatedIfRequired();
				this.RefreshOutputSize();
			});
			this.RegisterProfileProperty(nameof(this.PadTop), this.RefreshOutputSize);
			this.RegisterProfileProperty(nameof(this.PadBottom), this.RefreshOutputSize);
			this.RegisterProfileProperty(nameof(this.PadLeft), this.RefreshOutputSize);
			this.RegisterProfileProperty(nameof(this.PadRight), this.RefreshOutputSize);
			this.RegisterProfileProperty(nameof(this.UseAnamorphic), this.RefreshOutputSize);
			this.RegisterProfileProperty(nameof(this.PadColor), () =>
			{
				this.previewUpdateService.RefreshPreview();
			});
		}

		private ObservableAsPropertyHelper<List<InfoLineViewModel>> inputPreview;
		public List<InfoLineViewModel> InputPreview => this.inputPreview.Value;

		private ObservableAsPropertyHelper<List<InfoLineViewModel>> outputPreview;
		public List<InfoLineViewModel> OutputPreview => this.outputPreview.Value;

		public List<ComboChoice<VCSizingMode>> SizingModeChoices { get; }

		public VCSizingMode SizingMode
		{
			get { return this.Profile.SizingMode; }
			set { this.UpdateProfileProperty(nameof(this.Profile.SizingMode), value);}
		}

		private ObservableAsPropertyHelper<string> widthLabel;
		public string WidthLabel => this.widthLabel.Value;

		private ObservableAsPropertyHelper<string> heightLabel;
		public string HeightLabel => this.heightLabel.Value;

		public int Width
		{
			get { return this.Profile.Width; }
			set { this.UpdateProfileProperty(nameof(this.Profile.Width), value); }
		}

		public int Height
		{
			get { return this.Profile.Height; }
			set { this.UpdateProfileProperty(nameof(this.Profile.Height), value); }
		}

		private ObservableAsPropertyHelper<bool> allowEmptyResolution;
		public bool AllowEmptyResolution => this.allowEmptyResolution.Value;

		public bool UseAnamorphic
		{
			get { return this.Profile.UseAnamorphic; }
			set { this.UpdateProfileProperty(nameof(this.Profile.UseAnamorphic), value); }
		}

		public List<ComboChoice<VCScalingMode>> ScalingModeChoices { get; }

		public VCScalingMode ScalingMode
		{
			get { return this.Profile.ScalingMode; }
			set { this.UpdateProfileProperty(nameof(this.Profile.ScalingMode), value); }
		}

		public List<ComboChoice<VCPaddingMode>> PaddingModeChoices { get; }

		public VCPaddingMode PaddingMode
		{
			get { return this.Profile.PaddingMode; }
			set { this.UpdateProfileProperty(nameof(this.Profile.PaddingMode), value); }
		}

		private ObservableAsPropertyHelper<bool> paddingUIEnabled;
		public bool PaddingUIEnabled => this.paddingUIEnabled.Value;

		private int padTop;
		public int PadTop
		{
			get { return this.padTop; }
			set { this.RaiseAndSetIfChanged(ref this.padTop, value); }
		}

		private int padBottom;
		public int PadBottom
		{
			get { return this.padBottom; }
			set { this.RaiseAndSetIfChanged(ref this.padBottom, value); }
		}

		private int padLeft;
		public int PadLeft
		{
			get { return this.padLeft; }
			set { this.RaiseAndSetIfChanged(ref this.padLeft, value); }
		}

		private int padRight;
		public int PadRight
		{
			get { return this.padRight; }
			set { this.RaiseAndSetIfChanged(ref this.padRight, value); }
		}

		public Color PadColor
		{
			get { return ColorUtilities.ToWindowsColor(this.Profile.PadColor); }
			set { this.UpdateProfileProperty(nameof(this.Profile.PadColor), ColorUtilities.ToHexString(value)); }
		}

		private ObservableAsPropertyHelper<bool> padColorEnabled;
		public bool PadColorEnabled => this.padColorEnabled.Value;

		public int Modulus
		{
			get { return this.Profile.Modulus; }
			set { this.UpdateProfileProperty(nameof(this.Profile.Modulus), value); }
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

		public List<RotationViewModel> RotationChoices { get; }

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
			this.previewUpdateService.RefreshPreview();
		}

		private void EnsureResolutionPopulatedIfRequired()
		{
			if (!this.AllowEmptyResolution && (this.Width == 0 || this.Height == 0))
			{
				this.Width = JsonEncodeFactory.DefaultMaxWidth;
				this.Height = JsonEncodeFactory.DefaultMaxHeight;
			}
		}
	}
}
