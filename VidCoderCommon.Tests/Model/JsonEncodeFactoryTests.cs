using HandBrake.Interop.Interop.Json.Scan;
using HandBrake.Interop.Interop.Json.Shared;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using Shouldly;
using System;
using System.Collections.Generic;
using VidCoderCommon.Model;
using VidCoderCommon.Services;

namespace VidCoderCommon.Tests.Model;

/// <summary>
/// The tests need to run in 64-bit mode to work with the HandBrake Interop DLL platform target.
/// </summary>
[TestClass]
public class JsonEncodeFactoryTests
{
	#region Anamorphic source
	[TestMethod]
	public void GetOutputSize_NoCropping()
	{
		// Arrange
		var profile = new VCProfile
		{
			SizingMode = VCSizingMode.Automatic,
			Width = 0,
			Height = 0,
			CroppingType = VCCroppingType.None,
			UseAnamorphic = true
		};

		var sourceTitle = this.CreateAnamorphicTitle();

		// Act
		OutputSizeInfo outputSize = JsonEncodeFactory.GetOutputSize(profile, sourceTitle);

		// Assert
		AssertResolution(outputSize, 720, 480);
		outputSize.Par.Num.ShouldBe(8);
		outputSize.Par.Den.ShouldBe(9);

		AssertZeroPadding(outputSize);
	}

	[TestMethod]
	public void GetOutputSize_ManualSizing()
	{
		// Arrange
		var profile = new VCProfile
		{
			SizingMode = VCSizingMode.Manual,
			Width = 1000,
			Height = 2000,
			PixelAspectX = 12,
			PixelAspectY = 7,
			CroppingType = VCCroppingType.Automatic,
		};

		var sourceTitle = this.CreateAnamorphicTitle();

		// Act
		OutputSizeInfo outputSize = JsonEncodeFactory.GetOutputSize(profile, sourceTitle);

		// Assert
		AssertResolution(outputSize, 1000, 2000);
		outputSize.Par.Num.ShouldBe(12);
		outputSize.Par.Den.ShouldBe(7);

		AssertZeroPadding(outputSize);
	}

	[TestMethod]
	public void GetOutputSize_DownscaleOnly()
	{
		// Arrange
		var profile = new VCProfile
		{
			SizingMode = VCSizingMode.Automatic,
			ScalingMode = VCScalingMode.DownscaleOnly,
			Width = 1280,
			Height = 1024,
			UseAnamorphic = false,
			CroppingType = VCCroppingType.Automatic,
		};

		var sourceTitle = this.CreateAnamorphicTitle();

		// Act
		OutputSizeInfo outputSize = JsonEncodeFactory.GetOutputSize(profile, sourceTitle);

		// Assert
		AssertResolution(outputSize, 718, 310);
		AssertNormalPar(outputSize);
		AssertZeroPadding(outputSize);
	}

	[TestMethod]
	public void GetOutputSize_UpscaleToTargetWidth()
	{
		// Arrange
		var profile = new VCProfile
		{
			SizingMode = VCSizingMode.Automatic,
			ScalingMode = VCScalingMode.UpscaleFill,
			Width = 1280,
			Height = 1024,
			UseAnamorphic = false,
			CroppingType = VCCroppingType.Automatic,
		};

		var sourceTitle = this.CreateAnamorphicTitle();

		// Act
		OutputSizeInfo outputSize = JsonEncodeFactory.GetOutputSize(profile, sourceTitle);

		// Assert
		AssertResolution(outputSize, 1280, 552);
		AssertNormalPar(outputSize);
		AssertZeroPadding(outputSize);
	}

	[TestMethod]
	public void GetOutputSize_UpscaleToTargetHeight()
	{
		// Arrange
		var profile = new VCProfile
		{
			SizingMode = VCSizingMode.Automatic,
			ScalingMode = VCScalingMode.UpscaleFill,
			Width = 2000,
			Height = 700,
			UseAnamorphic = false,
			CroppingType = VCCroppingType.Automatic,
		};

		var sourceTitle = this.CreateAnamorphicTitle();

		// Act
		OutputSizeInfo outputSize = JsonEncodeFactory.GetOutputSize(profile, sourceTitle);

		// Assert
		AssertResolution(outputSize, 1620, 700);
		AssertNormalPar(outputSize);
		AssertZeroPadding(outputSize);
	}

