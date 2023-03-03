using DynamicData;
using DynamicData.Binding;
using Microsoft.AnyContainer;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using VidCoder.Model;
using VidCoder.Resources;
using VidCoder.Services;
using VidCoder.Services.Windows;
using VidCoder.ViewModel.DataModels;
using VidCoderCommon;
using VidCoderCommon.Model;

namespace VidCoder.ViewModel
{
	public class WatcherWindowViewModel : ReactiveObject, IClosableWindow
	{
		private const string StatusFilterConfigPrefix = "WatcherShowStatus_";

		private IWindowManager windowManager = StaticResolver.Resolve<IWindowManager>();
		private WatcherProcessManager watcherProcessManager = StaticResolver.Resolve<WatcherProcessManager>();
		private WatchedFileStatusTracker watchedFileStatusTracker = StaticResolver.Resolve<WatchedFileStatusTracker>();
		private IAppLogger logger = StaticResolver.Resolve<IAppLogger>();

		/// <summary>
		/// Maps file path directly to WatchedFileViewModel.
		/// This contains all files and is not filtered, so does not need to be rebuilt when changing filters.
		/// </summary>
		private Dictionary<string, WatchedFileViewModel> fileMap = new Dictionary<string, WatchedFileViewModel>(StringComparer.OrdinalIgnoreCase);

		private PresetsService presetsService = StaticResolver.Resolve<PresetsService>();
		private ProcessingService processingService = StaticResolver.Resolve<ProcessingService>();

		private readonly Dictionary<WatchedFileStatusLive, bool> statusFilters = new Dictionary<WatchedFileStatusLive, bool>();

		public WatcherWindowViewModel()
		{
			this.PopulateStatusFilters();

			this.WatchedFolders.AddRange(WatcherStorage.GetWatchedFolders(Database.Connection).Select(watchedFolder => new WatchedFolderViewModel(this, watchedFolder)));
			this.WatchedFolders.Connect().Bind(this.WatchedFoldersBindable).Subscribe();

			this.InitializeFiles();

			// WindowTitle
			this.WhenAnyValue(x => x.watcherProcessManager.Status)
				.Select(status =>
				{
					return string.Format(WatcherRes.WatcherWindowTitle, GetStatusString(status));
				})
				.ToProperty(this, x => x.WindowTitle, out this.windowTitle);

			// ShowFiles
			Observable.CombineLatest(
				this.WhenAnyValue(x => x.WatcherEnabled),
				this.WatchedFolders.CountChanged,
				(enabled, folderCount) =>
				{
					return folderCount > 0;
				}).ToProperty(this, x => x.ShowFiles, out this.showFiles);
		}

		private async void InitializeFiles()
		{
			// Wait for queue to be ready
			await this.processingService.WaitForQueueReadyAsync();

			this.RefreshFiles();

			this.WatchedFiles.Connect().Bind(this.WatchedFilesBindable).Subscribe();

			if (this.WatcherEnabled)
			{
				this.SubscribeToJobEvents();
			}
		}

		private ObservableAsPropertyHelper<string> windowTitle;
		public string WindowTitle => this.windowTitle.Value;

		public SourceList<WatchedFolderViewModel> WatchedFolders { get; } = new SourceList<WatchedFolderViewModel>();
		public ObservableCollectionExtended<WatchedFolderViewModel> WatchedFoldersBindable { get; } = new ObservableCollectionExtended<WatchedFolderViewModel>();

		public SourceList<WatchedFileViewModel> WatchedFiles { get; } = new SourceList<WatchedFileViewModel>();
		public ObservableCollectionExtended<WatchedFileViewModel> WatchedFilesBindable { get; } = new ObservableCollectionExtended<WatchedFileViewModel>();

		private ObservableAsPropertyHelper<bool> showFiles;
		public bool ShowFiles => this.showFiles.Value;

		private bool watcherEnabled = Config.WatcherEnabled;
		public bool WatcherEnabled
		{
			get { return this.watcherEnabled; }
			set
			{
				Config.WatcherEnabled = value;
				this.RaiseAndSetIfChanged(ref this.watcherEnabled, value);
				if (value)
				{
					this.watcherProcessManager.Start();
					this.watchedFileStatusTracker.Start();
					this.SubscribeToJobEvents();
				}
				else
				{
					this.watcherProcessManager.Stop();
					this.watchedFileStatusTracker.Stop();
					this.UnsubscribeFromJobEvents();
				}
			}
		}

