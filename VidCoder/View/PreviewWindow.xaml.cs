using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
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
using VidCoderCommon.Model;

namespace VidCoder.View
{
	public delegate Int32Rect RegionChooser(int imageWidth, int imageHeight, int regionWidth, int regionHeight);

	/// <summary>
	/// Interaction logic for PreviewWindow.xaml
	/// </summary>
	public partial class PreviewWindow : Window, IPreviewView
	{
		private PreviewWindowViewModel viewModel;

		private IDisposable controlsUpdateSubscription;
		private IDisposable mainDisplayUpdateSubscription;
		private IDisposable previewPausedSubscription;

		public PreviewWindow()
		{
			this.InitializeComponent();

			this.DataContextChanged += this.OnDataContextChanged;
			this.Loaded += (sender, args) =>
			{
				this.rootGrid.Focus();
			};
		}

		public void RefreshViewModelFromMediaElement()
		{
			var previewholder = this.MainContent as IPreviewFrame;
			if (previewholder != null)
			{
				TimeSpan position = previewholder.GetVideoPosition();
				this.viewModel.SetVideoPositionFromNonUser(position);
			}
		}

		public void SeekToViewModelPosition(TimeSpan position)
		{
			var previewholder = this.MainContent as IPreviewFrame;
			if (previewholder != null)
			{
				previewholder.SetVideoPosition(this.viewModel.PreviewVideoPosition);
			}
		}

		private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
		{
			this.viewModel = (PreviewWindowViewModel)this.DataContext;
			this.viewModel.View = this;

			this.mainDisplayUpdateSubscription = Observable.CombineLatest(
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

			this.controlsUpdateSubscription = this.viewModel.ControlsObservable
				.Subscribe(controls =>
				{
					this.OnControlsUpdate(controls);
				});

			this.previewPausedSubscription = this.viewModel.WhenAnyValue(x => x.PreviewPaused)
				.Subscribe(paused =>
				{
					var previewholder = this.MainContent as IPreviewFrame;
					if (previewholder != null)
					{
						if (paused)
						{
							previewholder.Pause();
						}
						else
						{
							previewholder.Play();
						}
					}
				});
		}

		private void OnMainDisplayUpdate(PreviewMainDisplay mainDisplay, bool playingPreview)
		{
			this.RefreshMainContent(mainDisplay, playingPreview);
			this.RefreshImageSize();
		}

		private void RefreshMainContent(PreviewMainDisplay mainDisplay, bool playingPreview)
		{
			// Close the video if we're swapping it out.
			var previewFrame = this.MainContent as IPreviewFrame;
			if (!playingPreview && previewFrame != null)
			{
				previewFrame.CloseVideo();
			}

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

			previewFrame = this.MainContent as IPreviewFrame;
			if (previewFrame != null)
			{
				if (playingPreview)
				{
					previewFrame.SetVideo(this.viewModel.OnVideoCompleted, this.viewModel.OnVideoFailed, this.viewModel.WhenAnyValue(x => x.Volume));
				}
				else
				{
					previewFrame.SetImage();
				}
			}
		}

		private void OnControlsUpdate(PreviewControls controls)
		{
			switch (controls)
			{
				case PreviewControls.Creation:
					if (!(this.ControlsContent is PreviewCreationControls))
					{
						this.ControlsContent = new PreviewCreationControls();
					}

					break;
				case PreviewControls.Generation:
					if (!(this.ControlsContent is PreviewGeneratingControls))
					{
						this.ControlsContent = new PreviewGeneratingControls();
					}

					break;
				case PreviewControls.Playback:
					if (!(this.ControlsContent is PreviewPlaybackControls))
					{
						this.ControlsContent = new PreviewPlaybackControls();
					}

					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(controls), controls, null);
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

		private UIElement ControlsContent
		{
			get
			{
				if (this.previewControls.Children.Count == 0)
				{
					return null;
				}

				return this.previewControls.Children[0];
			}

			set
			{
				this.previewControls.Children.Clear();
				this.previewControls.Children.Add(value);
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

			OutputSizeInfo outputSize = previewVM.PreviewImageService.OutputSizeInfo;

			if (outputSize != null)
			{
				int displayHeight = outputSize.OutputHeight;
				int displayWidth = outputSize.DisplayWidth;

				switch (previewVM.MainDisplay)
				{
					case PreviewMainDisplay.Default:
						fitControl = this.MainContent as PreviewFit;
						fitControl?.ResizeHolder(this.previewArea, displayWidth, displayHeight, showOneToOneWhenSmaller: true);

						break;
					case PreviewMainDisplay.FitToWindow:
						fitControl = this.MainContent as PreviewFit;
						fitControl?.ResizeHolder(this.previewArea, displayWidth, displayHeight, showOneToOneWhenSmaller: false);

						break;
					case PreviewMainDisplay.OneToOne:
						var oneToOneControl = this.MainContent as PreviewOneToOne;
						oneToOneControl?.ResizeHolder(displayWidth, displayHeight);

						break;
					case PreviewMainDisplay.StillCorners:
						var cornersControl = this.MainContent as PreviewCorners;
						cornersControl?.UpdateCornerImages();

						break;
				}
			}
		}

		private void OnVideoClick(object sender, MouseButtonEventArgs e)
		{
			if (this.viewModel.PreviewPaused)
			{
				this.viewModel.Play.Execute(null);
			}
			else
			{
				this.viewModel.Pause.Execute(null);
			}
		}

		private void OnMouseWheel(object sender, MouseWheelEventArgs e)
		{
			if (e.Delta > 0)
			{
				this.viewModel.PreviewImageServiceClient.ShowPreviousPreview();
			}
			else
			{
				this.viewModel.PreviewImageServiceClient.ShowNextPreview();
			}
		}

		private void OnKeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Left)
			{
				this.viewModel.PreviewImageServiceClient.ShowPreviousPreview();
				e.Handled = true;
			}
			else if (e.Key == Key.Right)
			{
				this.viewModel.PreviewImageServiceClient.ShowNextPreview();
				e.Handled = true;
			}
		}

		private void PreviewWindow_OnClosing(object sender, CancelEventArgs e)
		{
			this.mainDisplayUpdateSubscription?.Dispose();
			this.previewPausedSubscription?.Dispose();
			this.controlsUpdateSubscription?.Dispose();
		}
	}
}