	[TestMethod]
	public void GetOutputSize_Upscale2xMax()
	{
		// Arrange
		var profile = new VCProfile
		{
			SizingMode = VCSizingMode.Automatic,
			ScalingMode = VCScalingMode.Upscale2X,
			Width = 4000,
			Height = 4000,
			UseAnamorphic = false,
			CroppingType = VCCroppingType.Automatic,
		};

		var sourceTitle = this.CreateAnamorphicTitle();

		// Act
		OutputSizeInfo outputSize = JsonEncodeFactory.GetOutputSize(profile, sourceTitle);

		// Assert
		AssertResolution(outputSize, 1436, 620);
		AssertNormalPar(outputSize);
		AssertZeroPadding(outputSize);
	}

	[TestMethod]
	public void GetOutputSize_AnamorphicToAnamorphicWithCropping()
	{
		// Arrange
		var profile = new VCProfile
		{
			SizingMode = VCSizingMode.Automatic,
			Width = 0,
			Height = 0,
			CroppingType = VCCroppingType.Automatic,
			UseAnamorphic = true
		};

		var sourceTitle = this.CreateAnamorphicTitle();

		// Act
		OutputSizeInfo outputSize = JsonEncodeFactory.GetOutputSize(profile, sourceTitle);

		// Assert
		AssertResolution(outputSize, 718, 276);
		outputSize.Par.Num.ShouldBe(8);
		outputSize.Par.Den.ShouldBe(9);

		AssertZeroPadding(outputSize);
	}

	[TestMethod]
	public void GetOutputSize_AnamorphicToNonAnamorphicWithCropping()
	{
		// Arrange
		var profile = new VCProfile
		{
			SizingMode = VCSizingMode.Automatic,
			Width = 0,
			Height = 0,
			CroppingType = VCCroppingType.Automatic,
			UseAnamorphic = false
		};

		var sourceTitle = this.CreateAnamorphicTitle();

		// Act
		OutputSizeInfo outputSize = JsonEncodeFactory.GetOutputSize(profile, sourceTitle);

		// Assert
		AssertResolution(outputSize, 718, 310);
		AssertNormalPar(outputSize);
		AssertZeroPadding(outputSize);
	}

	[TestMethod]
	public void GetOutputSize_AnamorphicToAnamorphicRotate90()
	{
		// Arrange
		var profile = new VCProfile
		{
			SizingMode = VCSizingMode.Automatic,
			Width = 0,
			Height = 0,
			CroppingType = VCCroppingType.Automatic,
			UseAnamorphic = true,
			Rotation = VCPictureRotation.Clockwise90
		};

		var sourceTitle = this.CreateAnamorphicTitle();

		// Act
		OutputSizeInfo outputSize = JsonEncodeFactory.GetOutputSize(profile, sourceTitle);

		// Assert
		outputSize.ScaleWidth.ShouldBe(276);
		outputSize.ScaleHeight.ShouldBe(718);
		outputSize.OutputWidth.ShouldBe(276);
		outputSize.OutputHeight.ShouldBe(718);
		outputSize.Par.Num.ShouldBe(9);
		outputSize.Par.Den.ShouldBe(8);

		outputSize.PictureWidth.ShouldBe(276);
		outputSize.PictureHeight.ShouldBe(718);

		AssertZeroPadding(outputSize);
	}

	[TestMethod]
	public void GetOutputSize_AnamorphicToAnamorphicRotate180()
	{
		// Arrange
		var profile = new VCProfile
		{
			SizingMode = VCSizingMode.Automatic,
			Width = 0,
			Height = 0,
			CroppingType = VCCroppingType.Automatic,
			UseAnamorphic = true,
			Rotation = VCPictureRotation.Clockwise180
		};

		var sourceTitle = this.CreateAnamorphicTitle();

		// Act
		OutputSizeInfo outputSize = JsonEncodeFactory.GetOutputSize(profile, sourceTitle);

		// Assert
		AssertResolution(outputSize, 718, 276);
		outputSize.Par.Num.ShouldBe(8);
		outputSize.Par.Den.ShouldBe(9);
		AssertZeroPadding(outputSize);
	}

