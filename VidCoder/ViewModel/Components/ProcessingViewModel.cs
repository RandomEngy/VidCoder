using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Media;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shell;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;
using HandBrake.Interop;
using HandBrake.Interop.Model;
using HandBrake.Interop.SourceData;
using VidCoder.Messages;
using VidCoder.Model;
using VidCoder.Services;

namespace VidCoder.ViewModel.Components
{
	using System.Reflection;
	using HandBrake.Interop.EventArgs;
	using HandBrake.Interop.Model.Encoding;
	using Resources;

	/// <summary>
	/// Controls the queue and actual processing of encode jobs.
	/// </summary>
	public class ProcessingViewModel : ViewModelBase
	{
		public const int QueuedTabIndex = 0;
		public const int CompletedTabIndex = 1;

		private const double StopWarningThresholdMinutes = 5;

		private ILogger logger = Ioc.Container.GetInstance<ILogger>();
		private IProcessAutoPause autoPause = Ioc.Container.GetInstance<IProcessAutoPause>();
		private ISystemOperations systemOperations = Ioc.Container.GetInstance<ISystemOperations>();
		private MainViewModel main = Ioc.Container.GetInstance<MainViewModel>();
		private OutputPathViewModel outputVM = Ioc.Container.GetInstance<OutputPathViewModel>();
		private PresetsViewModel presetsViewModel = Ioc.Container.GetInstance<PresetsViewModel>();

		private ObservableCollection<EncodeJobViewModel> encodeQueue;
		private bool encoding;
		private bool paused;
		private bool encodeStopped;
		private int totalTasks;
		private int taskNumber;
		private bool encodeSpeedDetailsAvailable;
		private Stopwatch elapsedQueueEncodeTime;
		private long pollCount = 0;
		private string estimatedTimeRemaining;
		private double currentFps;
		private double averageFps;
		private double completedQueueWork;
		private double totalQueueCost;
		private double overallEncodeProgressFraction;
		private TimeSpan overallEtaSpan;
		private TimeSpan currentJobEta; // Kept around to check if the job finished early
		private TaskbarItemProgressState encodeProgressState;
		private ObservableCollection<EncodeResultViewModel> completedJobs;
		private List<EncodeCompleteAction> encodeCompleteActions; 
		private EncodeCompleteAction encodeCompleteAction;
		private IEncodeProxy encodeProxy;

		private int selectedTabIndex;

		public ProcessingViewModel()
		{
			this.encodeQueue = new ObservableCollection<EncodeJobViewModel>();
			this.encodeQueue.CollectionChanged += (sender, e) => { this.SaveEncodeQueue(); };
			EncodeJobPersistGroup jobPersistGroup = EncodeJobsPersist.EncodeJobs;
			foreach (EncodeJobWithMetadata job in jobPersistGroup.EncodeJobs)
			{
				this.encodeQueue.Add(new EncodeJobViewModel(job.Job) { SourceParentFolder = job.SourceParentFolder, ManualOutputPath = job.ManualOutputPath, NameFormatOverride = job.NameFormatOverride, PresetName = job.PresetName});
			}

			this.autoPause.PauseEncoding += this.AutoPauseEncoding;
			this.autoPause.ResumeEncoding += this.AutoResumeEncoding;

			this.encodeQueue.CollectionChanged +=
				(o, e) =>
					{
						if (e.Action != NotifyCollectionChangedAction.Replace && e.Action != NotifyCollectionChangedAction.Move)
						{
							this.RefreshEncodeCompleteActions();
						}

						this.EncodeCommand.RaiseCanExecuteChanged();
						this.RaisePropertyChanged(() => this.QueueHasItems);
					};

			Messenger.Default.Register<VideoSourceChangedMessage>(
				this,
				message =>
					{
						RefreshCanEnqueue();
						this.EncodeCommand.RaiseCanExecuteChanged();
					});

			Messenger.Default.Register<OutputPathChangedMessage>(
				this,
				message =>
					{
						RefreshCanEnqueue();
						this.EncodeCommand.RaiseCanExecuteChanged();
					});

			Messenger.Default.Register<SelectedTitleChangedMessage>(
				this,
				message =>
					{
						this.QueueTitlesCommand.RaiseCanExecuteChanged();
					});

			this.completedJobs = new ObservableCollection<EncodeResultViewModel>();
			this.completedJobs.CollectionChanged +=
				(o, e) =>
				{
					if (e.Action != NotifyCollectionChangedAction.Replace && e.Action != NotifyCollectionChangedAction.Move)
					{
						this.RefreshEncodeCompleteActions();
					}
				};

			this.RefreshEncodeCompleteActions();

			if (Config.ResumeEncodingOnRestart && this.encodeQueue.Count > 0)
			{
				DispatchService.BeginInvoke(() =>
					{
						this.StartEncodeQueue();
					});
			}
		}

		public ObservableCollection<EncodeJobViewModel> EncodeQueue
		{
			get
			{
				return this.encodeQueue;
			}
		}

		public EncodeJobViewModel CurrentJob
		{
			get
			{
				return this.EncodeQueue[0];
			}
		}

		public IEncodeProxy EncodeProxy
		{
			get
			{
				return this.encodeProxy;
			}
		}

		public ObservableCollection<EncodeResultViewModel> CompletedJobs
		{
			get
			{
				return this.completedJobs;
			}
		}

		public int CompletedItemsCount
		{
			get
			{
				return this.completedJobs.Count();
			}
		}

		public bool CanTryEnqueue
		{
			get
			{
				return this.main.HasVideoSource;
			}
		}

		public bool Encoding
		{
			get
			{
				return this.encoding;
			}

			set
			{
				this.encoding = value;

				if (value)
				{
					SystemSleepManagement.PreventSleep();
					this.elapsedQueueEncodeTime = Stopwatch.StartNew();
				}
				else
				{
					this.EncodeSpeedDetailsAvailable = false;
					SystemSleepManagement.AllowSleep();
					this.elapsedQueueEncodeTime.Stop();
				}

				this.PauseCommand.RaiseCanExecuteChanged();
				this.RaisePropertyChanged(() => this.PauseVisible);
				this.RaisePropertyChanged(() => this.Encoding);
				this.RaisePropertyChanged(() => this.EncodeButtonText);
				this.main.WindowManagerVM.RefreshEncoding();

				if (!value)
				{
					Messenger.Default.Send(new ProgressChangedMessage { Encoding = false });
				}
			}
		}

		public bool QueueHasItems
		{
			get
			{
				return this.EncodeQueue.Count > 0;
			}
		}

		public bool Paused
		{
			get
			{
				return this.paused;
			}

			set
			{
				this.paused = value;

				if (this.elapsedQueueEncodeTime != null)
				{
					if (value)
					{
						this.elapsedQueueEncodeTime.Stop();
					}
					else
					{
						this.elapsedQueueEncodeTime.Start();
					}
				}

				this.RaisePropertyChanged(() => this.PauseVisible);
				this.RaisePropertyChanged(() => this.ProgressBarColor);
				this.RaisePropertyChanged(() => this.Paused);
			}
		}

		public string EncodeButtonText
		{
			get
			{
				if (this.Encoding)
				{
					return MainRes.Resume;
				}
				else
				{
					return MainRes.Encode;
				}
			}
		}

		public bool PauseVisible
		{
			get
			{
				return this.Encoding && !this.Paused;
			}
		}

		public string QueuedTabHeader
		{
			get
			{
				if (this.EncodeQueue.Count == 0)
				{
					return MainRes.Queued;
				}

				return string.Format(MainRes.QueuedWithTotal, this.EncodeQueue.Count);
			}
		}

