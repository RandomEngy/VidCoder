using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using ReactiveUI;
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

			Observable.CombineLatest(
				this.viewModel.MainDisplayObservable, 
				this.viewModel.WhenAnyValue(x => x.PlayingPreview),
				(mainDisplay, playingPreview) =>
				{
					return new { mainDisplay, playingPreview };
				})
				.Subscribe(x =>
				{
					this.OnMainDisplayUpdate(x.mainDisplay, x.playingPreview);
				});
		}

		private object OnMainDisplayUpdate(PreviewMainDisplay mainDisplay, bool playingPreview)
		{
			this.RefreshMainContent(mainDisplay, playingPreview);
			this.RefreshImageSize();

			return null;
		}

		private void RefreshMainContent(PreviewMainDisplay mainDisplay, bool playingPreview)
		{
			switch (mainDisplay)
			{
				case PreviewMainDisplay.None:
					this.previewArea.Children.Clear();
					break;
				case PreviewMainDisplay.Default:
					if (!(this.MainContent is PreviewFit))
					{
						this.MainContent = new PreviewFit();
					}

					break;
				case PreviewMainDisplay.FitToWindow:
					if (!(this.MainContent is PreviewFit))
					{
						this.MainContent = new PreviewFit();
					}

					break;
				case PreviewMainDisplay.OneToOne:
					if (!(this.MainContent is PreviewOneToOne))
					{
						this.MainContent = new PreviewOneToOne();
					}

					break;
				case PreviewMainDisplay.StillCorners:
					if (!(this.MainContent is PreviewCorners))
					{
						this.MainContent = new PreviewCorners();
					}

					break;

				default:
					throw new ArgumentOutOfRangeException();
			}

			var previewholder = this.MainContent as IPreviewHolder;
			if (previewholder != null)
			{
				if (playingPreview)
				{

				}
				else
				{
					if (!(previewholder.PreviewContent is Image))
					{
						var image = new Image
						{
							Stretch = Stretch.Fill
						};

						image.SetBinding(Image.SourceProperty, nameof(this.viewModel.PreviewImage));

						previewholder.PreviewContent = image;
					}
				}
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
			PreviewFit fitControl;

			switch (previewVM.MainDisplay)
			{
				case PreviewMainDisplay.Default:
					fitControl = this.MainContent as PreviewFit;
					fitControl?.ResizeHolder(
						this.previewArea,
						previewVM.PreviewDisplayWidth,
						previewVM.PreviewDisplayHeight,
						showOneToOneWhenSmaller: true);

					break;
				case PreviewMainDisplay.FitToWindow:
					fitControl = this.MainContent as PreviewFit;
					fitControl?.ResizeHolder(
						this.previewArea,
						previewVM.PreviewDisplayWidth,
						previewVM.PreviewDisplayHeight,
						showOneToOneWhenSmaller: false);

					break;
				case PreviewMainDisplay.OneToOne:
					var oneToOneControl = this.MainContent as PreviewOneToOne;
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