	[TestMethod]
	public void GetOutputSize_AnamorphicToNonAnamorphicRotate270()
	{
		// Arrange
		var profile = new VCProfile
		{
			SizingMode = VCSizingMode.Automatic,
			Width = 0,
			Height = 0,
			CroppingType = VCCroppingType.Automatic,
			UseAnamorphic = false,
			Rotation = VCPictureRotation.Clockwise270
		};

		var sourceTitle = this.CreateAnamorphicTitle();

		// Act
		OutputSizeInfo outputSize = JsonEncodeFactory.GetOutputSize(profile, sourceTitle);

		// Assert
		outputSize.ScaleWidth.ShouldBe(310);
		outputSize.ScaleHeight.ShouldBe(718);
		outputSize.OutputWidth.ShouldBe(310);
		outputSize.OutputHeight.ShouldBe(718);
		AssertNormalPar(outputSize);

		outputSize.PictureWidth.ShouldBe(310);
		outputSize.PictureHeight.ShouldBe(718);

		AssertZeroPadding(outputSize);
	}

	[TestMethod]
	public void GetOutputSize_AnamorphicToAnamorphicPad()
	{
		// Arrange
		var profile = new VCProfile
		{
			SizingMode = VCSizingMode.Automatic,
			Width = 1280,
			Height = 1024,
			CroppingType = VCCroppingType.Automatic,
			UseAnamorphic = true,
			PaddingMode = VCPaddingMode.Fill
		};

		var sourceTitle = this.CreateAnamorphicTitle();

		// Act
		OutputSizeInfo outputSize = JsonEncodeFactory.GetOutputSize(profile, sourceTitle);

		// Assert
		outputSize.ScaleWidth.ShouldBe(718);
		outputSize.ScaleHeight.ShouldBe(276);
		outputSize.OutputWidth.ShouldBe(1280);
		outputSize.OutputHeight.ShouldBe(1024);
		outputSize.Par.Num.ShouldBe(8);
		outputSize.Par.Den.ShouldBe(9);

		outputSize.PictureWidth.ShouldBe(718);
		outputSize.PictureHeight.ShouldBe(276);

		outputSize.Padding.Left.ShouldBe(281);
		outputSize.Padding.Right.ShouldBe(281);
		outputSize.Padding.Top.ShouldBe(374);
		outputSize.Padding.Bottom.ShouldBe(374);
	}

	[TestMethod]
	public void GetOutputSize_AnamorphicToNonAnamorphicPad()
	{
		// Arrange
		var profile = new VCProfile
		{
			SizingMode = VCSizingMode.Automatic,
			Width = 1280,
			Height = 1024,
			CroppingType = VCCroppingType.Automatic,
			UseAnamorphic = false,
			PaddingMode = VCPaddingMode.Fill
		};

		var sourceTitle = this.CreateAnamorphicTitle();

		// Act
		OutputSizeInfo outputSize = JsonEncodeFactory.GetOutputSize(profile, sourceTitle);

		// Assert
		outputSize.ScaleWidth.ShouldBe(718);
		outputSize.ScaleHeight.ShouldBe(310);
		outputSize.OutputWidth.ShouldBe(1280);
		outputSize.OutputHeight.ShouldBe(1024);
		AssertNormalPar(outputSize);

		outputSize.PictureWidth.ShouldBe(718);
		outputSize.PictureHeight.ShouldBe(310);

		outputSize.Padding.Left.ShouldBe(281);
		outputSize.Padding.Right.ShouldBe(281);
		outputSize.Padding.Top.ShouldBe(357);
		outputSize.Padding.Bottom.ShouldBe(357);
	}

	[TestMethod]
	public void GetOutputSize_AnamorphicToAnamorphicRotatePad()
	{
		// Arrange
		var profile = new VCProfile
		{
			SizingMode = VCSizingMode.Automatic,
			Width = 500,
			Height = 500,
			CroppingType = VCCroppingType.Automatic,
			UseAnamorphic = true,
			Rotation = VCPictureRotation.Clockwise90,
			PaddingMode = VCPaddingMode.Fill
		};

		var sourceTitle = this.CreateAnamorphicTitle();

		// Act
		OutputSizeInfo outputSize = JsonEncodeFactory.GetOutputSize(profile, sourceTitle);

		// Assert
		outputSize.ScaleWidth.ShouldBe(192);
		outputSize.ScaleHeight.ShouldBe(500);
		outputSize.OutputWidth.ShouldBe(500);
		outputSize.OutputHeight.ShouldBe(500);
		outputSize.Par.Num.ShouldBe(25875);
		outputSize.Par.Den.ShouldBe(22976);

		outputSize.PictureWidth.ShouldBe(192);
		outputSize.PictureHeight.ShouldBe(500);

		outputSize.Padding.Left.ShouldBe(154);
		outputSize.Padding.Right.ShouldBe(154);
		outputSize.Padding.Top.ShouldBe(0);
		outputSize.Padding.Bottom.ShouldBe(0);
	}