		public string CompletedTabHeader
		{
			get
			{
				return string.Format(MainRes.CompletedWithTotal, this.CompletedJobs.Count);
			}
		}

		public bool CanTryEnqueueMultipleTitles
		{
			get
			{
				return this.main.HasVideoSource && this.main.SourceData.Titles.Count > 1;
			}
		}

		public bool EncodeSpeedDetailsAvailable
		{
			get
			{
				return this.encodeSpeedDetailsAvailable;
			}

			set
			{
				this.encodeSpeedDetailsAvailable = value;
				this.RaisePropertyChanged(() => this.EncodeSpeedDetailsAvailable);
			}
		}

		public string EstimatedTimeRemaining
		{
			get
			{
				return this.estimatedTimeRemaining;
			}

			set
			{
				this.estimatedTimeRemaining = value;
				this.RaisePropertyChanged(() => this.EstimatedTimeRemaining);
			}
		}

		public List<EncodeCompleteAction> EncodeCompleteActions
		{
			get
			{
				return this.encodeCompleteActions;
			}
		} 

		public EncodeCompleteAction EncodeCompleteAction
		{
			get
			{
				return this.encodeCompleteAction;
			}

			set
			{
				this.encodeCompleteAction = value;
				this.RaisePropertyChanged(() => this.EncodeCompleteAction);
			}
		}

		public double CurrentFps
		{
			get
			{
				return this.currentFps;
			}

			set
			{
				this.currentFps = value;
				this.RaisePropertyChanged(() => this.CurrentFps);
			}
		}

		public double AverageFps
		{
			get
			{
				return this.averageFps;
			}

			set
			{
				this.averageFps = value;
				this.RaisePropertyChanged(() => this.AverageFps);
			}
		}

		public double OverallEncodeProgressFraction
		{
			get
			{
				return this.overallEncodeProgressFraction;
			}

			set
			{
				this.overallEncodeProgressFraction = value;
				this.RaisePropertyChanged(() => this.OverallEncodeProgressPercent);
				this.RaisePropertyChanged(() => this.OverallEncodeProgressFraction);
			}
		}

		public double OverallEncodeProgressPercent
		{
			get
			{
				return this.overallEncodeProgressFraction * 100;
			}
		}

		public TaskbarItemProgressState EncodeProgressState
		{
			get
			{
				return this.encodeProgressState;
			}

			set
			{
				this.encodeProgressState = value;
				this.RaisePropertyChanged(() => this.EncodeProgressState);
			}
		}

		public Brush ProgressBarColor
		{
			get
			{
				if (this.Paused)
				{
					return new SolidColorBrush(Color.FromRgb(255, 230, 0));
				}
				else
				{
					return new SolidColorBrush(Color.FromRgb(0, 200, 0));
				}
			}
		}

		public int SelectedTabIndex
		{
			get
			{
				return this.selectedTabIndex;
			}

			set
			{
				this.selectedTabIndex = value;
				this.RaisePropertyChanged(() => this.SelectedTabIndex);
			}
		}

		private RelayCommand encodeCommand;
		public RelayCommand EncodeCommand
		{
			get
			{
				return this.encodeCommand ?? (this.encodeCommand = new RelayCommand(() =>
					{
						if (this.Encoding)
						{
							this.ResumeEncoding();
							this.autoPause.ReportResume();
						}
						else
						{
							if (this.EncodeQueue.Count == 0)
							{
								if (!this.TryQueue())
								{
									return;
								}
							}

							this.SelectedTabIndex = QueuedTabIndex;

							this.StartEncodeQueue();
						}
					},
					() =>
					{
						return this.EncodeQueue.Count > 0 || this.CanTryEnqueue;
					}));
			}
		}

		private RelayCommand addToQueueCommand;
		public RelayCommand AddToQueueCommand
		{
			get
			{
				return this.addToQueueCommand ?? (this.addToQueueCommand = new RelayCommand(() =>
					{
						this.TryQueue();
					},
					() =>
					{
						return this.CanTryEnqueue;
					}));
			}
		}

		private RelayCommand queueFilesCommand;
		public RelayCommand QueueFilesCommand
		{
			get
			{
				return this.queueFilesCommand ?? (this.queueFilesCommand = new RelayCommand(() =>
					{
						if (!this.EnsureDefaultOutputFolderSet())
						{
							return;
						}

						IList<string> fileNames = FileService.Instance.GetFileNames(Config.RememberPreviousFiles ? Config.LastInputFileFolder : null);
						if (fileNames != null && fileNames.Count > 0)
						{
							Config.LastInputFileFolder = Path.GetDirectoryName(fileNames[0]);

							this.QueueMultiple(fileNames);
						}
					}));
			}
		}

		private RelayCommand queueTitlesCommand;
		public RelayCommand QueueTitlesCommand
		{
			get
			{
				return this.queueTitlesCommand ?? (this.queueTitlesCommand = new RelayCommand(() =>
					{
						if (!this.EnsureDefaultOutputFolderSet())
						{
							return;
						}

						var queueTitlesDialog = new QueueTitlesDialogViewModel(this.main.SourceData.Titles);
						WindowManager.OpenDialog(queueTitlesDialog, this.main);

						if (queueTitlesDialog.DialogResult)
						{
							int currentTitleNumber = queueTitlesDialog.TitleStartOverride;

							// Queue the selected titles
							List<Title> titlesToQueue = queueTitlesDialog.CheckedTitles;
							foreach (Title title in titlesToQueue)
							{
								VCProfile profile = this.presetsViewModel.SelectedPreset.Preset.EncodingProfile;
								string queueSourceName = this.main.SourceName;
								if (this.main.SelectedSource.Type == SourceType.Dvd)
								{
									queueSourceName = this.outputVM.TranslateDvdSourceName(queueSourceName);
								}

								int titleNumber = title.TitleNumber;
								if (queueTitlesDialog.TitleStartOverrideEnabled)
								{
									titleNumber = currentTitleNumber;
									currentTitleNumber++;
								}

								string nameFormatOverride = null;
								if (queueTitlesDialog.NameOverrideEnabled)
								{
									nameFormatOverride = queueTitlesDialog.NameOverride;
								}

								var job = new VCJob
								{
									SourceType = this.main.SelectedSource.Type,
									SourcePath = this.main.SourcePath,
									EncodingProfile = profile.Clone(),
									Title = title.TitleNumber,
									ChapterStart = 1,
									ChapterEnd = title.Chapters.Count,
									UseDefaultChapterNames = true,
									Length = title.Duration
								};

								this.AutoPickAudio(job, title, useCurrentContext: true);
								this.AutoPickSubtitles(job, title, useCurrentContext: true);

								string queueOutputFileName = this.outputVM.BuildOutputFileName(
									this.main.SourcePath,
									queueSourceName,
									titleNumber,
									title.Duration,
									title.Chapters.Count,
									nameFormatOverride);

								string extension = this.outputVM.GetOutputExtension();
								string queueOutputPath = this.outputVM.BuildOutputPath(queueOutputFileName, extension, sourcePath: null);

								job.OutputPath = this.outputVM.ResolveOutputPathConflicts(queueOutputPath, isBatch: true);

								var jobVM = new EncodeJobViewModel(job)
								{
								    HandBrakeInstance = this.main.ScanInstance,
								    VideoSource = this.main.SourceData,
								    VideoSourceMetadata = this.main.GetVideoSourceMetadata(),
								    ManualOutputPath = false,
								    NameFormatOverride = nameFormatOverride,
									PresetName = this.presetsViewModel.SelectedPreset.DisplayName
								};

								this.Queue(jobVM);
							}
						}
					},
					() =>
					{
						return this.CanTryEnqueueMultipleTitles;
					}));
			}
		}

