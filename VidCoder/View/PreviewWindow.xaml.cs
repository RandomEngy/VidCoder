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
using VidCoder.Services;
using VidCoder.ViewModel;
using Microsoft.Practices.Unity;

namespace VidCoder.View
{
	/// <summary>
	/// Interaction logic for PreviewWindow.xaml
	/// </summary>
	public partial class PreviewWindow : Window
	{
		private PreviewViewModel viewModel;

		public PreviewWindow()
		{
			InitializeComponent();

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
			if (e.PropertyName == "PreviewImage")
			{
				this.RefreshImageSize();
			}
		}

		protected override void OnSourceInitialized(EventArgs e)
		{
			base.OnSourceInitialized(e);
			string placement = Settings.Default.PreviewWindowPlacement;
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
				this.Width = (targetImageWidth + 16.0) / ImageUtilities.DpiXFactor;
				this.Height = (targetImageHeight + 34.0) / ImageUtilities.DpiYFactor;
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
			var previewVM = this.DataContext as PreviewViewModel;
			double widthPixels = previewVM.PreviewWidth;
			double heightPixels = previewVM.PreviewHeight;

			ImageUtilities.UpdatePreviewImageSize(this.previewImage, this.previewImageHolder, widthPixels, heightPixels);
		}

		private void previewImageHolder_SizeChanged(object sender, SizeChangedEventArgs e)
		{
			this.RefreshImageSize();
		}

		private void Window_PreviewDrop(object sender, DragEventArgs e)
		{
			Unity.Container.Resolve<MainWindow>().HandleDrop(sender, e);
		}

		private void Window_PreviewDragOver(object sender, DragEventArgs e)
		{
			Utilities.SetDragIcon(e);
		}
	}
}
