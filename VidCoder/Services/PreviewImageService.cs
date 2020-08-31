using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DynamicData;
using HandBrake.Interop.Interop;
using Microsoft.AnyContainer;
using ReactiveUI;
using VidCoder.Extensions;
using VidCoder.Model;
using VidCoder.Resources;
using VidCoder.ViewModel;
using VidCoderCommon.Model;

namespace VidCoder.Services
{
    public class PreviewImageService : ReactiveObject
    {
		private const int PreviewImageCacheDistance = 1;

		private static readonly TimeSpan MinPreviewImageRefreshInterval = TimeSpan.FromSeconds(0.5);
	    private static int updateVersion;

		private VCJob job;
		private int previewCount;
		private DateTime lastImageRefreshTime;
	    private System.Timers.Timer previewImageRefreshTimer;
	    private Queue<PreviewImageJob> previewImageWorkQueue = new Queue<PreviewImageJob>();
		private bool previewImageQueueProcessing;
	    private BitmapSource[] previewImageCache;
	    private bool waitingOnRefresh;
		private bool loggedFileSaveError;

	    private IDisposable presetsSubscription;

		private readonly SourceList<PreviewImageServiceClient> clients = new SourceList<PreviewImageServiceClient>();

		private object imageSync = new object();
	    private List<object> imageFileSync;

		private readonly OutputSizeService outputSizeService = StaticResolver.Resolve<OutputSizeService>();
	    private readonly MainViewModel mainViewModel = StaticResolver.Resolve<MainViewModel>();
	    private readonly PreviewUpdateService previewUpdateService = StaticResolver.Resolve<PreviewUpdateService>();

		public PreviewImageService()
	    {
			this.previewUpdateService.PreviewInputChanged += this.OnPreviewInputChanged;

		    this.presetsSubscription = this.PresetsService.WhenAnyValue(x => x.SelectedPreset.Preset.EncodingProfile).Skip(1).Subscribe(x =>
		    {
			    this.RequestRefreshPreviews();
		    });

			this.clients
				.Connect()
				.WhenValueChanged(client => client.PreviewIndex)
				.Skip(1)
				.Subscribe(client =>
				{
					this.ClearOutOfRangeItems();
					this.BeginBackgroundImageLoad();
				});

			this.RequestRefreshPreviews();
		}

		public event EventHandler<PreviewImageLoadInfo> ImageLoaded;

		public PresetsService PresetsService { get; } = StaticResolver.Resolve<PresetsService>();


		private OutputSizeInfo outputSizeInfo;
	    public OutputSizeInfo OutputSizeInfo
	    {
		    get { return this.outputSizeInfo; }
		    set { this.RaiseAndSetIfChanged(ref this.outputSizeInfo, value); }
	    }

	    private Color padColor;
	    public Color PadColor
	    {
		    get { return this.padColor; }
		    set { this.RaiseAndSetIfChanged(ref this.padColor, value); }
	    }

	    private bool hasPreview;
	    public bool HasPreview
	    {
		    get { return this.hasPreview; }
		    set { this.RaiseAndSetIfChanged(ref this.hasPreview, value); }
	    }

	    public HandBrakeInstance ScanInstance
	    {
		    get { return this.mainViewModel.ScanInstance; }
	    }

	    public int PreviewCount
	    {
		    get
		    {
			    if (this.previewCount > 0)
			    {
				    return this.previewCount;
			    }

			    return Config.PreviewCount;
		    }
	    }

		public void RegisterClient(PreviewImageServiceClient client)
	    {
		    this.clients.Add(client);

		    this.RequestRefreshPreviews();
		}

		public void RemoveClient(PreviewImageServiceClient client)
	    {
			this.clients.Remove(client);
	    }

		/// <summary>
		/// Tries to get the preview at the given index.
		/// </summary>
		/// <param name="previewIndex">The 0-based index of the preview to start creation for.</param>
		/// <returns>The bitmap source at that index, or null if it doesn't exist yet.</returns>
		public BitmapSource TryGetPreviewImage(int previewIndex)
	    {
		    BitmapSource cachedImage = this.previewImageCache[previewIndex];
		    if (cachedImage != null)
		    {
			    return cachedImage;
		    }

		    return null;
	    }

	    private void OnPreviewInputChanged(object sender, EventArgs eventArgs)
	    {
		    this.RequestRefreshPreviews();
	    }

