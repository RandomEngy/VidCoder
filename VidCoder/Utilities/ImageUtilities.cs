using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.AnyContainer;
using VidCoder.Services;

namespace VidCoder
{
	public static class ImageUtilities
	{
		static ImageUtilities()
		{
			// Get system DPI
			Matrix m = PresentationSource.FromVisual(Application.Current.MainWindow).CompositionTarget.TransformToDevice;
			if (m.M11 > 0 && m.M22 > 0)
			{
				DpiXFactor = m.M11;
				DpiYFactor = m.M22;
			}
			else
			{
				// Sometimes this can return a matrix with 0s. Fall back to assuming normal DPI in this case.
				StaticResolver.Resolve<IAppLogger>().Log("Could not read DPI. Assuming default DPI.");
				DpiXFactor = 1;
				DpiYFactor = 1;
			}
		}

		// DPI factors for the OS. Used to scale back down the images so they display at native resolution.
		// Normal DPI: 1.0
		// 120 DPI: 1.25
		// 144 DPI: 1.5
		public static double DpiXFactor { get; }

		public static double DpiYFactor { get; }

		public static void UpdatePreviewHolderSize(FrameworkElement holder, Grid previewArea, double widthPixels, double heightPixels, bool showOneToOneWhenSmaller)
		{
			// Convert pixels to WPF length units: we want to keep native image resolution.
			double width = widthPixels / DpiXFactor;
			double height = heightPixels / DpiYFactor;

			double availableWidth = previewArea.ActualWidth;
			double availableHeight = previewArea.ActualHeight;

			if (width < availableWidth && height < availableHeight && showOneToOneWhenSmaller)
			{
				holder.Width = width;
				holder.Height = height;

				double left = (availableWidth - width) / 2;
				double top = (availableHeight - height) / 2;

				// Round to nearest pixel
				left = Math.Floor(left * DpiXFactor) / DpiXFactor;
				top = Math.Floor(top * DpiYFactor) / DpiYFactor;

				holder.Margin = new Thickness(left, top, 0, 0);
				return;
			}

			double imageAR = width / height;
			double availableAR = availableWidth / availableHeight;

			if (imageAR > availableAR)
			{
				// If the image is shorter than the space available and is touching the sides
				double scalingFactor = availableWidth / width;
				double resizedHeight = height * scalingFactor;
				holder.Width = availableWidth;
				holder.Height = resizedHeight;

				double top = (availableHeight - resizedHeight) / 2;

				// Round to nearest pixel
				top = Math.Floor(top * DpiYFactor) / DpiYFactor;

				holder.Margin = new Thickness(0, top, 0, 0);
			}
			else
			{
				// If the image is taller than the space available and is touching the top and bottom
				double scalingFactor = availableHeight / height;
				double resizedWidth = width * scalingFactor;
				holder.Width = resizedWidth;
				holder.Height = availableHeight;

				double left = (availableWidth - resizedWidth) / 2;

				// Round to nearest pixel
				left = Math.Floor(left * DpiXFactor) / DpiXFactor;

				holder.Margin = new Thickness(left, 0, 0, 0);
			}
		}
	}
}