		private RelayCommand pauseCommand;
		public RelayCommand PauseCommand
		{
			get
			{
				return this.pauseCommand ?? (this.pauseCommand = new RelayCommand(() =>
					{
						this.PauseEncoding();
						this.autoPause.ReportPause();
					},
					() =>
					{
						return this.Encoding && this.encodeProxy != null &&  this.encodeProxy.IsEncodeStarted;
					}));
			}
		}

		private RelayCommand stopEncodeCommand;
		public RelayCommand StopEncodeCommand
		{
			get
			{
				return this.stopEncodeCommand ?? (this.stopEncodeCommand = new RelayCommand(() =>
					{
						if (this.CurrentJob.EncodeTime > TimeSpan.FromMinutes(StopWarningThresholdMinutes))
						{
							MessageBoxResult dialogResult = Utilities.MessageBox.Show(
								MainRes.StopEncodeConfirmationMessage,
								MainRes.StopEncodeConfirmationTitle,
								MessageBoxButton.YesNo);
							if (dialogResult == MessageBoxResult.No)
							{
								return;
							}
						}

						// Signify that we stopped the encode manually rather than it completing.
						this.encodeStopped = true;
						this.encodeProxy.StopEncode();

						this.logger.ShowStatus(MainRes.StoppedEncoding);
					},
					() =>
					{
						return this.Encoding && this.encodeProxy != null && this.encodeProxy.IsEncodeStarted;
					}));
			}
		}

		private RelayCommand moveSelectedJobsToTopCommand;
		public RelayCommand MoveSelectedJobsToTopCommand
		{
			get
			{
				return this.moveSelectedJobsToTopCommand ?? (this.moveSelectedJobsToTopCommand = new RelayCommand(() =>
					{
						List<EncodeJobViewModel> jobsToMove = this.EncodeQueue.Where(j => j.IsSelected && !j.Encoding).ToList();
						if (jobsToMove.Count > 0)
						{
							foreach (EncodeJobViewModel jobToMove in jobsToMove)
							{
								this.EncodeQueue.Remove(jobToMove);
							}

							int insertPosition = this.Encoding ? 1 : 0;

							for (int i = jobsToMove.Count - 1; i >= 0; i--)
							{
								this.EncodeQueue.Insert(insertPosition, jobsToMove[i]);
							}
						}
					}));
			}
		}

		private RelayCommand moveSelectedJobsToBottomCommand;
		public RelayCommand MoveSelectedJobsToBottomCommand
		{
			get
			{
				return this.moveSelectedJobsToBottomCommand ?? (this.moveSelectedJobsToBottomCommand = new RelayCommand(() =>
					{
						List<EncodeJobViewModel> jobsToMove = this.EncodeQueue.Where(j => j.IsSelected && !j.Encoding).ToList();
						if (jobsToMove.Count > 0)
						{
							foreach (EncodeJobViewModel jobToMove in jobsToMove)
							{
								this.EncodeQueue.Remove(jobToMove);
							}

							foreach (EncodeJobViewModel jobToMove in jobsToMove)
							{
								this.EncodeQueue.Add(jobToMove);
							}
						}
					}));
			}
		}

		private RelayCommand importQueueCommand;
		public RelayCommand ImportQueueCommand
		{
			get
			{
				return this.importQueueCommand ?? (this.importQueueCommand = new RelayCommand(() =>
				{
					string presetFileName = FileService.Instance.GetFileNameLoad(null, MainRes.ImportQueueFilePickerTitle, "xml", Utilities.GetFilePickerFilter("xml"));
					if (presetFileName != null)
					{
						Ioc.Container.GetInstance<IQueueImportExport>().Import(presetFileName);
					}

				}));
			}
		}

		private RelayCommand exportQueueCommand;
		public RelayCommand ExportQueueCommand
		{
			get
			{
				return this.exportQueueCommand ?? (this.exportQueueCommand = new RelayCommand(() =>
					{
						var encodeJobs = new List<EncodeJobWithMetadata>();
						foreach (EncodeJobViewModel jobVM in this.EncodeQueue)
						{
							encodeJobs.Add(
								new EncodeJobWithMetadata
								{
									Job = jobVM.Job,
									SourceParentFolder = jobVM.SourceParentFolder,
									ManualOutputPath = jobVM.ManualOutputPath,
									NameFormatOverride = jobVM.NameFormatOverride,
									PresetName = jobVM.PresetName
								});
						}

						Ioc.Container.GetInstance<IQueueImportExport>().Export(encodeJobs);
				}));
			}
		}

		private RelayCommand removeSelectedJobsCommand;
		public RelayCommand RemoveSelectedJobsCommand
		{
			get
			{
				return this.removeSelectedJobsCommand ?? (this.removeSelectedJobsCommand = new RelayCommand(() =>
					{
						this.RemoveSelectedQueueJobs();
					}));
			}
		}

		private RelayCommand clearCompletedCommand;
		public RelayCommand ClearCompletedCommand
		{
			get
			{
				return this.clearCompletedCommand ?? (this.clearCompletedCommand = new RelayCommand(() =>
					{
						var removedItems = new List<EncodeResultViewModel>(this.CompletedJobs);
						this.CompletedJobs.Clear();
						var deletionCandidates = new List<string>();

						foreach (var removedItem in removedItems)
						{
							if (removedItem.Job.HandBrakeInstance != null)
							{
								this.CleanupHandBrakeInstanceIfUnused(removedItem.Job.HandBrakeInstance);
							}

							// Delete file if setting is enabled and item succeeded
							if (Config.DeleteSourceFilesOnClearingCompleted && removedItem.EncodeResult.Succeeded)
							{
								// And if file exists and is not read-only
								string sourcePath = removedItem.Job.Job.SourcePath;
								var fileInfo = new FileInfo(sourcePath);
								var directoryInfo = new DirectoryInfo(sourcePath);

								if (fileInfo.Exists && !fileInfo.IsReadOnly || directoryInfo.Exists && !directoryInfo.Attributes.HasFlag(FileAttributes.ReadOnly))
								{
									// And if it's not currently scanned or in the encode queue
									bool sourceInEncodeQueue = this.EncodeQueue.Any(job => string.Compare(job.Job.SourcePath, sourcePath, StringComparison.OrdinalIgnoreCase) == 0);
									if (!sourceInEncodeQueue &&
									    (!this.main.HasVideoSource || string.Compare(this.main.SourcePath, sourcePath, StringComparison.OrdinalIgnoreCase) != 0))
									{
										deletionCandidates.Add(sourcePath);
									}
								}
							}
						}

						if (deletionCandidates.Count > 0)
						{
							MessageBoxResult dialogResult = Utilities.MessageBox.Show(
								string.Format(MainRes.DeleteSourceFilesConfirmationMessage, deletionCandidates.Count), 
								MainRes.DeleteSourceFilesConfirmationTitle, 
								MessageBoxButton.YesNo);
							if (dialogResult == MessageBoxResult.Yes)
							{
								foreach (string pathToDelete in deletionCandidates)
								{
									try
									{
										if (File.Exists(pathToDelete))
										{
											File.Delete(pathToDelete);
										}
										else if (Directory.Exists(pathToDelete))
										{
											Utilities.DeleteDirectory(pathToDelete);
										}
									}
									catch (IOException exception)
									{
										Utilities.MessageBox.Show(string.Format(MainRes.CouldNotDeleteFile, pathToDelete, exception));
									}
								}
							}
						}

						this.RaisePropertyChanged(() => this.CompletedItemsCount);
						this.RaisePropertyChanged(() => this.CompletedTabHeader);
					}));
			}
		}

