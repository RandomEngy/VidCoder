using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HandBrake.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using VidCoder.Properties;
using System.IO;
using Microsoft.Practices.Unity;
using VidCoder.Services;
using System.Threading;
using VidCoder.Model;

namespace VidCoder.ViewModel
{
	public class PreviewViewModel : OkCancelDialogViewModel
	{
		public const int PreviewImageCount = 10;
		private const string NoSourceTitle = "Preview: No video source";

		private EncodeJob job;
		private HandBrakeInstance previewInstance;
		private ILogger logger = Unity.Container.Resolve<ILogger>();
		private string title;
		private int selectedPreview;
		private bool hasPreview;
		private bool generatingPreview;
		private bool encodeCancelled;
		private double previewPercentComplete;
		private int previewSeconds;

		private ImageSource previewImage;
		private ImageSource[] previewImageCache;
		private int updateVersion;
		private object imageSync = new object();

		private ICommand generatePreviewCommand;
		private ICommand cancelPreviewCommand;

		private MainViewModel mainViewModel = Unity.Container.Resolve<MainViewModel>();

		public PreviewViewModel()
		{
			this.previewSeconds = Settings.Default.PreviewSeconds;
			this.Title = NoSourceTitle;

			this.RefreshPreviews();
		}

		public MainViewModel MainViewModel
		{
			get
			{
				return this.mainViewModel;
			}
		}

		public string Title
		{
			get
			{
				return this.title;
			}

			set
			{
				this.title = value;
				this.NotifyPropertyChanged("Title");
			}
		}

		public ImageSource PreviewImage
		{
			get
			{
				return this.previewImage;
			}

			set
			{
				this.previewImage = value;
				this.NotifyPropertyChanged("PreviewImage");
			}
		}

		public bool GeneratingPreview
		{
			get
			{
				return this.generatingPreview;
			}

			set
			{
				this.generatingPreview = value;
				this.NotifyPropertyChanged("GeneratingPreview");
				this.NotifyPropertyChanged("SliderEnabled");
			}
		}

		public bool SliderEnabled
		{
			get
			{
				return this.HasPreview && !this.GeneratingPreview;
			}
		}

		public double PreviewPercentComplete
		{
			get
			{
				return this.previewPercentComplete;
			}

			set
			{
				this.previewPercentComplete = value;
				this.NotifyPropertyChanged("PreviewPercentComplete");
			}
		}

		public int PreviewSeconds
		{
			get
			{
				return this.previewSeconds;
			}

			set
			{
				this.previewSeconds = value;
				this.NotifyPropertyChanged("PreviewSeconds");

				Settings.Default.PreviewSeconds = value;
				Settings.Default.Save();
			}
		}

		public bool HasPreview
		{
			get
			{
				return this.hasPreview;
			}

			set
			{
				this.hasPreview = value;
				this.NotifyPropertyChanged("SliderEnabled");
				this.NotifyPropertyChanged("HasPreview");
			}
		}

		public int SelectedPreview
		{
			get
			{
				return this.selectedPreview;
			}

			set
			{
				this.selectedPreview = value;
				this.NotifyPropertyChanged("SelectedPreview");

				lock (this.imageSync)
				{
					this.PreviewImage = this.previewImageCache[value];
				}
			}
		}

		/// <summary>
		/// Gets or sets the width of the preview image in pixels.
		/// </summary>
		public double PreviewWidth { get; set; }

		/// <summary>
		/// Gets or sets the height of the preview image in pixels.
		/// </summary>
		public double PreviewHeight { get; set; }

		public int SliderMax
		{
			get
			{
				return PreviewImageCount - 1;
			}
		}

		public HandBrakeInstance ScanInstance
		{
			get
			{
				return this.mainViewModel.ScanInstance;
			}
		}

		public ICommand GeneratePreviewCommand
		{
			get
			{
				if (this.generatePreviewCommand == null)
				{
					this.generatePreviewCommand = new RelayCommand(param =>
					{
						this.job = this.mainViewModel.EncodeJob;
						this.logger.Log("## Generating preview clip");
						this.logger.Log("## Scanning title");


						this.previewInstance = new HandBrakeInstance();
						this.previewInstance.Initialize(Settings.Default.LogVerbosity);
						this.previewInstance.ScanCompleted += this.OnPreviewScanCompleted;
						this.previewInstance.StartScan(this.job.SourcePath, 10, this.job.Title);

						this.PreviewPercentComplete = 0;
						this.GeneratingPreview = true;
					},
					param =>
					{
						return this.HasPreview;
					});
				}

				return this.generatePreviewCommand;
			}
		}

		public ICommand CancelPreviewCommand
		{
			get
			{
				if (this.cancelPreviewCommand == null)
				{
					this.cancelPreviewCommand = new RelayCommand(param =>
					{
						this.encodeCancelled = true;
						this.previewInstance.StopEncode();
					});
				}

				return this.cancelPreviewCommand;
			}
		}