	[TestMethod]
	public void GetOutputSize_AnamorphicToNonAnamorphicRotatePad()
	{
		// Arrange
		var profile = new VCProfile
		{
			SizingMode = VCSizingMode.Automatic,
			Width = 500,
			Height = 500,
			CroppingType = VCCroppingType.Automatic,
			UseAnamorphic = false,
			Rotation = VCPictureRotation.Clockwise90,
			PaddingMode = VCPaddingMode.Fill
		};

		var sourceTitle = this.CreateAnamorphicTitle();

		// Act
		OutputSizeInfo outputSize = JsonEncodeFactory.GetOutputSize(profile, sourceTitle);

		// Assert
		outputSize.ScaleWidth.ShouldBe(216);
		outputSize.ScaleHeight.ShouldBe(500);
		outputSize.OutputWidth.ShouldBe(500);
		outputSize.OutputHeight.ShouldBe(500);
		AssertNormalPar(outputSize);

		outputSize.PictureWidth.ShouldBe(216);
		outputSize.PictureHeight.ShouldBe(500);

		outputSize.Padding.Left.ShouldBe(142);
		outputSize.Padding.Right.ShouldBe(142);
		outputSize.Padding.Top.ShouldBe(0);
		outputSize.Padding.Bottom.ShouldBe(0);
	}
	#endregion

	#region Non-anamorphic source

	[TestMethod]
	public void GetOutputSize_NonAnamorphicToAnamorphicWithCropping()
	{
		// Arrange
		var profile = new VCProfile
		{
			SizingMode = VCSizingMode.Automatic,
			Width = 0,
			Height = 0,
			CroppingType = VCCroppingType.Automatic,
			UseAnamorphic = true
		};

		var sourceTitle = this.CreateNonAnamorphicTitle();

		// Act
		OutputSizeInfo outputSize = JsonEncodeFactory.GetOutputSize(profile, sourceTitle);

		// Assert
		AssertResolution(outputSize, 1920, 796);
		AssertNormalPar(outputSize);
		AssertZeroPadding(outputSize);
	}


	[TestMethod]
	public void GetOutputSize_NonAnamorphicToNonAnamorphicWithCropping()
	{
		// Arrange
		var profile = new VCProfile
		{
			SizingMode = VCSizingMode.Automatic,
			Width = 0,
			Height = 0,
			CroppingType = VCCroppingType.Automatic,
			UseAnamorphic = false
		};

		var sourceTitle = this.CreateNonAnamorphicTitle();

		// Act
		OutputSizeInfo outputSize = JsonEncodeFactory.GetOutputSize(profile, sourceTitle);

		// Assert
		AssertResolution(outputSize, 1920, 796);
		AssertNormalPar(outputSize);
		AssertZeroPadding(outputSize);
	}

	[TestMethod]
	public void GetOutputSize_NonAnamorphicToNonAnamorphicWithLooseCropping()
	{
		// Arrange
		var profile = new VCProfile
		{
			SizingMode = VCSizingMode.Automatic,
			Width = 0,
			Height = 0,
			CroppingType = VCCroppingType.Loose,
			UseAnamorphic = true
		};

		var sourceTitle = this.CreateNonAnamorphicTitle();

		// Act
		OutputSizeInfo outputSize = JsonEncodeFactory.GetOutputSize(profile, sourceTitle);

		// Assert
		AssertResolution(outputSize, 1920, 800);
		AssertNormalPar(outputSize);
		AssertZeroPadding(outputSize);
	}