		/// <summary>
		/// Adds the given source to the encode queue.
		/// </summary>
		/// <param name="source">The path to the source file to encode.</param>
		/// <param name="destination">The destination path for the encoded file.</param>
		/// <param name="presetName">The name of the preset to use to encode.</param>
		/// <returns>True if the item was successfully queued for processing.</returns>
		public void Process(string source, string destination, string presetName)
		{
			if (string.IsNullOrWhiteSpace(source))
			{
				throw new ArgumentException("source cannot be null or empty.");
			}

			if (string.IsNullOrWhiteSpace(destination) && !this.EnsureDefaultOutputFolderSet())
			{
				throw new ArgumentException("If destination is not set, the default output folder must be set.");
			}

			if (destination != null && !Utilities.IsValidFullPath(destination))
			{
				throw new ArgumentException("Destination path is not valid: " + destination);
			}

			VCProfile profile = this.presetsViewModel.GetProfileByName(presetName);
			if (profile == null)
			{
				throw new ArgumentException("Cannot find preset: " + presetName);
			}

			var jobVM = new EncodeJobViewModel(new VCJob
				{
					SourcePath = source,
					SourceType = Utilities.GetSourceType(source),
					Title = 1,
					RangeType = VideoRangeType.All,
					EncodingProfile = profile,
					ChosenAudioTracks = new List<int> { 1 },
					OutputPath = destination
				});

			jobVM.PresetName = presetName;
			jobVM.ManualOutputPath = !string.IsNullOrWhiteSpace(destination);

			var scanMultipleDialog = new ScanMultipleDialogViewModel(new List<EncodeJobViewModel>{ jobVM });
			WindowManager.OpenDialog(scanMultipleDialog, this.main);

			VCJob job = jobVM.Job;

			Title title = jobVM.HandBrakeInstance.Titles.SingleOrDefault(t => t.TitleNumber == job.Title);
			if (title == null)
			{
				title = jobVM.HandBrakeInstance.Titles[0];
			}

			// Choose the correct audio/subtitle tracks based on settings
			this.AutoPickAudio(job, title);
			this.AutoPickSubtitles(job, title);

			// Now that we have the title and subtitles we can determine the final output file name
			if (string.IsNullOrWhiteSpace(destination))
			{
				// Exclude all current queued files if overwrite is disabled
				HashSet<string> excludedPaths;
				if (CustomConfig.WhenFileExistsBatch == WhenFileExists.AutoRename)
				{
					excludedPaths = this.GetQueuedFiles();
				}
				else
				{
					excludedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
				}

				string pathToQueue = job.SourcePath;

				excludedPaths.Add(pathToQueue);
				string outputFolder = this.outputVM.GetOutputFolder(pathToQueue);
				string outputFileName = this.outputVM.BuildOutputFileName(pathToQueue, Utilities.GetSourceName(pathToQueue), job.Title, title.Duration, title.Chapters.Count, usesScan: false);
				string outputExtension = this.outputVM.GetOutputExtension();
				string queueOutputPath = Path.Combine(outputFolder, outputFileName + outputExtension);
				queueOutputPath = this.outputVM.ResolveOutputPathConflicts(queueOutputPath, excludedPaths, isBatch: true);

				job.OutputPath = queueOutputPath;
			}

			this.Queue(jobVM);

			if (!this.Encoding)
			{
				this.StartEncodeQueue();
			}
		}

		public bool TryQueue()
		{
			if (!this.EnsureDefaultOutputFolderSet())
			{
				return false;
			}

			if (!this.EnsureValidOutputPath())
			{
				return false;
			}

			var newEncodeJobVM = this.main.CreateEncodeJobVM();

			string resolvedOutputPath = this.outputVM.ResolveOutputPathConflicts(newEncodeJobVM.Job.OutputPath, isBatch: false);
			if (resolvedOutputPath == null)
			{
				return false;
			}

			newEncodeJobVM.Job.OutputPath = resolvedOutputPath;

			this.Queue(newEncodeJobVM);
			return true;
		}

		/// <summary>
		/// Queues the given Job. Assumed that the job has an associated HandBrake instance and populated Length.
		/// </summary>
		/// <param name="encodeJobVM">The job to add.</param>
		public void Queue(EncodeJobViewModel encodeJobVM)
		{
			if (this.Encoding)
			{
				if (this.totalTasks == 1)
				{
					this.EncodeQueue[0].IsOnlyItem = false;
				}

				this.totalTasks++;
				this.totalQueueCost += encodeJobVM.Cost;
			}

			this.EncodeQueue.Add(encodeJobVM);

			this.RaisePropertyChanged(() => this.QueuedTabHeader);

			// Select the Queued tab.
			if (this.SelectedTabIndex != QueuedTabIndex)
			{
				this.SelectedTabIndex = QueuedTabIndex;
			}
		}

		public void QueueMultiple(IEnumerable<string> pathsToQueue)
		{
			this.QueueMultiple(pathsToQueue.Select(p => new SourcePath { Path = p }));
		}

		// Queues a list of files or video folders.
		public void QueueMultiple(IEnumerable<SourcePath> sourcePaths)
		{
			if (!this.EnsureDefaultOutputFolderSet())
			{
				return;
			}

			// Exclude all current queued files if overwrite is disabled
			HashSet<string> excludedPaths;
			if (CustomConfig.WhenFileExistsBatch == WhenFileExists.AutoRename)
			{
				excludedPaths = this.GetQueuedFiles();
			}
			else
			{
				excludedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			}

			var itemsToQueue = new List<EncodeJobViewModel>();
			foreach (SourcePath sourcePath in sourcePaths)
			{
				var job = new VCJob
				{
					SourcePath = sourcePath.Path,
					EncodingProfile = this.presetsViewModel.SelectedPreset.Preset.EncodingProfile.Clone(),
					Title = 1,
					RangeType = VideoRangeType.All,
					UseDefaultChapterNames = true
				};

				if (sourcePath.SourceType == SourceType.None)
				{
					if (Directory.Exists(sourcePath.Path))
					{
						job.SourceType = SourceType.VideoFolder;
					}
					else if (File.Exists(sourcePath.Path))
					{
						job.SourceType = SourceType.File;
					}
				}
				else
				{
					job.SourceType = sourcePath.SourceType;
				}

				if (job.SourceType != SourceType.None)
				{
					var jobVM = new EncodeJobViewModel(job);
					jobVM.SourceParentFolder = sourcePath.ParentFolder;
					jobVM.ManualOutputPath = false;
					jobVM.PresetName = this.presetsViewModel.SelectedPreset.DisplayName;
					itemsToQueue.Add(jobVM);
				}
			}

			// This dialog will scan the items in the list, calculating length.
			var scanMultipleDialog = new ScanMultipleDialogViewModel(itemsToQueue);
			WindowManager.OpenDialog(scanMultipleDialog, this.main);

			var failedFiles = new List<string>();
			foreach (EncodeJobViewModel jobVM in itemsToQueue)
			{
				// Skip over any cancelled jobs
				if (jobVM.HandBrakeInstance == null)
				{
					continue;
				}

				// Only queue items with a successful scan
				if (jobVM.HandBrakeInstance.Titles.Count > 0)
				{
					VCJob job = jobVM.Job;
					Title title = jobVM.HandBrakeInstance.Titles.SingleOrDefault(t => t.TitleNumber == job.Title);
					if (title == null)
					{
						title = jobVM.HandBrakeInstance.Titles[0];
					}

					// Choose the correct audio/subtitle tracks based on settings
					this.AutoPickAudio(job, title);
					this.AutoPickSubtitles(job, title);

					// Now that we have the title and subtitles we can determine the final output file name
					string fileToQueue = job.SourcePath;

					excludedPaths.Add(fileToQueue);
					string outputFolder = this.outputVM.GetOutputFolder(fileToQueue, jobVM.SourceParentFolder);
					string outputFileName = this.outputVM.BuildOutputFileName(fileToQueue, Utilities.GetSourceNameFile(fileToQueue), job.Title, title.Duration, title.Chapters.Count, usesScan: false);
					string outputExtension = this.outputVM.GetOutputExtension();
					string queueOutputPath = Path.Combine(outputFolder, outputFileName + outputExtension);
					queueOutputPath = this.outputVM.ResolveOutputPathConflicts(queueOutputPath, excludedPaths, isBatch: true);

					job.OutputPath = queueOutputPath;

					excludedPaths.Add(queueOutputPath);

					this.Queue(jobVM);
				}
				else
				{
					failedFiles.Add(jobVM.Job.SourcePath);
				}
			}

			if (failedFiles.Count > 0)
			{
				Utilities.MessageBox.Show(
					string.Format(MainRes.QueueMultipleScanErrorMessage, string.Join(Environment.NewLine, failedFiles)),
					MainRes.QueueMultipleScanErrorTitle,
					MessageBoxButton.OK,
					MessageBoxImage.Warning);
			}
		}

