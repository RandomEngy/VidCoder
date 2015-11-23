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
using VidCoder.Model;
using VidCoder.Services;
using VidCoder.Services.Windows;
using VidCoder.View.Preview;
using VidCoder.ViewModel;

namespace VidCoder.View
{
	public delegate Int32Rect RegionChooser(int imageWidth, int imageHeight, int regionWidth, int regionHeight);

	/// <summary>
	/// Interaction logic for PreviewWindow.xaml
	/// </summary>
	public partial class PreviewWindow : Window, IPreviewView
	{
		private PreviewWindowViewModel viewModel;

		public PreviewWindow()
		{
			this.InitializeComponent();

			this.DataContextChanged += this.OnDataContextChanged;
		}

		private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
		{
			this.viewModel = (PreviewWindowViewModel)this.DataContext;
			this.viewModel.View = this;
			this.viewModel.MainDisplayObservable.Subscribe(this.OnMainDisplayUpdate);
		}

		private void OnMainDisplayUpdate(PreviewMainDisplay mainDisplay)
		{
			switch (mainDisplay)
			{
				case PreviewMainDisplay.None:
					this.previewArea.Children.Clear();
					break;
				case PreviewMainDisplay.StillDefault:
					if (!(this.MainContent is PreviewStillFit))
					{
						this.MainContent = new PreviewStillFit();
					}

					break;
				case PreviewMainDisplay.StillFitToWindow:
					if (!(this.MainContent is PreviewStillFit))
					{
						this.MainContent = new PreviewStillFit();
					}

					break;
				case PreviewMainDisplay.StillOneToOne:
					if (!(this.MainContent is PreviewStillOneToOne))
					{
						this.MainContent = new PreviewStillOneToOne();
					}

					break;
				case PreviewMainDisplay.StillCorners:
					if (!(this.MainContent is PreviewCorners))
					{
						this.MainContent = new PreviewCorners();
					}

					break;
				case PreviewMainDisplay.Video:
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}

			this.RefreshImageSize();
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

		private UIElement MainContent
		{
			get
			{
				if (this.previewArea.Children.Count == 0)
				{
					return null;
				}

				return this.previewArea.Children[0];
			}

			set
			{
				this.previewArea.Children.Clear();
				this.previewArea.Children.Add(value);
			}
		}

		private void PreviewArea_OnSizeChanged(object sender, SizeChangedEventArgs e)
		{
			this.RefreshImageSize();
		}

		public void RefreshImageSize()
		{
			var previewVM = (PreviewWindowViewModel) this.DataContext;
			PreviewStillFit stillFitControl;

			switch (previewVM.MainDisplay)
			{
				case PreviewMainDisplay.StillDefault:
					stillFitControl = this.MainContent as PreviewStillFit;
					stillFitControl?.ResizeHolder(
						this.previewArea,
						previewVM.PreviewDisplayWidth,
						previewVM.PreviewDisplayHeight,
						showOneToOneWhenSmaller: true);

					break;
				case PreviewMainDisplay.StillFitToWindow:
					stillFitControl = this.MainContent as PreviewStillFit;
					stillFitControl?.ResizeHolder(
						this.previewArea,
						previewVM.PreviewDisplayWidth,
						previewVM.PreviewDisplayHeight,
						showOneToOneWhenSmaller: false);

					break;
				case PreviewMainDisplay.StillOneToOne:
					var oneToOneControl = this.MainContent as PreviewStillOneToOne;
					oneToOneControl?.ResizeHolder(previewVM.PreviewDisplayWidth, previewVM.PreviewDisplayHeight);

					break;
				case PreviewMainDisplay.StillCorners:
					var cornersControl = this.MainContent as PreviewCorners;
					cornersControl?.UpdateCornerImages();

					break;
				//case PreviewMainDisplay.Video:
				//	break;
			}
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