		private void RequestRefreshPreviews()
	    {
		    if (this.clients.Count == 0)
		    {
			    return;
		    }

		    if (!this.mainViewModel.HasVideoSource || this.outputSizeService.Size == null)
		    {
			    this.HasPreview = false;
			    return;
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
		    DispatchUtilities.BeginInvoke(this.RefreshPreviews);
	    }

	    private void RefreshPreviews()
	    {
		    this.job = this.mainViewModel.EncodeJob;

		    OutputSizeInfo newOutputSizeInfo = this.outputSizeService.Size;

		    int width = newOutputSizeInfo.OutputWidth;
		    int height = newOutputSizeInfo.OutputHeight;
		    int parWidth = newOutputSizeInfo.Par.Num;
		    int parHeight = newOutputSizeInfo.Par.Den;

		    if (parWidth <= 0 || parHeight <= 0)
		    {
			    this.HasPreview = false;

			    StaticResolver.Resolve<IAppLogger>().LogError("HandBrake returned a negative pixel aspect ratio. Cannot show preview.");
			    return;
		    }

		    if (width < 100 || height < 100)
		    {
			    this.HasPreview = false;

			    return;
		    }

		    this.OutputSizeInfo = newOutputSizeInfo;

		    var profile = this.PresetsService.SelectedPreset.Preset.EncodingProfile;
		    this.PadColor = ColorUtilities.ToWindowsColor(profile.PadColor);

		    // Update the number of previews.
		    this.previewCount = this.ScanInstance?.PreviewCount ?? 10;
		    foreach (PreviewImageServiceClient client in this.clients.Items)
		    {
			    if (client.PreviewIndex >= this.previewCount)
			    {
				    client.PreviewIndex = this.previewCount - 1;
			    }
		    }

		    this.RaisePropertyChanged(nameof(this.PreviewCount));

		    this.HasPreview = true;

		    lock (this.imageSync)
		    {
			    this.previewImageCache = new BitmapSource[this.previewCount];
			    updateVersion++;

			    // Clear main work queue.
			    this.previewImageWorkQueue.Clear();

			    // Clear old images out of the file cache.
			    this.ClearImageFileCache();

			    this.imageFileSync = new List<object>(this.previewCount);
			    for (int i = 0; i < this.previewCount; i++)
			    {
				    this.imageFileSync.Add(new object());
			    }

			    this.BeginBackgroundImageLoad();
		    }
	    }

	    private void ClearImageFileCache()
	    {
		    try
		    {
			    string processCacheFolder = Path.Combine(Utilities.ImageCacheFolder, Process.GetCurrentProcess().Id.ToString(CultureInfo.InvariantCulture));
			    if (!Directory.Exists(processCacheFolder))
			    {
				    return;
			    }

			    int lowestUpdate = -1;
			    for (int i = updateVersion - 1; i >= 1; i--)
			    {
				    if (Directory.Exists(Path.Combine(processCacheFolder, i.ToString(CultureInfo.InvariantCulture))))
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
				    FileUtilities.DeleteDirectory(Path.Combine(processCacheFolder, i.ToString(CultureInfo.InvariantCulture)));
			    }
		    }
		    catch (Exception)
		    {
			    // Ignore. Later checks will clear the cache.
		    }
	    }

	    private void ClearOutOfRangeItems()
	    {
		    if (this.previewCount <= 0)
		    {
			    return;
		    } 

			// Determine which slots are in range of a client
		    bool[] inRange = new bool[this.previewCount];
		    foreach (PreviewImageServiceClient client in this.clients.Items)
		    {
			    inRange[client.PreviewIndex] = true;
			    for (int i = 1; i <= PreviewImageCacheDistance; i++)
			    {
				    if (client.PreviewIndex - i >= 0)
				    {
					    inRange[client.PreviewIndex - i] = true;
				    }

				    if (client.PreviewIndex + i < this.previewCount)
				    {
					    inRange[client.PreviewIndex + i] = true;
				    }
			    }
		    }

		    // Remove out of range items from work queue
		    var newWorkQueue = new Queue<PreviewImageJob>();
		    while (this.previewImageWorkQueue.Count > 0)
		    {
			    PreviewImageJob job = this.previewImageWorkQueue.Dequeue();
			    if (inRange[job.PreviewIndex])
			    {
				    newWorkQueue.Enqueue(job);
			    }
		    }

		    // Remove out of range cache entries
		    for (int i = 0; i < this.previewCount; i++)
		    {
			    if (!inRange[i])
			    {
				    this.previewImageCache[i] = null;
			    }
		    }
	    }

	    private void BeginBackgroundImageLoad()
	    {
		    if (this.previewCount <= 0 || this.ScanInstance == null)
		    {
			    return;
		    }

		    foreach (PreviewImageServiceClient client in this.clients.Items)
		    {
			    if (!this.ImageLoadedOrLoading(client.PreviewIndex))
			    {
				    this.EnqueueWork(client.PreviewIndex);
			    }

			    for (int i = 1; i <= PreviewImageCacheDistance; i++)
			    {
				    if (client.PreviewIndex - i >= 0 && !this.ImageLoadedOrLoading(client.PreviewIndex - i))
				    {
					    this.EnqueueWork(client.PreviewIndex - i);
				    }

				    if (client.PreviewIndex + i < this.previewCount && !this.ImageLoadedOrLoading(client.PreviewIndex + i))
				    {
					    this.EnqueueWork(client.PreviewIndex + i);
				    }
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

		    if (this.previewImageWorkQueue.Count(j => j.PreviewIndex == previewNumber) > 0)
		    {
			    return true;
		    }

		    return false;
	    }

	    private void EnqueueWork(int previewNumber)
	    {
		    this.previewImageWorkQueue.Enqueue(new PreviewImageJob
		    {
			    UpdateVersion = updateVersion,
			    ScanInstance = this.ScanInstance,
			    PreviewIndex = previewNumber,
			    Profile = this.job.EncodingProfile,
			    Title = this.mainViewModel.SelectedTitle.Title,
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
				    while (imageJob.UpdateVersion < updateVersion)
				    {
					    if (this.previewImageWorkQueue.Count == 0)
					    {
						    this.previewImageQueueProcessing = false;
						    return;
					    }

					    imageJob = this.previewImageWorkQueue.Dequeue();
				    }
			    }

			    string imagePath = Path.Combine(
				    Utilities.ImageCacheFolder,
				    Process.GetCurrentProcess().Id.ToString(CultureInfo.InvariantCulture),
				    imageJob.UpdateVersion.ToString(CultureInfo.InvariantCulture),
				    imageJob.PreviewIndex.ToString(CultureInfo.InvariantCulture) + ".bmp");
			    BitmapSource imageSource = null;

			    // Check the disc cache for the image
			    lock (imageJob.ImageFileSync)
			    {
				    if (File.Exists(imagePath))
				    {
					    Uri imageUri;
					    if (Uri.TryCreate(imagePath, UriKind.Absolute, out imageUri))
					    {
						    // When we read from disc cache the image is already transformed.
						    var bitmapImage = new BitmapImage();
						    bitmapImage.BeginInit();
						    bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
						    bitmapImage.UriSource = imageUri;
						    bitmapImage.EndInit();
						    bitmapImage.Freeze();

						    imageSource = bitmapImage;
					    }
					    else
					    {
						    StaticResolver.Resolve<IAppLogger>().LogError($"Could not load cached preview image from {imagePath} . Did not parse as a URI.");
					    }
				    }
			    }

			    if (imageSource == null && !imageJob.ScanInstance.IsDisposed)
			    {
				    // Make a HandBrake call to get the image
				    imageSource = BitmapUtilities.ConvertToBitmapImage(BitmapUtilities.ConvertByteArrayToBitmap(imageJob.ScanInstance.GetPreview(imageJob.Profile.CreatePreviewSettings(imageJob.Title), imageJob.PreviewIndex, imageJob.Profile.DeinterlaceType != VCDeinterlace.Off)));

				    // Transform the image as per rotation and reflection settings
				    VCProfile profile = imageJob.Profile;
				    if (profile.FlipHorizontal || profile.FlipVertical || profile.Rotation != VCPictureRotation.None)
				    {
					    imageSource = CreateTransformedBitmap(imageSource, profile);
				    }

				    // Start saving the image file in the background and continue to process the queue.
				    ThreadPool.QueueUserWorkItem(this.BackgroundFileSave, new SaveImageJob
				    {
					    PreviewNumber = imageJob.PreviewIndex,
					    UpdateVersion = imageJob.UpdateVersion,
					    FilePath = imagePath,
					    Image = imageSource,
					    ImageFileSync = imageJob.ImageFileSync
				    });
			    }

			    lock (this.imageSync)
			    {
				    if (imageJob.UpdateVersion == updateVersion)
				    {
					    this.previewImageCache[imageJob.PreviewIndex] = imageSource;
					    int previewIndex = imageJob.PreviewIndex;
					    DispatchUtilities.BeginInvoke(() =>
					    {
							this.ImageLoaded?.Invoke(this, new PreviewImageLoadInfo { PreviewImage = imageSource, PreviewIndex = previewIndex });
					    });
				    }

				    if (this.previewImageWorkQueue.Count == 0)
				    {
					    workLeft = false;
					    this.previewImageQueueProcessing = false;
				    }
			    }
		    }
	    }

	    private static TransformedBitmap CreateTransformedBitmap(BitmapSource source, VCProfile profile)
	    {
		    var transformedBitmap = new TransformedBitmap();
		    transformedBitmap.BeginInit();
		    transformedBitmap.Source = source;
		    var transformGroup = new TransformGroup();
		    transformGroup.Children.Add(new ScaleTransform(profile.FlipHorizontal ? -1 : 1, profile.FlipVertical ? -1 : 1));
		    transformGroup.Children.Add(new RotateTransform(ConvertRotationToDegrees(profile.Rotation)));
		    transformedBitmap.Transform = transformGroup;
		    transformedBitmap.EndInit();
		    transformedBitmap.Freeze();

		    return transformedBitmap;
	    }

	    private static double ConvertRotationToDegrees(VCPictureRotation rotation)
	    {
		    switch (rotation)
		    {
			    case VCPictureRotation.None:
				    return 0;
			    case VCPictureRotation.Clockwise90:
				    return 90;
			    case VCPictureRotation.Clockwise180:
				    return 180;
			    case VCPictureRotation.Clockwise270:
				    return 270;
		    }

		    return 0;
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
			    catch (Exception exception)
			    {
					if (!this.loggedFileSaveError)
					{
						StaticResolver.Resolve<IAppLogger>().LogError($"Could not cache preview image to {job.FilePath}: {exception}");
						this.loggedFileSaveError = true;
					}
				}
		    }
	    }
	}
}