		public void RemoveQueueJob(EncodeJobViewModel job)
		{
			this.EncodeQueue.Remove(job);

			if (this.Encoding)
			{
				this.totalTasks--;
				this.totalQueueCost -= job.Cost;

				if (this.totalTasks == 1)
				{
					this.EncodeQueue[0].IsOnlyItem = true;
				}
			}

			this.RaisePropertyChanged(() => this.QueuedTabHeader);
		}

		public void RemoveSelectedQueueJobs()
		{
			for (int i = this.EncodeQueue.Count - 1; i >= 0; i--)
			{
				EncodeJobViewModel jobVM = this.EncodeQueue[i];

				if (jobVM.IsSelected && !jobVM.Encoding)
				{
					this.EncodeQueue.RemoveAt(i);
				}
			}
		}

		public void StartEncodeQueue()
		{
			this.EncodeProgressState = TaskbarItemProgressState.Normal;
			this.logger.Log("Starting queue");
			this.logger.ShowStatus(MainRes.StartedEncoding);

			this.totalTasks = this.EncodeQueue.Count;
			this.taskNumber = 0;

			this.completedQueueWork = 0.0;
			this.totalQueueCost = 0.0;
			foreach (EncodeJobViewModel jobVM in this.EncodeQueue)
			{
				this.totalQueueCost += jobVM.Cost;
			}

			this.OverallEncodeProgressFraction = 0;

			this.pollCount = 0;
			this.Encoding = true;
			this.Paused = false;
			this.encodeStopped = false;
			this.autoPause.ReportStart();

			this.EncodeNextJob();

			// User had the window open when the encode ended last time, so we re-open when starting the queue again.
			if (Config.EncodeDetailsWindowOpen)
			{
				this.main.WindowManagerVM.OpenEncodeDetailsWindow();
			}

			Messenger.Default.Send(new ProgressChangedMessage
			{
				Encoding = true,
				OverallProgressFraction = 0,
				TaskNumber = 1,
				TotalTasks = this.totalTasks,
				FileName = Path.GetFileName(this.CurrentJob.Job.OutputPath)
			});
		}

		public HashSet<string> GetQueuedFiles()
		{
			return new HashSet<string>(this.EncodeQueue.Select(j => j.Job.OutputPath), StringComparer.OrdinalIgnoreCase);
		}

		/// <summary>
		/// Cleans up the given HandBrake instance if it's not being used anymore.
		/// </summary>
		/// <param name="instance">The instance to clean up.</param>
		public void CleanupHandBrakeInstanceIfUnused(HandBrakeInstance instance)
		{
			foreach (EncodeJobViewModel encodeJobVM in this.EncodeQueue)
			{
				if (instance == encodeJobVM.HandBrakeInstance)
				{
					return;
				}
			}

			foreach (EncodeResultViewModel resultVM in this.CompletedJobs)
			{
				if (instance == resultVM.Job.HandBrakeInstance)
				{
					return;
				}
			}

			if (instance == this.main.ScanInstance)
			{
				return;
			}

			instance.Dispose();
		}

		/// <summary>
		/// Cleans up all HandBrakeInstance objects it can find around the app.
		/// </summary>
		public void CleanupHandBrakeInstances()
		{
			var instances = new List<HandBrakeInstance>();

			foreach (EncodeJobViewModel encodeJobVM in this.EncodeQueue)
			{
				if (encodeJobVM.HandBrakeInstance != null)
				{
					instances.Add(encodeJobVM.HandBrakeInstance);
				}
			}

			foreach (EncodeResultViewModel resultVM in this.CompletedJobs)
			{
				if (resultVM.Job.HandBrakeInstance != null)
				{
					instances.Add(resultVM.Job.HandBrakeInstance);
				}
			}

			if (this.main.ScanInstance != null)
			{
				instances.Add(this.main.ScanInstance);
			}

			foreach (HandBrakeInstance instance in instances.Distinct())
			{
				instance.Dispose();
			}
		}

		public EncodeJobPersistGroup GetQueuePersistGroup()
		{
			var jobPersistGroup = new EncodeJobPersistGroup();
			foreach (EncodeJobViewModel jobVM in this.EncodeQueue)
			{
				jobPersistGroup.EncodeJobs.Add(
					new EncodeJobWithMetadata
					{
						Job = jobVM.Job,
						SourceParentFolder = jobVM.SourceParentFolder,
						ManualOutputPath = jobVM.ManualOutputPath,
						NameFormatOverride = jobVM.NameFormatOverride,
						PresetName = jobVM.PresetName
					});
			}

			return jobPersistGroup;
		}

		private void EncodeNextJob()
		{
			this.taskNumber++;
			this.StartEncode();
		}

		private void StartEncode()
		{
			VCJob job = this.CurrentJob.Job;

			var encodeLogger = new Logger(this.logger, Path.GetFileName(job.OutputPath));
			this.CurrentJob.Logger = encodeLogger;

			encodeLogger.Log("Starting job " + this.taskNumber + "/" + this.totalTasks);
			encodeLogger.Log("  Path: " + job.SourcePath);
			encodeLogger.Log("  Title: " + job.Title);

			switch (job.RangeType)
			{
				case VideoRangeType.All:
					encodeLogger.Log("  Range: All");
					break;
				case VideoRangeType.Chapters:
					encodeLogger.Log("  Chapters: " + job.ChapterStart + "-" + job.ChapterEnd);
					break;
				case VideoRangeType.Seconds:
					encodeLogger.Log("  Seconds: " + job.SecondsStart + "-" + job.SecondsEnd);
					break;
				case VideoRangeType.Frames:
					encodeLogger.Log("  Frames: " + job.FramesStart + "-" + job.FramesEnd);
					break;
			}

			this.encodeProxy = Utilities.CreateEncodeProxy();
			this.encodeProxy.EncodeProgress += this.OnEncodeProgress;
			this.encodeProxy.EncodeCompleted += this.OnEncodeCompleted;
			this.encodeProxy.EncodeStarted += this.OnEncodeStarted;

			string destinationDirectory = Path.GetDirectoryName(this.CurrentJob.Job.OutputPath);
			if (!Directory.Exists(destinationDirectory))
			{
				try
				{
					Directory.CreateDirectory(destinationDirectory);
				}
				catch (IOException exception)
				{
					Utilities.MessageBox.Show(
						string.Format(MainRes.DirectoryCreateErrorMessage, exception),
						MainRes.DirectoryCreateErrorTitle,
						MessageBoxButton.OK,
						MessageBoxImage.Error);
				}
			}

			this.currentJobEta = TimeSpan.Zero;
			this.EncodeQueue[0].ReportEncodeStart(this.totalTasks == 1);
			this.encodeProxy.StartEncode(this.CurrentJob.Job, encodeLogger, false, 0, 0, 0);

			this.StopEncodeCommand.RaiseCanExecuteChanged();
			this.PauseCommand.RaiseCanExecuteChanged();
		}