		private bool runWhenClosed = RegistryUtilities.IsFileWatcherAutoStart();
		public bool RunWhenClosed
		{
			get { return this.runWhenClosed; }
			set
			{
				this.RaiseAndSetIfChanged(ref this.runWhenClosed, value);
				if (value)
				{
					RegistryUtilities.SetFileWatcherAutoStart(this.logger);
				}
				else
				{
					RegistryUtilities.RemoveFileWatcherAutoStart(this.logger);
				}
			}
		}

		public bool ShowQueued
		{
			get => this.GetStatusShown(WatchedFileStatusLive.Queued);
			set => this.SetStatusShown(WatchedFileStatusLive.Queued, value);
		}

		public bool ShowSucceeded
		{
			get => this.GetStatusShown(WatchedFileStatusLive.Succeeded);
			set => this.SetStatusShown(WatchedFileStatusLive.Succeeded, value);
		}

		public bool ShowFailed
		{
			get => this.GetStatusShown(WatchedFileStatusLive.Failed);
			set => this.SetStatusShown(WatchedFileStatusLive.Failed, value);
		}

		public bool ShowCanceled
		{
			get => this.GetStatusShown(WatchedFileStatusLive.Canceled);
			set => this.SetStatusShown(WatchedFileStatusLive.Canceled, value);
		}

		public bool ShowSkipped
		{
			get => this.GetStatusShown(WatchedFileStatusLive.Skipped);
			set => this.SetStatusShown(WatchedFileStatusLive.Skipped, value);
		}

		public bool ShowOutput
		{
			get => this.GetStatusShown(WatchedFileStatusLive.Output);
			set => this.SetStatusShown(WatchedFileStatusLive.Output, value);
		}

		private bool GetStatusShown(WatchedFileStatusLive status)
		{
			return this.statusFilters[status];
		}

		private void SetStatusShown(WatchedFileStatusLive status, bool shown)
		{
			DatabaseConfig.Set<bool>(StatusFilterConfigPrefix + status.ToString(), shown, Database.Connection);
			this.statusFilters[status] = shown;
			this.RefreshWatchedFilesFromFileMap();
		}

		private void PopulateStatusFilters()
		{
			foreach (WatchedFileStatusLive status in Enum.GetValues(typeof(WatchedFileStatusLive)))
			{
				this.statusFilters[status] = DatabaseConfig.Get<bool>(StatusFilterConfigPrefix + status.ToString(), true, Database.Connection);
			}
		}

		private ReactiveCommand<Unit, Unit> addWatchedFolder;
		public ICommand AddWatchedFolder
		{
			get
			{
				return this.addWatchedFolder ?? (this.addWatchedFolder = ReactiveCommand.Create(
					() =>
					{
						var newWatchedFolder = new WatchedFolder
						{
							Picker = string.Empty, // This is the default picker
							Preset = this.presetsService.AllPresets[0].Preset.Name
						};

						var addWatchedFolderViewModel = new WatcherEditDialogViewModel(
							newWatchedFolder,
							this.WatchedFolders.Items.Select(f => f.WatchedFolder.Path).ToList(),
							isAdd: true);
						this.windowManager.OpenDialog(addWatchedFolderViewModel, this);

						if (addWatchedFolderViewModel.DialogResult)
						{
							this.WatchedFolders.Add(new WatchedFolderViewModel(this, addWatchedFolderViewModel.WatchedFolder));
							this.SaveWatchedFolders();
						}
					}));
			}
		}

		public bool OnClosing()
		{
			if (this.WatcherEnabled)
			{
				this.UnsubscribeFromJobEvents();
			}

			return true;
		}

		public void EditFolder(WatchedFolderViewModel folderToEdit)
		{
			var editWatchedFolderViewModel = new WatcherEditDialogViewModel(
				folderToEdit.WatchedFolder.Clone(),
				this.WatchedFolders.Items.Select(f => f.WatchedFolder.Path).ToList(),
				isAdd: false);
			this.windowManager.OpenDialog(editWatchedFolderViewModel, this);

			if (editWatchedFolderViewModel.DialogResult)
			{
				this.WatchedFolders.Replace(folderToEdit, new WatchedFolderViewModel(this, editWatchedFolderViewModel.WatchedFolder));
				this.SaveWatchedFolders();
			}
		}

