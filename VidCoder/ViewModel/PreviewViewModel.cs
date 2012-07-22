using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;
using HandBrake.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using HandBrake.Interop.Model;
using HandBrake.Interop.Model.Encoding;
using VidCoder.Messages;
using VidCoder.Properties;
using System.IO;
using Microsoft.Practices.Unity;
using VidCoder.Services;
using System.Threading;
using VidCoder.Model;
using System.Diagnostics;
using VidCoder.ViewModel.Components;

namespace VidCoder.ViewModel
{
	public class PreviewViewModel : OkCancelDialogViewModel
	{
		private const int PreviewImageCacheDistance = 1;
		private const string NoSourceTitle = "Preview: No video source";

		private static readonly TimeSpan MinPreviewImageRefreshInterval = TimeSpan.FromSeconds(1);
		private static int updateVersion;

		private EncodeJob job;
		private HandBrakeInstance originalScanInstance;
		private HandBrakeInstance previewInstance;
		private ILogger logger = Unity.Container.Resolve<ILogger>();
		private string title;
		private int selectedPreview;
		private bool hasPreview;
		private bool scanningPreview;
		private bool generatingPreview;
		private string previewFilePath;
		private bool encodeCancelled;
		private double previewPercentComplete;
		private int previewSeconds;
		private int previewCount;

		private DateTime lastImageRefreshTime;
		private System.Timers.Timer previewImageRefreshTimer;
		private bool waitingOnRefresh;
		private ImageSource previewImage;
		private ImageSource[] previewImageCache;
		private Queue<PreviewImageJob> previewImageWorkQueue = new Queue<PreviewImageJob>();
		private bool previewImageQueueProcessing;
		private object imageSync = new object();
		private List<object> imageFileSync;
		private string imageFileCacheFolder;

		private MainViewModel mainViewModel = Unity.Container.Resolve<MainViewModel>();
		private OutputPathViewModel outputPathVM = Unity.Container.Resolve<OutputPathViewModel>();
		private WindowManagerViewModel windowManagerVM = Unity.Container.Resolve<WindowManagerViewModel>();
		private ProcessingViewModel processingVM = Unity.Container.Resolve<ProcessingViewModel>();

		public PreviewViewModel()
		{
			Messenger.Default.Register<RefreshPreviewMessage>(
				this,
				message =>
				{
					this.RequestRefreshPreviews();
				});

			this.previewSeconds = Settings.Default.PreviewSeconds;
			this.selectedPreview = 1;
			this.Title = NoSourceTitle;

			this.RequestRefreshPreviews();
		}

		public MainViewModel MainViewModel
		{
			get
			{
				return this.mainViewModel;
			}
		}

		public WindowManagerViewModel WindowManagerVM
		{
			get
			{
				return this.windowManagerVM;
			}
		}

		public ProcessingViewModel ProcessingVM
		{
			get
			{
				return this.processingVM;
			}
		}

		public OutputPathViewModel OutputPathVM
		{
			get
			{
				return this.outputPathVM;
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
				this.RaisePropertyChanged(() => this.Title);
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
				this.RaisePropertyChanged(() => this.PreviewImage);
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
				this.RaisePropertyChanged(() => this.GeneratingPreview);
				this.RaisePropertyChanged(() => this.SeekBarEnabled);
			}
		}

		public bool SeekBarEnabled
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
				this.RaisePropertyChanged(() => this.PreviewPercentComplete);
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
				this.RaisePropertyChanged(() => this.PreviewSeconds);

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
				this.GeneratePreviewCommand.RaiseCanExecuteChanged();
				this.PlaySourceCommand.RaiseCanExecuteChanged();
				this.RaisePropertyChanged(() => this.SeekBarEnabled);
				this.RaisePropertyChanged(() => this.HasPreview);
				this.RaisePropertyChanged(() => this.PlayAvailable);
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
				this.RaisePropertyChanged(() => this.SelectedPreview);

				lock (this.imageSync)
				{
					this.PreviewImage = this.previewImageCache[value];
					this.ClearOutOfRangeItems();
					this.BeginBackgroundImageLoad();
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
				if (this.previewCount > 0)
				{
					return this.previewCount - 1;
				}

				return Settings.Default.PreviewCount - 1;
			}
		}

		public int PreviewCount
		{
			get
			{
				if (this.previewCount > 0)
				{
					return this.previewCount;
				}

				return Settings.Default.PreviewCount;
			}
		}

		public bool PlayAvailable
		{
			get
			{
				if (!this.HasPreview || this.mainViewModel.SourcePath == null)
				{
					return false;
				}

				string sourcePath = this.mainViewModel.SourcePath;
				var fileAttributes = File.GetAttributes(sourcePath);
				if ((fileAttributes & FileAttributes.Directory) == FileAttributes.Directory)
				{
					// Path is a directory. Can only preview when it's a DVD and we have a supported player installed.
					bool isDvd = Utilities.IsDvdFolder(this.mainViewModel.SourcePath);
					bool playerInstalled = Players.Installed.Count > 0;

					return isDvd && playerInstalled;
				}
				else
				{
					// Path is a file
					return true;
				}
			}
		}