		private void OnEncodeStarted(object sender, EventArgs e)
		{
			DispatchService.BeginInvoke(() =>
			    {
					// After the encode has reported that it's started, we can now pause/stop it.
					this.StopEncodeCommand.RaiseCanExecuteChanged();
					this.PauseCommand.RaiseCanExecuteChanged();
			    });
		}

		private void OnEncodeProgress(object sender, EncodeProgressEventArgs e)
		{
			if (this.EncodeQueue.Count == 0)
			{
				return;
			}

			VCJob currentJob = this.EncodeQueue[0].Job;
			double passCost = currentJob.Length.TotalSeconds;
			double scanPassCost = passCost / EncodeJobViewModel.SubtitleScanCostFactor;
			double currentJobCompletedWork = 0.0;

			if (this.EncodeQueue[0].SubtitleScan)
			{
				switch (e.Pass)
				{
					case -1:
						currentJobCompletedWork += scanPassCost * e.FractionComplete;
						break;
					case 1:
						currentJobCompletedWork += scanPassCost;
						currentJobCompletedWork += passCost * e.FractionComplete;
						break;
					case 2:
						currentJobCompletedWork += scanPassCost;
						currentJobCompletedWork += passCost;
						currentJobCompletedWork += passCost * e.FractionComplete;
						break;
					default:
						break;
				}
			}
			else
			{
				switch (e.Pass)
				{
					case 1:
						currentJobCompletedWork += passCost * e.FractionComplete;
						break;
					case 2:
						currentJobCompletedWork += passCost;
						currentJobCompletedWork += passCost * e.FractionComplete;
						break;
					default:
						break;
				}
			}

			double totalCompletedWork = this.completedQueueWork + currentJobCompletedWork;

			this.OverallEncodeProgressFraction = totalCompletedWork / this.totalQueueCost;

			double overallWorkCompletionRate = totalCompletedWork / this.elapsedQueueEncodeTime.Elapsed.TotalSeconds;

			// Only update encode time every 5th update.
			if (Interlocked.Increment(ref this.pollCount) % 5 == 1)
			{
				if (this.elapsedQueueEncodeTime != null && this.elapsedQueueEncodeTime.Elapsed.TotalSeconds > 0.5 && this.OverallEncodeProgressFraction != 0.0)
				{
					if (this.OverallEncodeProgressFraction == 1.0)
					{
						this.EstimatedTimeRemaining = Utilities.FormatTimeSpan(TimeSpan.Zero);
					}
					else
					{
						this.overallEtaSpan = TimeSpan.FromSeconds((long)(((1.0 - this.OverallEncodeProgressFraction) * this.elapsedQueueEncodeTime.Elapsed.TotalSeconds) / this.OverallEncodeProgressFraction));
						this.EstimatedTimeRemaining = Utilities.FormatTimeSpan(this.overallEtaSpan);
					}

					double currentJobRemainingWork = this.EncodeQueue[0].Cost - currentJobCompletedWork;

					this.currentJobEta =
						TimeSpan.FromSeconds(currentJobRemainingWork / overallWorkCompletionRate);
					this.EncodeQueue[0].Eta = this.currentJobEta;
				}
			}

			double currentJobFractionComplete = currentJobCompletedWork / this.EncodeQueue[0].Cost;
			this.EncodeQueue[0].PercentComplete = (int)(currentJobFractionComplete * 100.0);

			if (e.EstimatedTimeLeft >= TimeSpan.Zero)
			{
				this.CurrentFps = Math.Round(e.CurrentFrameRate, 1);
				this.AverageFps = Math.Round(e.AverageFrameRate, 1);
				this.EncodeSpeedDetailsAvailable = true;
			}

			VCProfile currentProfile = currentJob.EncodingProfile;

			var progressChangedMessage = new ProgressChangedMessage
			{
				Encoding = true,
				OverallProgressFraction = this.OverallEncodeProgressFraction,
				TaskNumber = this.taskNumber,
				TotalTasks = this.totalTasks,
				OverallElapsedTime = this.elapsedQueueEncodeTime.Elapsed,
				OverallEta = this.overallEtaSpan,
				FileName = Path.GetFileName(currentJob.OutputPath),
				FileProgressFraction = currentJobFractionComplete,
				FileElapsedTime = this.CurrentJob.EncodeTime,
				FileEta = this.currentJobEta,
				HasScanPass = this.CurrentJob.SubtitleScan,
				TwoPass = currentProfile.VideoEncodeRateType != VideoEncodeRateType.ConstantQuality && currentProfile.TwoPass,
				CurrentPass = e.Pass,
				PassProgressFraction = e.FractionComplete,
				EncodeSpeedDetailsAvailable = this.EncodeSpeedDetailsAvailable,
				CurrentFps = this.CurrentFps,
				AverageFps = this.AverageFps
			};

			try
			{
				var outputFileInfo = new FileInfo(currentJob.OutputPath);
				progressChangedMessage.FileSizeBytes = outputFileInfo.Length;
			}
			catch (IOException)
			{
			}
			catch (UnauthorizedAccessException)
			{
			}

			Messenger.Default.Send(progressChangedMessage);
		}