		public void RemoveFolder(WatchedFolderViewModel folderToRemove)
		{
			this.WatchedFolders.Remove(folderToRemove);
			this.SaveWatchedFolders();
		}

		private void SaveWatchedFolders()
		{ 
			WatcherStorage.SaveWatchedFolders(Database.Connection, this.WatchedFolders.Items.Select(f => f.WatchedFolder));
			if (this.WatcherEnabled)
			{
				this.watcherProcessManager.RefreshFromWatchedFolders();
			}
		}

		public void RefreshFiles()
		{
			Dictionary<string, WatchedFile> newFiles = WatcherStorage.GetWatchedFiles(Database.Connection);
			var viewModelList = new List<WatchedFileViewModel>(newFiles.Select(pair => new WatchedFileViewModel(this, pair.Value)));

			this.RebuildFileMap(viewModelList);
			this.InitializeLiveStatus();

			this.RefreshWatchedFilesFromFileMap();
		}

		/// <summary>
		/// Refreshes WatchedFiles from fileMap, applying any relevant filters.
		/// </summary>
		private void RefreshWatchedFilesFromFileMap()
		{
			this.WatchedFiles.Edit(watchedFilesInnerList =>
			{
				watchedFilesInnerList.Clear();
				foreach (WatchedFileViewModel fileViewModel in this.fileMap.Values)
				{
					if (this.GetStatusShown(fileViewModel.Status))
					{
						watchedFilesInnerList.Add(fileViewModel);
					}
				}
			});
		}

		public void RetrySelectedFiles()
		{
			var filesToQueue = new List<WatchedFile>();

			foreach (WatchedFileViewModel fileViewModel in this.WatchedFiles.Items)
			{
				if (fileViewModel.IsSelected && fileViewModel.CanRetry)
				{
					filesToQueue.Add(fileViewModel.WatchedFile);
				}
			}

			if (filesToQueue.Count > 0)
			{
				this.processingService.QueueWatchedFiles(filesToQueue);
			}
		}

		public void CancelSelectedFiles()
		{
			var pathSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			foreach (WatchedFileViewModel fileViewModel in this.WatchedFiles.Items)
			{
				if (fileViewModel.IsSelected && fileViewModel.CanCancel)
				{
					string filePath = fileViewModel.WatchedFile.Path;
					WatcherStorage.UpdateEntryStatus(Database.Connection, filePath, WatchedFileStatus.Canceled);
					pathSet.Add(filePath);
				}
			}

			if (pathSet.Count > 0)
			{
				this.processingService.RemoveJobsBySourcePath(pathSet);
			}
		}

		private void SubscribeToJobEvents()
		{
			this.processingService.JobQueued += this.OnJobQueued;
			this.processingService.JobQueueSkipped += this.OnJobQueueSkipped;
			this.processingService.JobRemovedFromQueue += this.OnJobRemovedFromQueue;
			this.processingService.JobStarted += this.OnJobStarted;
			this.processingService.JobCompleted += this.OnJobCompleted;
			this.processingService.JobsAddedFromWatcher += this.OnJobsAddedFromWatcher;
			this.processingService.WatchedFilesRemoved += this.OnWatchedFilesRemoved;
		}

		private void UnsubscribeFromJobEvents()
		{
			this.processingService.JobQueued -= this.OnJobQueued;
			this.processingService.JobQueueSkipped -= this.OnJobQueueSkipped;
			this.processingService.JobRemovedFromQueue -= this.OnJobRemovedFromQueue;
			this.processingService.JobStarted -= this.OnJobStarted;
			this.processingService.JobCompleted -= this.OnJobCompleted;
			this.processingService.JobsAddedFromWatcher -= this.OnJobsAddedFromWatcher;
			this.processingService.WatchedFilesRemoved -= this.OnWatchedFilesRemoved;
		}

		private void OnJobQueued(object sender, EventArgs<EncodeJobViewModel> e)
		{
			WatchedFileViewModel watchedFile = this.GetWatchedFileForJob(e.Value);
			if (watchedFile != null)
			{
				watchedFile.Status = WatchedFileStatusLive.Queued;
			}
		}

