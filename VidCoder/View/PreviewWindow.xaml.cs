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
using VidCoder.Properties;
using VidCoder.ViewModel;

namespace VidCoder.View
{
	/// <summary>
	/// Interaction logic for PreviewWindow.xaml
	/// </summary>
	public partial class PreviewWindow : Window
	{
		private PreviewViewModel viewModel;

		// DPI factors for the OS. Used to scale back down the images so they display at native resolution.
		// Normal DPI: 1.0
		// 120 DPI: 1.25
		// 144 DPI: 1.5
		private double dpiXFactor, dpiYFactor;

		public PreviewWindow()
		{
			InitializeComponent();

			// Get system DPI
			Matrix m = PresentationSource.FromVisual(Application.Current.MainWindow).CompositionTarget.TransformToDevice;
			this.dpiXFactor = m.M11;
			this.dpiYFactor = m.M22;

			this.previewControls.Opacity = 0.0;
			this.DataContextChanged += new DependencyPropertyChangedEventHandler(PreviewWindow_DataContextChanged);
		}

		private void PreviewWindow_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
		{
			this.viewModel = this.DataContext as PreviewViewModel;
			this.viewModel.PropertyChanged += new System.ComponentModel.PropertyChangedEventHandler(viewModel_PropertyChanged);
		}

		private void viewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			if (e.PropertyName == "PreviewSource")
			{
				this.RefreshImageSize();
			}
		}

		protected override void OnSourceInitialized(EventArgs e)
		{
			base.OnSourceInitialized(e);
			string placement = Properties.Settings.Default.PreviewWindowPlacement;
			if (string.IsNullOrEmpty(placement))
			{
				Rect workArea = SystemParameters.WorkArea;

				if (workArea.Width > Constants.TotalDefaultWidth && workArea.Height > Constants.TotalDefaultHeight)
				{
					double widthRemaining = workArea.Width - Constants.TotalDefaultWidth;
					double heightRemaining = workArea.Height - Constants.TotalDefaultHeight;

					this.Left = workArea.Left + widthRemaining / 2 + 676;
					this.Top = workArea.Top + heightRemaining / 2;
				}

				// Initialize window size to fit 853x480 at native resolution.
				double targetImageWidth = 480.0 * (16.0 / 9.0);
				double targetImageHeight = 480.0;

				// Add some for the window borders and scale to DPI
				this.Width = (targetImageWidth + 16.0) / this.dpiXFactor;
				this.Height = (targetImageHeight + 34.0) / this.dpiYFactor;
			}
			else
			{
				this.SetPlacement(placement);
			}
		}

		private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			Settings.Default.PreviewWindowPlacement = this.GetPlacement();
			Settings.Default.Save();
		}

		private void RefreshImageSize()
		{
			PreviewViewModel previewVM = this.DataContext as PreviewViewModel;
			double widthPixels = previewVM.PreviewWidth;
			double heightPixels = previewVM.PreviewHeight;

			// Convert pixels to WPF length units: we want to keep native image resolution.
			double width = widthPixels / this.dpiXFactor;
			double height = heightPixels / this.dpiYFactor;

			double availableWidth = this.previewImageHolder.ActualWidth;
			double availableHeight = this.previewImageHolder.ActualHeight;

			if (width < availableWidth && height < availableHeight)
			{
				this.previewImage.Width = width;
				this.previewImage.Height = height;

				double left = (availableWidth - width) / 2;
				double top = (availableHeight - height) / 2;

				// Round to nearest pixel
				left = Math.Floor(left * this.dpiXFactor) / this.dpiXFactor;
				top = Math.Floor(top * this.dpiYFactor) / this.dpiYFactor;

				this.previewImage.Margin = new Thickness(left, top, 0, 0);
				return;
			}

			double imageAR = width / height;
			double availableAR = availableWidth / availableHeight;

			if (imageAR > availableAR)
			{
				// If the image is shorter than the space available and is touching the sides
				double scalingFactor = availableWidth / width;
				double resizedHeight = height * scalingFactor;
				this.previewImage.Width = availableWidth;
				this.previewImage.Height = resizedHeight;

				double top = (availableHeight - resizedHeight) / 2;

				// Round to nearest pixel
				top = Math.Floor(top * this.dpiYFactor) / this.dpiYFactor;

				this.previewImage.Margin = new Thickness(0, top, 0, 0);
			}
			else
			{
				// If the image is taller than the space available and is touching the top and bottom
				double scalingFactor = availableHeight / height;
				double resizedWidth = width * scalingFactor;
				this.previewImage.Width = resizedWidth;
				this.previewImage.Height = availableHeight;

				double left = (availableWidth - resizedWidth) / 2;

				// Round to nearest pixel
				left = Math.Floor(left * this.dpiXFactor) / this.dpiXFactor;

				this.previewImage.Margin = new Thickness(left, 0, 0, 0);
			}
		}

		private void previewImageHolder_SizeChanged(object sender, SizeChangedEventArgs e)
		{
			this.RefreshImageSize();
		}
	}
}