		public HandBrakeInstance ScanInstance
		{
			get
			{
				return this.mainViewModel.ScanInstance;
			}
		}

		private RelayCommand generatePreviewCommand;
		public RelayCommand GeneratePreviewCommand
		{
			get
			{
				return this.generatePreviewCommand ?? (this.generatePreviewCommand = new RelayCommand(() =>
					{
						this.job = this.mainViewModel.EncodeJob;
						this.logger.Log("Generating preview clip");
						this.logger.Log("Scanning title");

						this.previewInstance = new HandBrakeInstance();
						this.previewInstance.Initialize(Settings.Default.LogVerbosity);
						this.previewInstance.ScanCompleted += this.OnPreviewScanCompleted;
						this.previewInstance.StartScan(this.job.SourcePath, Settings.Default.PreviewCount, this.job.Title);

						this.PreviewPercentComplete = 0;
						this.scanningPreview = true;
						this.GeneratingPreview = true;
						this.encodeCancelled = false;
					}, () =>
					{
						return this.HasPreview;
					}));
			}
		}

		private RelayCommand playSourceCommand;
		public RelayCommand PlaySourceCommand
		{
			get
			{
				return this.playSourceCommand ?? (this.playSourceCommand = new RelayCommand(() =>
				    {
						string sourcePath = this.mainViewModel.SourcePath;
						var fileAttributes = File.GetAttributes(sourcePath);
						if ((fileAttributes & FileAttributes.Directory) == FileAttributes.Directory)
						{
							// Path is a directory
							IVideoPlayer player = Players.Installed.FirstOrDefault(p => p.Id == Settings.Default.PreferredPlayer);
							if (player == null)
							{
								player = Players.Installed[0];
							}

							player.PlayTitle(sourcePath, this.mainViewModel.SelectedTitle.TitleNumber);
						}
						else
						{
							// Path is a file
							FileService.Instance.LaunchFile(sourcePath);
						}
				    }, () =>
				    {
						return this.PlayAvailable;
				    }));
			}
		}

		private RelayCommand cancelPreviewCommand;
		public RelayCommand CancelPreviewCommand
		{
			get
			{
				return this.cancelPreviewCommand ?? (this.cancelPreviewCommand = new RelayCommand(() =>
					{
						this.CancelPreviewEncode();
					}));
			}
		}

		public void RequestRefreshPreviews()
		{
			if (!this.mainViewModel.HasVideoSource)
			{
				this.HasPreview = false;
				this.Title = NoSourceTitle;
				this.CancelPreviewEncode();
				return;
			}

			if (this.originalScanInstance != this.ScanInstance || (this.job != null && this.job.Title != this.mainViewModel.EncodeJob.Title))
			{
				this.CancelPreviewEncode();
			}

			if (this.waitingOnRefresh)
			{
				return;
			}

			DateTime now = DateTime.Now;
			TimeSpan timeSinceLastRefresh = now - this.lastImageRefreshTime;
			if (timeSinceLastRefresh < MinPreviewImageRefreshInterval)
			{
				this.waitingOnRefresh = true;
				TimeSpan timeUntilNextRefresh = MinPreviewImageRefreshInterval - timeSinceLastRefresh;
				this.previewImageRefreshTimer = new System.Timers.Timer(timeUntilNextRefresh.TotalMilliseconds);
				this.previewImageRefreshTimer.Elapsed += this.previewImageRefreshTimer_Elapsed;
				this.previewImageRefreshTimer.AutoReset = false;
				this.previewImageRefreshTimer.Start();

				return;
			}

			this.lastImageRefreshTime = now;

			this.RefreshPreviews();
		}

		private void previewImageRefreshTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
		{
			this.waitingOnRefresh = false;
			this.lastImageRefreshTime = DateTime.MinValue;
			DispatchService.BeginInvoke(this.RefreshPreviews);
		}