		private void OnJobQueueSkipped(object sender, EventArgs<string> e)
		{
			WatchedFileViewModel watchedFile = this.GetWatchedFileForSourcePath(e.Value);
			if (watchedFile != null)
			{
				watchedFile.Status = WatchedFileStatusLive.Skipped;
			}
		}

		private void OnJobRemovedFromQueue(object sender, EventArgs<EncodeJobViewModel> e)
		{
			WatchedFileViewModel watchedFile = this.GetWatchedFileForJob(e.Value);
			if (watchedFile != null)
			{
				watchedFile.Status = WatchedFileStatusLive.Canceled;
			}
		}

		private void OnJobStarted(object sender, EventArgs<EncodeJobViewModel> e)
		{
			WatchedFileViewModel watchedFile = this.GetWatchedFileForJob(e.Value);
			if (watchedFile != null)
			{
				watchedFile.Status = WatchedFileStatusLive.Encoding;
			}
		}

		private void OnJobCompleted(object sender, JobCompletedEventArgs e)
		{
			WatchedFileViewModel watchedFile = this.GetWatchedFileForJob(e.JobViewModel);
			if (watchedFile != null)
			{
				if (e.JobViewModel.CompleteReason == EncodeCompleteReason.Finished)
				{
					if (e.ResultStatus == EncodeResultStatus.Succeeded)
					{
						watchedFile.Status = WatchedFileStatusLive.Succeeded;
					}
					else if (e.ResultStatus == EncodeResultStatus.Failed)
					{
						watchedFile.Status = WatchedFileStatusLive.Failed;
					}
				}
				else
				{
					watchedFile.Status = WatchedFileStatusLive.Queued;
				}
			}
		}

		private void OnJobsAddedFromWatcher(object sender, EventArgs e)
		{
			this.RefreshFiles();
		}

		private void OnWatchedFilesRemoved(object sender, EventArgs e)
		{
			this.RefreshFiles();
		}

		private void RebuildFileMap(IEnumerable<WatchedFileViewModel> items)
		{
			this.fileMap.Clear();
			foreach (var watchedFileViewModel in items)
			{
				this.fileMap.Add(watchedFileViewModel.WatchedFile.Path, watchedFileViewModel);
			}
		}

		/// <summary>
		/// Gets the watched file that corresponds with the job, or null if there is no watched file for that job.
		/// </summary>
		/// <param name="job">The job to look up.</param>
		/// <returns>The watched file that corresponds to the job, or null if there is no watched file for that job.</returns>
		private WatchedFileViewModel GetWatchedFileForJob(EncodeJobViewModel job)
		{
			return this.GetWatchedFileForSourcePath(job.Job.SourcePath);
		}

		/// <summary>
		/// Gets the watched file that corresponds with the source path, or null if there is no watched file for that source path.
		/// </summary>
		/// <param name="sourcePath">The source path to look up.</param>
		/// <returns>The watched file for the source path, or null if there is no watched file for that source path.</returns>
		private WatchedFileViewModel GetWatchedFileForSourcePath(string sourcePath)
		{
			if (this.fileMap.TryGetValue(sourcePath, out WatchedFileViewModel watchedFile))
			{
				return watchedFile;
			}
			else
			{
				return null;
			}
		}

		/// <summary>
		/// Find the current live status of all the items. Update the status of any that are queued or encoding.
		/// </summary>
		private void InitializeLiveStatus()
		{
			// Run through the queue and see if there are any matches
			foreach (var job in this.processingService.EncodeQueue.Items)
			{
				if (this.fileMap.TryGetValue(job.Job.SourcePath, out WatchedFileViewModel watchedFile))
				{
					if (job.Encoding)
					{
						watchedFile.Status = WatchedFileStatusLive.Encoding;
					}
					else
					{
						watchedFile.Status = WatchedFileStatusLive.Queued;
					}
				}
			}
		}

		private static string GetStatusString(WatcherProcessStatus status)
		{
			switch (status)
			{
				case WatcherProcessStatus.Running:
					return EnumsRes.WatcherProcessStatus_Running;
				case WatcherProcessStatus.Starting:
					return EnumsRes.WatcherProcessStatus_Starting;
				case WatcherProcessStatus.Stopped:
					return EnumsRes.WatcherProcessStatus_Stopped;
				case WatcherProcessStatus.Disabled:
					return EnumsRes.WatcherProcessStatus_Disabled;
				default:
					return string.Empty;
			}
		}
	}
}
