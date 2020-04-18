using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Microsoft.AnyContainer;
using ReactiveUI;

namespace VidCoder.Services
{
	/// <summary>
	/// Represents a client to the preview image service.
	/// </summary>
    public class PreviewImageServiceClient : ReactiveObject
	{
		private BitmapSource previewBitmapSource;

		private readonly PreviewImageService previewImageService = StaticResolver.Resolve<PreviewImageService>();

		public PreviewImageServiceClient()
		{
			this.previewImageService.ImageLoaded += this.OnImageLoaded;

			this.previewIndex = 1;
		}

		private int previewIndex;
	    public int PreviewIndex
	    {
		    get { return this.previewIndex; }

		    set
		    {
			    this.RaiseAndSetIfChanged(ref this.previewIndex, value);

			    this.previewBitmapSource = this.previewImageService.TryGetPreviewImage(value);
			    this.RefreshFromBitmapImage();
			}
		}

		private BitmapSource previewImage;
		public BitmapSource PreviewImage
		{
			get
			{
				return this.previewImage;
			}
			set { this.RaiseAndSetIfChanged(ref this.previewImage, value); }
		}

		public void ShowPreviousPreview()
		{
			if (this.PreviewIndex > 0)
			{
				this.PreviewIndex--;
			}
		}

		public void ShowNextPreview()
		{
			if (this.PreviewIndex < this.previewImageService.PreviewCount - 1)
			{
				this.PreviewIndex++;
			}
		}

		private void OnImageLoaded(object sender, PreviewImageLoadInfo e)
		{
			// If the image that loaded happens to be on our slot, show it.
			if (this.PreviewIndex == e.PreviewIndex)
			{
				this.previewBitmapSource = e.PreviewImage;
				this.RefreshFromBitmapImage();
			}
		}

		/// <summary>
		/// Refreshes the view using this.previewBitmapSource.
		/// </summary>
		private void RefreshFromBitmapImage()
		{
			if (this.previewBitmapSource == null)
			{
				return;
			}

			this.PreviewImage = this.previewBitmapSource;
		}
	}
}