		private void RefreshPreviews()
		{
			this.originalScanInstance = this.ScanInstance;

			this.job = this.mainViewModel.EncodeJob;

			//EncodingProfile profile = this.job.EncodingProfile;
			int width, height;

			int parWidth, parHeight;
			this.ScanInstance.GetSize(this.job, out width, out height, out parWidth, out parHeight);

			if (parWidth <= 0 || parHeight <= 0)
			{
				this.HasPreview = false;
				this.Title = NoSourceTitle;

				Unity.Container.Resolve<ILogger>().LogError("HandBrake returned a negative pixel aspect ratio. Cannot show preview.");
				return;
			}

			this.PreviewHeight = height;
			this.PreviewWidth = width * ((double)parWidth / parHeight);

			// Update the number of previews.
			this.previewCount = this.ScanInstance.PreviewCount;
			if (this.selectedPreview >= this.previewCount)
			{
				this.selectedPreview = this.previewCount - 1;
				this.RaisePropertyChanged(() => this.SelectedPreview);
			}

			this.RaisePropertyChanged(() => this.PreviewCount);

			this.HasPreview = true;

			lock (this.imageSync)
			{
				this.previewImageCache = new ImageSource[this.previewCount];
				updateVersion++;

				// Clear main work queue.
				this.previewImageWorkQueue.Clear();

				this.imageFileCacheFolder = Path.Combine(Utilities.ImageCacheFolder, Process.GetCurrentProcess().Id.ToString(), updateVersion.ToString());
				if (!Directory.Exists(this.imageFileCacheFolder))
				{
					Directory.CreateDirectory(this.imageFileCacheFolder);
				}

				// Clear old images out of the file cache.
				this.clearImageFileCache();

				this.imageFileSync = new List<object>(this.previewCount);
				for (int i = 0; i < this.previewCount; i++)
				{
					this.imageFileSync.Add(new object());
				}

				this.BeginBackgroundImageLoad();
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

		private void clearImageFileCache()
		{
			try
			{
				string processCacheFolder = Path.Combine(Utilities.ImageCacheFolder, Process.GetCurrentProcess().Id.ToString());

				int lowestUpdate = -1;
				for (int i = updateVersion - 1; i >= 1; i--)
				{
					if (Directory.Exists(Path.Combine(processCacheFolder, i.ToString())))
					{
						lowestUpdate = i;
					}
					else
					{
						break;
					}
				}

				if (lowestUpdate == -1)
				{
					return;
				}

				for (int i = lowestUpdate; i <= updateVersion - 1; i++)
				{
					Utilities.DeleteDirectory(Path.Combine(processCacheFolder, i.ToString()));
				}
			}
			catch (IOException)
			{
				// Ignore. Later checks will clear the cache.
			}
		}

		private void CancelPreviewEncode()
		{
			if (this.GeneratingPreview)
			{
				this.encodeCancelled = true;
				if (this.scanningPreview)
				{
					this.previewInstance.StopScan();
				}
				else
				{
					this.previewInstance.StopEncode();
				}
			}
		}

		private void ClearOutOfRangeItems()
		{
			// Remove out of range items from work queue
			var newWorkQueue = new Queue<PreviewImageJob>();
			while (this.previewImageWorkQueue.Count > 0)
			{
				PreviewImageJob job = this.previewImageWorkQueue.Dequeue();
				if (Math.Abs(job.PreviewNumber - this.SelectedPreview) <= PreviewImageCacheDistance)
				{
					newWorkQueue.Enqueue(job);
				}
			}

			// Remove out of range cache entries
			for (int i = 0; i < this.previewCount; i++)
			{
				if (Math.Abs(i - this.SelectedPreview) > PreviewImageCacheDistance)
				{
					this.previewImageCache[i] = null;
				}
			}
		}

		private void BeginBackgroundImageLoad()
		{
			int initialQueueSize = this.previewImageWorkQueue.Count;
			int currentPreview = this.SelectedPreview;

			if (!ImageLoadedOrLoading(currentPreview))
			{
				this.EnqueueWork(currentPreview);
			}

			for (int i = 1; i <= PreviewImageCacheDistance; i++)
			{
				if (currentPreview - i >= 0 && !ImageLoadedOrLoading(currentPreview - i))
				{
					EnqueueWork(currentPreview - i);
				}

				if (currentPreview + i < this.previewCount && !ImageLoadedOrLoading(currentPreview + i))
				{
					EnqueueWork(currentPreview + i);
				}
			}

			// Start a queue processing thread if one is not going already.
			if (!this.previewImageQueueProcessing && this.previewImageWorkQueue.Count > 0)
			{
				ThreadPool.QueueUserWorkItem(this.ProcessPreviewImageWork);
				this.previewImageQueueProcessing = true;
			}
		}

		private bool ImageLoadedOrLoading(int previewNumber)
		{
			if (this.previewImageCache[previewNumber] != null)
			{
				return true;
			}

			if (this.previewImageWorkQueue.Count(j => j.PreviewNumber == previewNumber) > 0)
			{
				return true;
			}

			return false;
		}

		private void EnqueueWork(int previewNumber)
		{
			this.previewImageWorkQueue.Enqueue(
				new PreviewImageJob
				{
					UpdateVersion = updateVersion,
					ScanInstance = this.ScanInstance,
					PreviewNumber = previewNumber,
					EncodeJob = this.job,
					ImageFileSync = this.imageFileSync[previewNumber]
				});
		}

		private void ProcessPreviewImageWork(object state)
		{
			PreviewImageJob imageJob;
			bool workLeft = true;

			while (workLeft)
			{
				lock (this.imageSync)
				{
					if (this.previewImageWorkQueue.Count == 0)
					{
						this.previewImageQueueProcessing = false;
						return;
					}

					imageJob = this.previewImageWorkQueue.Dequeue();
				}

				string imagePath = Path.Combine(
					Utilities.ImageCacheFolder,
					Process.GetCurrentProcess().Id.ToString(),
					imageJob.UpdateVersion.ToString(),
					imageJob.PreviewNumber + ".bmp");
				BitmapImage image = null;

				// Check the disc cache for the image
				lock (imageJob.ImageFileSync)
				{
					if (File.Exists(imagePath))
					{
						image = new BitmapImage();
						image.BeginInit();
						image.CacheOption = BitmapCacheOption.OnLoad;
						image.UriSource = new Uri(imagePath);
						image.EndInit();
						image.Freeze();
					}
				}

				if (image == null)
				{
					// Make a HandBrake call to get the image
					image = imageJob.ScanInstance.GetPreview(imageJob.EncodeJob, imageJob.PreviewNumber);

					// Start saving the image file in the background and continue to process the queue.
					ThreadPool.QueueUserWorkItem(
						this.BackgroundFileSave,
						new SaveImageJob
						{
							PreviewNumber = imageJob.PreviewNumber,
							UpdateVersion = imageJob.UpdateVersion,
							FilePath = imagePath,
							Image = image,
							ImageFileSync = imageJob.ImageFileSync
						});
				}

				lock (this.imageSync)
				{
					if (imageJob.UpdateVersion == updateVersion)
					{
						this.previewImageCache[imageJob.PreviewNumber] = image;
						if (this.SelectedPreview == imageJob.PreviewNumber)
						{
							DispatchService.BeginInvoke(() =>
							{
								this.PreviewImage = image;
							});
						}
					}

					if (this.previewImageWorkQueue.Count == 0)
					{
						workLeft = false;
						this.previewImageQueueProcessing = false;
					}
				}
			}
		}

		private void BackgroundFileSave(object state)
		{
			var job = state as SaveImageJob;

			lock (this.imageSync)
			{
				if (job.UpdateVersion < updateVersion)
				{
					return;
				}
			}

			lock (job.ImageFileSync)
			{
				try
				{
					using (var memoryStream = new MemoryStream())
					{
						// Write the bitmap out to a memory stream before saving so that we won't be holding
						// a write lock on the BitmapImage for very long; it's used in the UI.
						var encoder = new BmpBitmapEncoder();
						encoder.Frames.Add(BitmapFrame.Create(job.Image));
						encoder.Save(memoryStream);

						using (var fileStream = new FileStream(job.FilePath, FileMode.Create))
						{
							fileStream.Write(memoryStream.GetBuffer(), 0, (int)memoryStream.Length);
						}
					}
				}
				catch (IOException)
				{
					// Directory may have been deleted. Ignore.
				}
			}
		}

		private void OnPreviewScanCompleted(object sender, EventArgs eventArgs)
		{
			this.scanningPreview = false;

			if (this.encodeCancelled)
			{
				this.GeneratingPreview = false;
				this.logger.Log("Cancelled preview clip generation");
				return;
			}

			string extension = null;

			if (this.job.EncodingProfile.OutputFormat == Container.Mkv)
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

			this.previewFilePath = Path.Combine(previewDirectory, @"preview" + extension);
			this.job.OutputPath = previewFilePath;
			this.previewInstance.EncodeProgress += (o, e) =>
			{
				this.PreviewPercentComplete = e.FractionComplete * 100;
			};
			this.previewInstance.EncodeCompleted += (o, e) =>
			{
				this.GeneratingPreview = false;

				if (this.encodeCancelled)
				{
					this.logger.Log("Cancelled preview clip generation");
				}
				else
				{
					if (e.Error)
					{
						this.logger.Log("Preview clip generation failed");
						Utilities.MessageBox.Show("Preview clip generation failed. See the Log window for details.");
					}
					else
					{
						var previewFileInfo = new FileInfo(this.previewFilePath);
						this.logger.Log("Finished preview clip generation. Size: " + Utilities.FormatFileSize(previewFileInfo.Length));

						FileService.Instance.LaunchFile(previewFilePath);
					}
				}
			};

			this.logger.Log("Encoding clip");
			this.logger.Log("  Path: " + this.job.OutputPath);
			this.logger.Log("  Title: " + this.job.Title);
			this.logger.Log("  Preview #: " + this.SelectedPreview);

			this.previewInstance.StartEncode(this.job, true, this.SelectedPreview, this.PreviewSeconds, this.job.Length.TotalSeconds);
		}
	}
}
