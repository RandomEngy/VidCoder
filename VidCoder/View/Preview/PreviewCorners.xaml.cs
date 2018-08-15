using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using ReactiveUI;
using VidCoder.Model;
using VidCoder.ViewModel;

namespace VidCoder.View.Preview
{
	/// <summary>
	/// Interaction logic for PreviewCorners.xaml
	/// </summary>
	public partial class PreviewCorners : UserControl
	{
		private const double ZoomedPixelSize = 4;

		private PreviewWindowViewModel viewModel;

		private IDisposable updateCornerImageSubscription;

		public PreviewCorners()
		{
			this.InitializeComponent();

			this.DataContextChanged += this.OnDataContextChanged;
		}

		private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
		{
			var newViewModel = e.NewValue as PreviewWindowViewModel;

			this.viewModel = newViewModel;
			if (newViewModel == null)
			{
				//this.viewModel.PropertyChanged -= this.OnPropertyChanged;
				this.updateCornerImageSubscription?.Dispose();
			}
			else
			{
				//newViewModel.PropertyChanged += this.OnPropertyChanged;
				this.updateCornerImageSubscription = newViewModel.PreviewImageServiceClient.WhenAnyValue(x => x.PreviewImage).Subscribe(_ =>
				{
					this.UpdateCornerImages();
				});
			}
		}

		//private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
		//{
		//	if (e.PropertyName == nameof(this.viewModel.PreviewImage))
		//	{
		//		this.UpdateCornerImages();
		//	}
		//}

		public void UpdateCornerImages()
		{
			if (this.viewModel.PreviewImageServiceClient.PreviewImage != null)
			{
				// Top left
				UpdateCornerImage(
					this.topLeftImage,
					this.topLeftImageHolder,
					this.viewModel.PreviewImageServiceClient.PreviewImage,
					(imageWidth, imageHeight, regionWidth, regionHeight) => new Int32Rect(0, 0, regionWidth, regionHeight));

				// Bottom right
				UpdateCornerImage(
					this.bottomRightImage,
					this.bottomRightImageHolder,
					this.viewModel.PreviewImageServiceClient.PreviewImage,
					(imageWidth, imageHeight, regionWidth, regionHeight) => new Int32Rect(imageWidth - regionWidth, imageHeight - regionHeight, regionWidth, regionHeight));
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
	}
}
