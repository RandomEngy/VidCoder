using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using VidCoder.Extensions;
using VidCoder.Services;
using VidCoder.Services.Windows;
using VidCoder.ViewModel;

namespace VidCoder.View
{
	using GalaSoft.MvvmLight.Messaging;
	using Messages;
	using Model;

	public delegate Int32Rect RegionChooser(int imageWidth, int imageHeight, int regionWidth, int regionHeight);

	/// <summary>
	/// Interaction logic for PreviewWindow.xaml
	/// </summary>
	public partial class PreviewWindow : Window
	{
		private PreviewWindowViewModel viewModel;
		private const double ZoomedPixelSize = 4;
		private const double MarginWithScrollBar = 22;

		public PreviewWindow()
		{
			InitializeComponent();

			this.previewControls.Opacity = 0.0;
			this.DataContextChanged += PreviewWindow_DataContextChanged;

			Messenger.Default.Register<PreviewImageChangedMessage>(
				this,
				message =>
					{
						this.RefreshImageSize();
					});
		}

		private void PreviewWindow_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
		{
			this.viewModel = this.DataContext as PreviewWindowViewModel;
			this.viewModel.PropertyChanged += viewModel_PropertyChanged;
			this.RefreshControlMargins();
			if (this.viewModel.InCornerDisplayMode)
			{
				this.previewControls.Opacity = 1.0;
			}
		}

		private void viewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			if (e.PropertyName == "DisplayType")
			{
				this.RefreshControlMargins();
			}
		}

		protected override void OnSourceInitialized(EventArgs e)
		{
			base.OnSourceInitialized(e);
			string placement = Config.PreviewWindowPlacement;
			if (string.IsNullOrEmpty(placement))
			{
				// Initialize window size to fit 853x480 at native resolution.
				double targetImageWidth = 480.0 * (16.0 / 9.0);
				double targetImageHeight = 480.0;

				// Add some for the window borders and scale to DPI
				this.Width = (targetImageWidth + 16.0) / ImageUtilities.DpiXFactor;
				this.Height = (targetImageHeight + 34.0) / ImageUtilities.DpiYFactor;

				Rect? placementRect = new WindowPlacer().PlaceWindow(this);
				if (placementRect.HasValue)
				{
					this.Left = placementRect.Value.Left;
					this.Top = placementRect.Value.Top;
				}
			}
			else
			{
				this.SetPlacementJson(placement);
			}
		}

		private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			Config.PreviewWindowPlacement = this.GetPlacementJson();
		}

		private void previewImageHolder_SizeChanged(object sender, SizeChangedEventArgs e)
		{
			this.RefreshImageSize();
		}

		private void previewImageScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
		{
			this.RefreshControlMargins();
		}

		private void previewImageOneToOne_SizeChanged(object sender, SizeChangedEventArgs e)
		{
			this.RefreshControlMargins();
		}

		private void RefreshImageSize()
		{
			var previewVM = this.DataContext as PreviewWindowViewModel;

			if (previewVM.DisplayType == PreviewDisplay.Corners)
			{
				var bitmap = previewVM.PreviewBitmapSource;
				
				if (bitmap != null)
				{
					// Top left
					UpdateCornerImage(
						this.topLeftImage,
						this.topLeftImageHolder,
						bitmap, 
						(imageWidth, imageHeight, regionWidth, regionHeight) => new Int32Rect(0, 0, regionWidth, regionHeight));

					// Bottom right
					UpdateCornerImage(
						this.bottomRightImage,
						this.bottomRightImageHolder,
						bitmap,
						(imageWidth, imageHeight, regionWidth, regionHeight) => new Int32Rect(imageWidth - regionWidth, imageHeight - regionHeight, regionWidth, regionHeight));
				}
			}
			else
			{
				double widthPixels = previewVM.PreviewDisplayWidth;
				double heightPixels = previewVM.PreviewDisplayHeight;

				ImageUtilities.UpdatePreviewImageSize(this.previewImage, this.previewImageHolder, widthPixels, heightPixels);
			}
		}

		private static void UpdateCornerImage(Image image, Grid imageHolder, BitmapSource bitmap, RegionChooser regionChooser, bool isRetry = false)
		{
			// Image dimensions
			int imageWidth = bitmap.PixelWidth;
			int imageHeight = bitmap.PixelHeight;

			double availableWidth = imageHolder.ActualWidth;
			double availableHeight = imageHolder.ActualHeight;

			if (availableWidth == 0 || availableHeight == 0)
			{
				// Intermittent bug causing this. In this case we queue it up to go again after a layout pass has been done.
				if (!isRetry)
				{
					DispatchUtilities.BeginInvoke(() =>
						{
							UpdateCornerImage(image, imageHolder, bitmap, regionChooser, isRetry: true);
						});
				}

				return;
			}

			int cornerWidthPixels = (int)(availableWidth / ZoomedPixelSize);
			int cornerHeightPixels = (int)(availableHeight / ZoomedPixelSize);

			// Make sure the subsection of the image is not larger than the image itself
			cornerWidthPixels = Math.Min(cornerWidthPixels, imageWidth);
			cornerHeightPixels = Math.Min(cornerHeightPixels, imageHeight);

			double cornerWidth = cornerWidthPixels * ZoomedPixelSize;
			double cornerHeight = cornerHeightPixels * ZoomedPixelSize;

			var croppedBitmap = new CroppedBitmap();
			croppedBitmap.BeginInit();
			croppedBitmap.SourceRect = regionChooser(imageWidth, imageHeight, cornerWidthPixels, cornerHeightPixels);
			croppedBitmap.Source = bitmap;
			croppedBitmap.EndInit();

			image.Source = croppedBitmap;
			image.Width = cornerWidth;
			image.Height = cornerHeight;
		}

		private void RefreshControlMargins()
		{
			if (this.viewModel == null)
			{
				return;
			}

			var margin = new Thickness(6, 0, 6, 6);

			if (!this.viewModel.SingleOneToOneImageVisible)
			{
				this.previewControls.Margin = margin;
				return;
			}

			if (this.previewImageScrollViewer.ComputedHorizontalScrollBarVisibility == Visibility.Visible)
			{
				margin.Right = MarginWithScrollBar;
			}

			if (this.previewImageScrollViewer.ComputedVerticalScrollBarVisibility == Visibility.Visible)
			{
				margin.Bottom = MarginWithScrollBar;
			}

			this.previewControls.Margin = margin;
		}

		private void Window_PreviewDrop(object sender, DragEventArgs e)
		{
			Ioc.Get<Main>().HandleDrop(sender, e);
		}

		private void Window_PreviewDragOver(object sender, DragEventArgs e)
		{
			Utilities.SetDragIcon(e);
		}
	}
}