		private void OnEncodeCompleted(object sender, EncodeCompletedEventArgs e)
		{
			DispatchService.BeginInvoke(() =>
			{
				ILogger encodeLogger = this.CurrentJob.Logger;
				string outputPath = this.CurrentJob.Job.OutputPath;

				if (this.encodeStopped)
				{
					// If the encode was stopped manually
					this.StopEncodingAndReport();
					this.CurrentJob.ReportEncodeEnd();

					if (this.totalTasks == 1)
					{
						this.EncodeQueue.Clear();
					}

					encodeLogger.Log("Encoding stopped");
				}
				else
				{
					// If the encode completed successfully
					this.completedQueueWork += this.CurrentJob.Cost;

					var outputFileInfo = new FileInfo(this.CurrentJob.Job.OutputPath);

					EncodeResultStatus status = EncodeResultStatus.Succeeded;
					if (e.Error)
					{
						status = EncodeResultStatus.Failed;
						encodeLogger.LogError("Encode failed.");
					}
					else if (!outputFileInfo.Exists)
					{
						status = EncodeResultStatus.Failed;
						encodeLogger.LogError("Encode failed. HandBrake reported no error but the expected output file was not found.");
					}
					else if (outputFileInfo.Length == 0)
					{
						status = EncodeResultStatus.Failed;
						encodeLogger.LogError("Encode failed. HandBrake reported no error but the output file was empty.");
					}

					EncodeJobViewModel finishedJob = this.CurrentJob;

					if (Config.PreserveModifyTimeFiles)
					{
						try
						{
							if (status != EncodeResultStatus.Failed && !Utilities.IsDirectory(finishedJob.Job.SourcePath))
							{
								FileInfo info = new FileInfo(finishedJob.Job.SourcePath);

								File.SetCreationTimeUtc(finishedJob.Job.OutputPath, info.CreationTimeUtc);
								File.SetLastWriteTimeUtc(finishedJob.Job.OutputPath, info.LastWriteTimeUtc);
							}
						}
						catch (IOException exception)
						{
							encodeLogger.LogError("Could not set create/modify dates on file: " + exception);
						}
						catch (UnauthorizedAccessException exception)
						{
							encodeLogger.LogError("Could not set create/modify dates on file: " + exception);
						} 
					}

					this.CompletedJobs.Add(new EncodeResultViewModel(
						new EncodeResult
						{
							Destination = this.CurrentJob.Job.OutputPath,
							Status = status,
							EncodeTime = this.CurrentJob.EncodeTime,
							LogPath = encodeLogger.LogPath
						},
						finishedJob));
					this.RaisePropertyChanged(() => this.CompletedItemsCount);
					this.RaisePropertyChanged(() => this.CompletedTabHeader);

					this.EncodeQueue.RemoveAt(0);
					this.RaisePropertyChanged(() => this.QueuedTabHeader);

					// Wait until after it's removed from the queue before running cleanup: otherwise it will find
					// the instance "in use" in the queue and not do removal.
					if (!Config.KeepScansAfterCompletion)
					{
						this.CleanupHandBrakeInstanceIfUnused(finishedJob.HandBrakeInstance);
						finishedJob.HandBrakeInstance = null;
					}

					encodeLogger.Log("Job completed (Elapsed Time: " + Utilities.FormatTimeSpan(finishedJob.EncodeTime) + ")");

					if (this.EncodeQueue.Count == 0)
					{
						this.SelectedTabIndex = CompletedTabIndex;
						this.StopEncodingAndReport();

						this.logger.Log("Queue completed");
						this.logger.ShowStatus(MainRes.EncodeCompleted);
						this.logger.Log("");

						Ioc.Container.GetInstance<TrayService>().ShowBalloonMessage(MainRes.EncodeCompleteBalloonTitle, MainRes.EncodeCompleteBalloonMessage);

						EncodeCompleteActionType actionType = this.EncodeCompleteAction.ActionType;
						switch (actionType)
						{
							case EncodeCompleteActionType.DoNothing:
								break;
							case EncodeCompleteActionType.EjectDisc:
								this.systemOperations.Eject(this.EncodeCompleteAction.DriveLetter);
								break;
							case EncodeCompleteActionType.Sleep:
								this.systemOperations.Sleep();
								break;
							case EncodeCompleteActionType.LogOff:
								this.systemOperations.LogOff();
								break;
							case EncodeCompleteActionType.Shutdown:
								this.systemOperations.ShutDown();
								break;
							default:
								throw new ArgumentOutOfRangeException();
						}

						if (Config.PlaySoundOnCompletion &&
							actionType != EncodeCompleteActionType.Sleep && 
							actionType != EncodeCompleteActionType.LogOff &&
							actionType != EncodeCompleteActionType.Shutdown)
						{
							string soundPath = null;
							if (Config.UseCustomCompletionSound)
							{
								if (File.Exists(Config.CustomCompletionSound))
								{
									soundPath = Config.CustomCompletionSound;
								}
								else
								{
									this.logger.LogError(string.Format("Cound not find custom completion sound \"{0}\" . Using default.", Config.CustomCompletionSound));
								}
							}

							if (soundPath == null)
							{
								soundPath = Path.Combine(Utilities.ProgramFolder, "Encode_Complete.wav");
							}

							var soundPlayer = new SoundPlayer(soundPath);

							try
							{
								soundPlayer.Play();
							}
							catch (InvalidOperationException)
							{
								this.logger.LogError(string.Format("Completion sound \"{0}\" was not a supported WAV file.", soundPath));
							}
						}
					}
					else
					{
						this.EncodeNextJob();
					}
				}

				if (this.encodeStopped || this.EncodeQueue.Count == 0)
				{
					this.main.WindowManagerVM.CloseEncodeDetailsWindow();
				}

				string encodeLogPath = encodeLogger.LogPath;
				encodeLogger.Dispose();

				if (Config.CopyLogToOutputFolder)
				{
					string logCopyPath = Path.Combine(Path.GetDirectoryName(outputPath), Path.GetFileName(encodeLogPath));

					try
					{
						File.Copy(encodeLogPath, logCopyPath);
					}
					catch (IOException exception)
					{
						this.logger.LogError("Could not copy log file to output directory: " + exception);
					}
					catch (UnauthorizedAccessException exception)
					{
						this.logger.LogError("Could not copy log file to output directory: " + exception);
					}
				}
			});
		}

		private void PauseEncoding()
		{
			this.encodeProxy.PauseEncode();
			this.EncodeProgressState = TaskbarItemProgressState.Paused;
			this.CurrentJob.ReportEncodePause();

			this.Paused = true;
		}

		private void ResumeEncoding()
		{
			this.encodeProxy.ResumeEncode();
			this.EncodeProgressState = TaskbarItemProgressState.Normal;
			this.CurrentJob.ReportEncodeResume();

			this.Paused = false;
		}

		private void StopEncodingAndReport()
		{
			this.EncodeProgressState = TaskbarItemProgressState.None;
			this.Encoding = false;
			this.autoPause.ReportStop();
		}

		private void SaveEncodeQueue()
		{
			EncodeJobsPersist.EncodeJobs = this.GetQueuePersistGroup();
		}

		private void RefreshCanEnqueue()
		{
			this.RaisePropertyChanged(() => this.CanTryEnqueueMultipleTitles);
			this.RaisePropertyChanged(() => this.CanTryEnqueue);

			this.AddToQueueCommand.RaiseCanExecuteChanged();
			this.QueueTitlesCommand.RaiseCanExecuteChanged();
		}

		private void RefreshEncodeCompleteActions()
		{
			if (this.EncodeQueue == null || this.CompletedJobs == null)
			{
				return;
			}

			EncodeCompleteAction oldCompleteAction = this.EncodeCompleteAction;

			this.encodeCompleteActions =
				new List<EncodeCompleteAction>
				{
					new EncodeCompleteAction { ActionType = EncodeCompleteActionType.DoNothing },
					new EncodeCompleteAction { ActionType = EncodeCompleteActionType.Sleep },
					new EncodeCompleteAction { ActionType = EncodeCompleteActionType.LogOff },
					new EncodeCompleteAction { ActionType = EncodeCompleteActionType.Shutdown },
				};

			// Applicable drives to eject are those in the queue or completed items list
			var applicableDrives = new HashSet<string>();
			foreach (EncodeJobViewModel job in this.EncodeQueue)
			{
				if (job.Job.SourceType == SourceType.Dvd)
				{
					string driveLetter = job.Job.SourcePath.Substring(0, 1).ToUpperInvariant();
					if (!applicableDrives.Contains(driveLetter))
					{
						applicableDrives.Add(driveLetter);
					}
				}
			}

			foreach (EncodeResultViewModel result in this.CompletedJobs)
			{
				if (result.Job.Job.SourceType == SourceType.Dvd)
				{
					string driveLetter = result.Job.Job.SourcePath.Substring(0, 1).ToUpperInvariant();
					if (!applicableDrives.Contains(driveLetter))
					{
						applicableDrives.Add(driveLetter);
					}
				}
			}

			// Order backwards so repeated insertions put them in correct order
			var orderedDrives =
				from d in applicableDrives
				orderby d descending 
				select d;

			foreach (string drive in orderedDrives)
			{
				this.encodeCompleteActions.Insert(1, new EncodeCompleteAction { ActionType = EncodeCompleteActionType.EjectDisc, DriveLetter = drive });
			}

			this.RaisePropertyChanged(() => this.EncodeCompleteActions);

			// Transfer over the previously selected item
			this.encodeCompleteAction = this.encodeCompleteActions[0];
			for (int i = 1; i < this.encodeCompleteActions.Count; i++)
			{
				if (this.encodeCompleteActions[i].Equals(oldCompleteAction))
				{
					this.encodeCompleteAction = this.encodeCompleteActions[i];
					break;
				}
			}

			this.RaisePropertyChanged(() => this.EncodeCompleteAction);
		}

