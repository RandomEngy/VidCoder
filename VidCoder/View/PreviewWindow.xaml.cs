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
            double width = previewVM.PreviewWidth;
            double height = previewVM.PreviewHeight;

            double availableWidth = this.previewImageHolder.ActualWidth;
            double availableHeight = this.previewImageHolder.ActualHeight;

            if (width < availableWidth && height < availableHeight)
            {
                this.previewImage.Width = width;
                this.previewImage.Height = height;
                return;
            }

            double imageAR = width / height;
            double availableAR = availableWidth / availableHeight;

            if (imageAR > availableAR)
            {
                // If the image is shorter than the space available and is touching the sides
                double scalingFactor = availableWidth / width;
                this.previewImage.Width = availableWidth;
                this.previewImage.Height = height * scalingFactor;
            }
            else
            {
                // If the image is taller than the space available and is touching the top and bottom
                double scalingFactor = availableHeight / height;
                this.previewImage.Width = width * scalingFactor;
                this.previewImage.Height = availableHeight;
            }
        }

        private void previewImageHolder_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            this.RefreshImageSize();
        }
    }
}