		public static void FindAndRefreshPreviews()
		{
			PreviewViewModel previewWindow = WindowManager.FindWindow(typeof(PreviewViewModel)) as PreviewViewModel;
			if (previewWindow != null)
			{
				previewWindow.RefreshPreviews();
			}
		}

		public void RefreshPreviews()
		{
			if (!this.mainViewModel.HasVideoSource)
			{
				this.HasPreview = false;
				this.Title = NoSourceTitle;
				return;
			}

			this.job = this.mainViewModel.EncodeJob;

			EncodingProfile profile = this.job.EncodingProfile;
			int width, height;

			int parWidth, parHeight;
			this.ScanInstance.GetSize(this.job, out width, out height, out parWidth, out parHeight);
			this.PreviewHeight = height;
			this.PreviewWidth = width * ((double)parWidth / parHeight);

			this.HasPreview = true;

			this.previewImageCache = new ImageSource[PreviewImageCount];
			lock (this.imageSync)
			{
				this.updateVersion++;
				ThreadPool.QueueUserWorkItem(
					this.UpdatePreviewImageBackground,
					new PreviewImageJob
					{
						UpdateVersion = this.updateVersion,
						ScanInstance = this.ScanInstance,
						StartPreviewNumber = this.SelectedPreview,
						EncodeJob = this.job
					});
			}

			if (parWidth == parHeight)
			{
				this.Title = "Preview: " + width + "x" + height;
			}
			else
			{
				this.Title = "Preview: Display " + Math.Round(this.PreviewWidth) + "x" + Math.Round(this.PreviewHeight) + " - Storage " + width + "x" + height;
			}
		}

		private void UpdatePreviewImageBackground(object state)
		{
			var imageJob = state as PreviewImageJob;
			ImageSource startImage = imageJob.ScanInstance.GetPreview(imageJob.EncodeJob, imageJob.StartPreviewNumber);
			if (!this.HandleImageLoadCompleted(startImage, imageJob.StartPreviewNumber, imageJob.UpdateVersion))
			{
				return;
			}

			for (int i = 0; i < PreviewImageCount; i++)
			{
				if (i != imageJob.StartPreviewNumber)
				{
					ImageSource image = imageJob.ScanInstance.GetPreview(imageJob.EncodeJob, i);
					if (!this.HandleImageLoadCompleted(image, i, imageJob.UpdateVersion))
					{
						return;
					}
				}
			}
		}

		/// <summary>
		/// Handles a preview image load completion event.
		/// </summary>
		/// <param name="image">The loaded image.</param>
		/// <param name="previewNumber">The number of the loaded image.</param>
		/// <param name="jobUpdateVersion">The version of the current image refresh.</param>
		/// <returns>True if the image was updated. False if another refresh has been triggered
		/// and the results were discarded.</returns>
		private bool HandleImageLoadCompleted(ImageSource image, int previewNumber, int jobUpdateVersion)
		{
			lock (this.imageSync)
			{
				if (jobUpdateVersion != this.updateVersion)
				{
					return false;
				}

				this.previewImageCache[previewNumber] = image;
				if (this.SelectedPreview == previewNumber)
				{
					DispatchService.Invoke(() =>
					{
						this.PreviewImage = image;
					});
				}
			}

			return true;
		}

		private void OnPreviewScanCompleted(object sender, EventArgs eventArgs)
		{
			string extension = null;

			if (this.job.EncodingProfile.OutputFormat == OutputFormat.Mkv)
			{
				extension = ".mkv";
			}
			else
			{
				if (this.job.EncodingProfile.PreferredExtension == OutputExtension.M4v)
				{
					extension = ".m4v";
				}
				else
				{
					extension = ".mp4";
				}
			}

			string previewDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"VidCoder");

			if (!Directory.Exists(previewDirectory))
			{
				Directory.CreateDirectory(previewDirectory);
			}

			this.job.OutputPath = Path.Combine(previewDirectory, @"preview" + extension);
			this.previewInstance.EncodeProgress += (o, e) =>
			{
				this.PreviewPercentComplete = e.FractionComplete * 100;
			};
			this.previewInstance.EncodeCompleted += (o, e) =>
			{
				this.GeneratingPreview = false;

				if (this.encodeCancelled)
				{
					this.logger.Log("# Cancelled preview clip generation");
				}
				else
				{
					if (e.Error)
					{
						this.logger.Log("# Preview clip generation failed");
						Utilities.MessageBox.Show("Preview clip generation failed. See the Log window for details.");
					}
					else
					{
						this.logger.Log("# Finished preview clip generation");
						FileService.Instance.LaunchFile(this.job.OutputPath);
					}
				}
			};

			this.logger.Log("## Encoding clip");
			this.logger.Log("##   Path: " + this.job.OutputPath);
			this.logger.Log("##   Title: " + this.job.Title);
			this.logger.Log("##   Preview #: " + this.SelectedPreview);

			this.encodeCancelled = false;
			this.previewInstance.StartEncode(this.job, true, this.SelectedPreview, this.PreviewSeconds);
		}
	}
}