		private bool EnsureDefaultOutputFolderSet()
		{
			if (!string.IsNullOrEmpty(Config.AutoNameOutputFolder))
			{
				return true;
			}

			var messageService = Ioc.Container.GetInstance<IMessageBoxService>();
			var messageResult = messageService.Show(
				this.main,
				MainRes.OutputFolderRequiredMessage, 
				MainRes.OutputFolderRequiredTitle, 
				MessageBoxButton.OKCancel, 
				MessageBoxImage.Information);

			if (messageResult == MessageBoxResult.Cancel)
			{
				return false;
			}

			return this.outputVM.PickDefaultOutputFolder();
		}

		private bool EnsureValidOutputPath()
		{
			if (this.outputVM.PathIsValid())
			{
				return true;
			}

			Ioc.Container.GetInstance<IMessageBoxService>().Show(
				MainRes.OutputPathNotValidMessage,
				MainRes.OutputPathNotValidTitle, 
				MessageBoxButton.OK,
				MessageBoxImage.Error);

			return false;
		}

		private void AutoPauseEncoding(object sender, EventArgs e)
		{
			DispatchService.Invoke(() =>
			{
				if (this.Encoding && !this.Paused && this.CurrentJob.HandBrakeInstance != null)
				{
					this.PauseEncoding();
				}
			});
		}

		private void AutoResumeEncoding(object sender, EventArgs e)
		{
			DispatchService.Invoke(() =>
			{
				if (this.Encoding && this.Paused)
				{
					this.ResumeEncoding();
				}
			});
		}

		// Automatically pick the correct audio on the given job.
		// Only relies on input from settings and the current title.
		private void AutoPickAudio(VCJob job, Title title, bool useCurrentContext = false)
		{
			job.ChosenAudioTracks = new List<int>();
			switch (CustomConfig.AutoAudio)
			{
				case AutoAudioType.Disabled:
					if (title.AudioTracks.Count > 0)
					{
						if (useCurrentContext)
						{
							// With previous context, pick similarly
							foreach (AudioChoiceViewModel audioVM in this.main.AudioChoices)
							{
								int audioIndex = audioVM.SelectedIndex;

								if (title.AudioTracks.Count > audioIndex && this.main.SelectedTitle.AudioTracks[audioIndex].LanguageCode == title.AudioTracks[audioIndex].LanguageCode)
								{
									job.ChosenAudioTracks.Add(audioIndex + 1);
								}
							}

							// If we didn't manage to match any existing audio tracks, use the first audio track.
							if (this.main.AudioChoices.Count > 0 && job.ChosenAudioTracks.Count == 0)
							{
								job.ChosenAudioTracks.Add(1);
							}
						}
						else
						{
							// With no previous context, just pick the first track
							job.ChosenAudioTracks.Add(1);
						}
					}

					break;
				case AutoAudioType.Language:
					List<AudioTrack> nativeTracks = title.AudioTracks.Where(track => track.LanguageCode == Config.AudioLanguageCode).ToList();
					if (nativeTracks.Count > 0)
					{
						if (Config.AutoAudioAll)
						{
							foreach (AudioTrack audioTrack in nativeTracks)
							{
								job.ChosenAudioTracks.Add(audioTrack.TrackNumber);
							}
						}
						else
						{
							job.ChosenAudioTracks.Add(nativeTracks[0].TrackNumber);
						}
					}
					break;
				case AutoAudioType.All:
					foreach (AudioTrack audioTrack in title.AudioTracks)
					{
						job.ChosenAudioTracks.Add(audioTrack.TrackNumber);
					}
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}

			// If none get chosen, pick the first one.
			if (job.ChosenAudioTracks.Count == 0 && title.AudioTracks.Count > 0)
			{
				job.ChosenAudioTracks.Add(1);
			}
		}

		// Automatically pick the correct subtitles on the given job.
		// Only relies on input from settings and the current title.
		private void AutoPickSubtitles(VCJob job, Title title, bool useCurrentContext = false)
		{
			job.Subtitles = new Subtitles { SourceSubtitles = new List<SourceSubtitle>(), SrtSubtitles = new List<SrtSubtitle>() };
			switch (CustomConfig.AutoSubtitle)
			{
				case AutoSubtitleType.Disabled:
					// Only pick subtitles when we have previous context.
					if (useCurrentContext)
					{
						foreach (SourceSubtitle sourceSubtitle in this.main.CurrentSubtitles.SourceSubtitles)
						{
							if (sourceSubtitle.TrackNumber == 0)
							{
								job.Subtitles.SourceSubtitles.Add(sourceSubtitle.Clone());
							}
							else if (
								title.Subtitles.Count > sourceSubtitle.TrackNumber - 1 &&
								this.main.SelectedTitle.Subtitles[sourceSubtitle.TrackNumber - 1].LanguageCode == title.Subtitles[sourceSubtitle.TrackNumber - 1].LanguageCode)
							{
								job.Subtitles.SourceSubtitles.Add(sourceSubtitle.Clone());
							}
						}
					}
					break;
				case AutoSubtitleType.ForeignAudioSearch:
					job.Subtitles.SourceSubtitles.Add(
						new SourceSubtitle
						{
							TrackNumber = 0,
							BurnedIn = Config.AutoSubtitleBurnIn,
							Forced = true,
							Default = true
						});
					break;
				case AutoSubtitleType.Language:
					string languageCode = Config.SubtitleLanguageCode;
					bool audioSame = false;
					bool burnIn = Config.AutoSubtitleLanguageBurnIn;
					if (job.ChosenAudioTracks.Count > 0 && title.AudioTracks.Count > 0)
					{
						if (title.AudioTracks[job.ChosenAudioTracks[0] - 1].LanguageCode == languageCode)
						{
							audioSame = true;
						}
					}

					if (!Config.AutoSubtitleOnlyIfDifferent || !audioSame)
					{
						List<Subtitle> nativeSubtitles = title.Subtitles.Where(subtitle => subtitle.LanguageCode == languageCode).ToList();
						if (nativeSubtitles.Count > 0)
						{
							if (Config.AutoSubtitleAll)
							{
								foreach (Subtitle subtitle in nativeSubtitles)
								{
									job.Subtitles.SourceSubtitles.Add(new SourceSubtitle
									{
										BurnedIn = false,
										Default = false,
										Forced = false,
										TrackNumber = subtitle.TrackNumber
									});
								}
							}
							else
							{
								job.Subtitles.SourceSubtitles.Add(new SourceSubtitle
								{
									BurnedIn = burnIn,
									Default = false,
									Forced = false,
									TrackNumber = nativeSubtitles[0].TrackNumber
								});
							}
						}
					}

					break;
				case AutoSubtitleType.All:
					foreach (Subtitle subtitle in title.Subtitles)
					{
						job.Subtitles.SourceSubtitles.Add(
							new SourceSubtitle
							{
								TrackNumber = subtitle.TrackNumber,
								BurnedIn = false,
								Default = false,
								Forced = false
							});
					}
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}
		}
	}
}
