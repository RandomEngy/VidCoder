using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using VidCoder.Services;

namespace VidCoder
{
	public static class ImageUtilities
	{
		// DPI factors for the OS. Used to scale back down the images so they display at native resolution.
		// Normal DPI: 1.0
		// 120 DPI: 1.25
		// 144 DPI: 1.5
		private static double dpiXFactor, dpiYFactor;

		static ImageUtilities()
		{
			// Get system DPI
			Matrix m = PresentationSource.FromVisual(Application.Current.MainWindow).CompositionTarget.TransformToDevice;
			if (m.M11 > 0 && m.M22 > 0)
			{
				dpiXFactor = m.M11;
				dpiYFactor = m.M22;
			}
			else
			{
				// Sometimes this can return a matrix with 0s. Fall back to assuming normal DPI in this case.
				Ioc.Get<ILogger>().Log("Could not read DPI. Assuming default DPI.");
				dpiXFactor = 1;
				dpiYFactor = 1;
			}
		}

		public static double DpiXFactor
		{
			get
			{
				return dpiXFactor;
			}
		}

		public static double DpiYFactor
		{
			get
			{
				return dpiYFactor;
			}
		}

		public static void UpdatePreviewImageSize(Image image, Grid imageHolder, double widthPixels, double heightPixels)
		{
			// Convert pixels to WPF length units: we want to keep native image resolution.
			double width = widthPixels / dpiXFactor;
			double height = heightPixels / dpiYFactor;

			double availableWidth = imageHolder.ActualWidth;
			double availableHeight = imageHolder.ActualHeight;

			if (width < availableWidth && height < availableHeight)
			{
				image.Width = width;
				image.Height = height;

				double left = (availableWidth - width) / 2;
				double top = (availableHeight - height) / 2;

				// Round to nearest pixel
				left = Math.Floor(left * dpiXFactor) / dpiXFactor;
				top = Math.Floor(top * dpiYFactor) / dpiYFactor;

				image.Margin = new Thickness(left, top, 0, 0);
				return;
			}

			double imageAR = width / height;
			double availableAR = availableWidth / availableHeight;

			if (imageAR > availableAR)
			{
				// If the image is shorter than the space available and is touching the sides
				double scalingFactor = availableWidth / width;
				double resizedHeight = height * scalingFactor;
				image.Width = availableWidth;
				image.Height = resizedHeight;

				double top = (availableHeight - resizedHeight) / 2;

				// Round to nearest pixel
				top = Math.Floor(top * dpiYFactor) / dpiYFactor;

				image.Margin = new Thickness(0, top, 0, 0);
			}
			else
			{
				// If the image is taller than the space available and is touching the top and bottom
				double scalingFactor = availableHeight / height;
				double resizedWidth = width * scalingFactor;
				image.Width = resizedWidth;
				image.Height = availableHeight;

				double left = (availableWidth - resizedWidth) / 2;

				// Round to nearest pixel
				left = Math.Floor(left * dpiXFactor) / dpiXFactor;

				image.Margin = new Thickness(left, 0, 0, 0);
			}
		}
	}
}