	[TestMethod]
	public void GetOutputSize_NonAnamorphicToNonAnamorphicWithTooLargeCropping()
	{
		// Arrange
		var profile = new VCProfile
		{
			SizingMode = VCSizingMode.Automatic,
			Width = 0,
			Height = 0,
			CroppingType = VCCroppingType.Custom,
			Cropping = new VCCropping(5000, 5000, 5000, 5000),
			UseAnamorphic = false
		};

		var sourceTitle = this.CreateNonAnamorphicTitle();

		// Act
		OutputSizeInfo outputSize = JsonEncodeFactory.GetOutputSize(profile, sourceTitle);

		// Assert
		AssertResolution(outputSize, 2, 2);
		AssertNormalPar(outputSize);
		AssertZeroPadding(outputSize);
	}

	[TestMethod]
	public void GetOutputSize_NonAnamorphicToNonAnamorphicWithCroppingAndRotateAndPad()
	{
		// Arrange
		var profile = new VCProfile
		{
			SizingMode = VCSizingMode.Automatic,
			Width = 1920,
			Height = 1080,
			CroppingType = VCCroppingType.Automatic,
			UseAnamorphic = false,
			Rotation = VCPictureRotation.Clockwise90,
			PaddingMode = VCPaddingMode.Fill
		};

		var sourceTitle = this.CreateNonAnamorphicTitle();

		// Act
		OutputSizeInfo outputSize = JsonEncodeFactory.GetOutputSize(profile, sourceTitle);

		// Assert
		outputSize.ScaleWidth.ShouldBe(448);
		outputSize.ScaleHeight.ShouldBe(1080);
		outputSize.OutputWidth.ShouldBe(1920);
		outputSize.OutputHeight.ShouldBe(1080);
		AssertNormalPar(outputSize);

		outputSize.PictureWidth.ShouldBe(448);
		outputSize.PictureHeight.ShouldBe(1080);

		outputSize.Padding.Left.ShouldBe(736);
		outputSize.Padding.Right.ShouldBe(736);
		outputSize.Padding.Top.ShouldBe(0);
		outputSize.Padding.Bottom.ShouldBe(0);
	}

	#endregion

	/// <summary>
	/// Gets a DVD anamorphic source with cropping needed.
	/// </summary>
	/// <returns>The source title.</returns>
	/// <remarks>This is "The Abyss" DVD</remarks>
	private SourceTitle CreateAnamorphicTitle()
	{
		return new SourceTitle
		{
			Geometry = new Geometry
			{
				Width = 720,
				Height = 480,
				PAR = new PAR
				{
					Num = 8,
					Den = 9
				}
			},
			Crop = new List<int>
			{
				100,
				104,
				0,
				2
			},
			LooseCrop = new List<int>
			{
				100,
				104,
				0,
				0
			}
		};
	}

	/// <summary>
	/// Gets a non-anamorphic source with cropping needed.
	/// </summary>
	/// <returns>The source title.</returns>
	private SourceTitle CreateNonAnamorphicTitle()
	{
		return new SourceTitle
		{
			Geometry = new Geometry
			{
				Width = 1920,
				Height = 1080,
				PAR = new PAR
				{
					Num = 1,
					Den = 1
				}
			},
			Crop = new List<int>
			{
				142,
				142,
				0,
				0
			},
			LooseCrop = new List<int>
			{
				140,
				140,
				0,
				0
			}
		};
	}

	private static void AssertNormalPar(OutputSizeInfo outputSize)
	{
		outputSize.Par.Num.ShouldBe(1);
		outputSize.Par.Den.ShouldBe(1);
	}

	private static void AssertResolution(OutputSizeInfo outputSize, int width, int height)
	{
		outputSize.ScaleWidth.ShouldBe(width);
		outputSize.ScaleHeight.ShouldBe(height);
		outputSize.OutputWidth.ShouldBe(width);
		outputSize.OutputHeight.ShouldBe(height);
		outputSize.PictureWidth.ShouldBe(width);
		outputSize.PictureHeight.ShouldBe(height);
	}

	private static void AssertZeroPadding(OutputSizeInfo outputSize)
	{
		VCPadding padding = outputSize.Padding;

		padding.Left.ShouldBe(0);
		padding.Right.ShouldBe(0);
		padding.Top.ShouldBe(0);
		padding.Bottom.ShouldBe(0);
	}
}
