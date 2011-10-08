using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
using HandBrake.Interop.Model.Encoding;
using HandBrake.Interop.SourceData;
using Microsoft.Practices.Unity;
using VidCoder.Messages;
using VidCoder.Model;
using VidCoder.Properties;
using VidCoder.Services;

namespace VidCoder.ViewModel.Components
{
	/// <summary>
	/// Controls the queue and actual processing of encode jobs.
	/// </summary>
	public class ProcessingViewModel : ViewModelBase
	{
		public const int QueuedTabIndex = 0;
		public const int CompletedTabIndex = 1;

		private ILogger logger = Unity.Container.Resolve<ILogger>();
		private IProcessAutoPause autoPause = Unity.Container.Resolve<IProcessAutoPause>();
		private ISystemOperations systemOperations = Unity.Container.Resolve<ISystemOperations>();
		private MainViewModel main = Unity.Container.Resolve<MainViewModel>();
		private OutputPathViewModel outputVM = Unity.Container.Resolve<OutputPathViewModel>();
		private PresetsViewModel presetsViewModel = Unity.Container.Resolve<PresetsViewModel>();

		private ObservableCollection<EncodeJobViewModel> encodeQueue;
		private bool encoding;
		private bool paused;
		private bool encodeStopped;
		private bool errorLoggedDuringJob; // True if an error was logged during an encode (and no scan was going on at the time)
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
		private TimeSpan currentJobEta; // Kept around to check if the job finished early
		private TaskbarItemProgressState encodeProgressState;
		private ObservableCollection<EncodeResultViewModel> completedJobs;
		private EncodeCompleteAction encodeCompleteAction;

		private int selectedTabIndex;

		public ProcessingViewModel()
		{
			this.encodeQueue = new ObservableCollection<EncodeJobViewModel>();
			this.encodeQueue.CollectionChanged += (sender, e) => { this.SaveEncodeQueue(); };
			EncodeJobPersistGroup jobPersistGroup = EncodeJobsPersist.EncodeJobs;
			foreach (EncodeJobWithMetadata job in jobPersistGroup.EncodeJobs)
			{
				this.encodeQueue.Add(new EncodeJobViewModel(job.Job) { ManualOutputPath = job.ManualOutputPath });
			}

			this.autoPause.PauseEncoding += this.AutoPauseEncoding;
			this.autoPause.ResumeEncoding += this.AutoResumeEncoding;

			// Keep track of errors logged. HandBrake doesn't reliably report errors on job completion.
			this.logger.EntryLogged += (sender, e) =>
			{
				if (e.Value.LogType == LogType.Error && this.Encoding && !this.main.ScanningSource)
				{
					this.errorLoggedDuringJob = true;
				}
			};

			this.encodeQueue.CollectionChanged +=
				(o, e) =>
					{
						this.EncodeCommand.RaiseCanExecuteChanged();
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

			Messenger.Default.Register<OutputFolderChangedMessage>(
				this,
				message =>
					{
						this.QueueFilesCommand.RaiseCanExecuteChanged();
						this.QueueTitlesCommand.RaiseCanExecuteChanged();
					});

			Messenger.Default.Register<SelectedTitleChangedMessage>(
				this,
				message =>
					{
						this.QueueTitlesCommand.RaiseCanExecuteChanged();
					});

			this.completedJobs = new ObservableCollection<EncodeResultViewModel>();
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

		public bool CanEnqueue
		{
			get
			{
				return this.main.HasVideoSource && Utilities.IsValidFullPath(this.outputVM.OutputPath);
			}
		}

		public string EnqueueToolTip
		{
			get
			{
				if (String.IsNullOrEmpty(Settings.Default.AutoNameOutputFolder))
				{
					return "Please choose a destination directory before adding items to the queue.";
				}

				return null;
			}
		}

		public string EncodeToolTip
		{
			get
			{
				if (String.IsNullOrEmpty(Settings.Default.AutoNameOutputFolder))
				{
					return "Please choose a destination directory before encoding.";
				}

				return null;
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
					Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.BelowNormal;
					SystemSleepManagement.PreventSleep();
					this.elapsedQueueEncodeTime = Stopwatch.StartNew();
				}
				else
				{
					Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.Normal;
					this.EncodeSpeedDetailsAvailable = false;
					SystemSleepManagement.AllowSleep();
					this.elapsedQueueEncodeTime.Stop();
				}

				this.PauseCommand.RaiseCanExecuteChanged();
				this.RaisePropertyChanged("PauseVisible");
				this.RaisePropertyChanged("Encoding");
				this.RaisePropertyChanged("EncodeButtonText");
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

				this.RaisePropertyChanged("PauseVisible");
				this.RaisePropertyChanged("ProgressBarColor");
				this.RaisePropertyChanged("Paused");
			}
		}

		public string EncodeButtonText
		{
			get
			{
				if (this.Encoding)
				{
					return "Resume";
				}
				else
				{
					return "Encode";
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
					return "Queued";
				}

				return "Queued (" + this.EncodeQueue.Count + ")";
			}
		}

		public string CompletedTabHeader
		{
			get
			{
				return "Completed (" + this.CompletedJobs.Count + ")";
			}
		}

		public bool CanEnqueueMultipleTitles
		{
			get
			{
				return !string.IsNullOrEmpty(Settings.Default.AutoNameOutputFolder) && this.main.HasVideoSource && this.main.SourceData.Titles.Count > 1;
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
				this.RaisePropertyChanged("EncodeSpeedDetailsAvailable");
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
				this.RaisePropertyChanged("EstimatedTimeRemaining");
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
				this.RaisePropertyChanged("EncodeCompleteAction");
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
				this.RaisePropertyChanged("CurrentFps");
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
				this.RaisePropertyChanged("AverageFps");
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
				this.RaisePropertyChanged("OverallEncodeProgressPercent");
				this.RaisePropertyChanged("OverallEncodeProgressFraction");
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
				this.RaisePropertyChanged("EncodeProgressState");
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
				this.RaisePropertyChanged("SelectedTabIndex");
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
								var newEncodeJobVM = this.main.CreateEncodeJobVM();

								string resolvedOutputPath = this.outputVM.ResolveOutputPathConflicts(newEncodeJobVM.Job.OutputPath, isBatch: false);
								if (resolvedOutputPath == null)
								{
									// There was a conflict and the user canceled out of the operation.
									return;
								}

								newEncodeJobVM.Job.OutputPath = resolvedOutputPath;

								this.EncodeQueue.Add(newEncodeJobVM);
							}

							this.SelectedTabIndex = QueuedTabIndex;

							this.StartEncodeQueue();
						}
					},
					() =>
					{
						return this.EncodeQueue.Count > 0 || this.CanEnqueue;
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
						var newEncodeJobVM = this.main.CreateEncodeJobVM();

						string resolvedOutputPath = this.outputVM.ResolveOutputPathConflicts(newEncodeJobVM.Job.OutputPath, isBatch: false);
						if (resolvedOutputPath == null)
						{
							// There was a conflict and the user canceled out of the operation.
							return;
						}

						newEncodeJobVM.Job.OutputPath = resolvedOutputPath;

						this.Queue(newEncodeJobVM);
					},
					() =>
					{
						return this.CanEnqueue;
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
						IList<string> fileNames = FileService.Instance.GetFileNames(Settings.Default.LastInputFileFolder);
						if (fileNames != null && fileNames.Count > 0)
						{
							Settings.Default.LastInputFileFolder = Path.GetDirectoryName(fileNames[0]);
							Settings.Default.Save();

							this.QueueMultiple(fileNames);
						}
					},
					() =>
					{
						return !string.IsNullOrEmpty(Settings.Default.AutoNameOutputFolder);
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
						var queueTitlesDialog = new QueueTitlesDialogViewModel(this.main.SourceData.Titles);
						WindowManager.OpenDialog(queueTitlesDialog, this.main);

						if (queueTitlesDialog.DialogResult)
						{
							int currentTitleNumber = queueTitlesDialog.TitleStartOverride;

							// Queue the selected titles
							List<Title> titlesToQueue = queueTitlesDialog.CheckedTitles;
							foreach (Title title in titlesToQueue)
							{
								// Use current subtitle and audio track choices for each queued title.
								var subtitles =
									new Subtitles
									{
										SrtSubtitles = new List<SrtSubtitle>(),
										SourceSubtitles = new List<SourceSubtitle>()
									};

								foreach (SourceSubtitle sourceSubtitle in this.main.CurrentSubtitles.SourceSubtitles)
								{
									if (sourceSubtitle.TrackNumber == 0)
									{
										subtitles.SourceSubtitles.Add(sourceSubtitle.Clone());
									}
									else if (
										title.Subtitles.Count > sourceSubtitle.TrackNumber - 1 &&
										this.main.SelectedTitle.Subtitles[sourceSubtitle.TrackNumber - 1].LanguageCode == title.Subtitles[sourceSubtitle.TrackNumber - 1].LanguageCode)
									{
										subtitles.SourceSubtitles.Add(sourceSubtitle.Clone());
									}
								}

								var currentAudioChoices = new List<int>();
								foreach (AudioChoiceViewModel audioVM in this.main.AudioChoices)
								{
									int audioIndex = audioVM.SelectedIndex;

									if (title.AudioTracks.Count > audioIndex && this.main.SelectedTitle.AudioTracks[audioIndex].LanguageCode == title.AudioTracks[audioIndex].LanguageCode)
									{
										currentAudioChoices.Add(audioIndex + 1);
									}
								}

								// If we didn't manage to match any existing audio tracks, use the first audio track.
								if (this.main.AudioChoices.Count > 0 && currentAudioChoices.Count == 0)
								{
									currentAudioChoices.Add(1);
								}

								EncodingProfile profile = this.presetsViewModel.SelectedPreset.Preset.EncodingProfile;
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

								string queueOutputFileName = this.outputVM.BuildOutputFileName(
									this.main.SourcePath,
									queueSourceName,
									titleNumber,
									title.Chapters.Count);

								string extension = this.outputVM.GetOutputExtension(subtitles, title);
								string queueOutputPath = this.outputVM.BuildOutputPath(queueOutputFileName, extension, sourcePath: null);

								var job = new EncodeJob
								{
									SourceType = this.main.SelectedSource.Type,
									SourcePath = this.main.SourcePath,
									OutputPath = this.outputVM.ResolveOutputPathConflicts(queueOutputPath, isBatch: true),
									EncodingProfile = profile.Clone(),
									Title = title.TitleNumber,
									ChapterStart = 1,
									ChapterEnd = title.Chapters.Count,
									ChosenAudioTracks = currentAudioChoices,
									Subtitles = subtitles,
									UseDefaultChapterNames = true,
									Length = title.Duration
								};

								var jobVM = new EncodeJobViewModel(job);
								jobVM.HandBrakeInstance = this.main.ScanInstance;
								jobVM.VideoSource = this.main.SourceData;
								jobVM.VideoSourceMetadata = this.main.GetVideoSourceMetadata();
								jobVM.ManualOutputPath = false;

								this.Queue(jobVM);
							}
						}
					},
					() =>
					{
						return this.CanEnqueueMultipleTitles;
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
						return this.Encoding && this.CurrentJob.HandBrakeInstance != null;
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
						// Signify that we stopped the encode manually rather than it completing.
						this.encodeStopped = true;
						this.CurrentJob.HandBrakeInstance.StopEncode();
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
						this.CompletedJobs.Clear();
						this.RaisePropertyChanged("CompletedItemsCount");
						this.RaisePropertyChanged("CompletedTabHeader");
					}));
			}
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
				this.RaisePropertyChanged("TaskDetails");
			}

			this.EncodeQueue.Add(encodeJobVM);

			this.RaisePropertyChanged("QueuedTabHeader");

			// Select the Queued tab.
			if (this.SelectedTabIndex != QueuedTabIndex)
			{
				this.SelectedTabIndex = QueuedTabIndex;
			}
		}

		public void QueueMultiple(IEnumerable<string> filesToQueue)
		{
			// Don't queue anything if we don't know where the output files will go.
			if (string.IsNullOrEmpty(Settings.Default.AutoNameOutputFolder))
			{
				return;
			}

			// Exclude all current queued files if overwrite is disabled
			HashSet<string> excludedPaths;
			if (Settings.Default.WhenFileExistsBatch == WhenFileExists.AutoRename)
			{
				excludedPaths = this.GetQueuedFiles();
			}
			else
			{
				excludedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			}

			var itemsToQueue = new List<EncodeJobViewModel>();
			foreach (string fileToQueue in filesToQueue)
			{
				excludedPaths.Add(fileToQueue);
				string outputFolder = this.outputVM.GetOutputFolder(fileToQueue);
				string queueOutputPath = Path.Combine(outputFolder, Path.GetFileNameWithoutExtension(fileToQueue) + this.outputVM.GetOutputExtensionForCurrentEncodingProfile());
				queueOutputPath = this.outputVM.ResolveOutputPathConflicts(queueOutputPath, excludedPaths, isBatch: true);

				// Even if you're doing overwrite don't try to stomp on the source file.
				if (string.Compare(queueOutputPath, fileToQueue, StringComparison.OrdinalIgnoreCase) == 0)
				{
					queueOutputPath = Utilities.CreateUniqueFileName(Path.GetFileNameWithoutExtension(fileToQueue) + this.outputVM.GetOutputExtensionForCurrentEncodingProfile(), outputFolder, excludedPaths);
				}

				excludedPaths.Add(queueOutputPath);

				// When ChapterStart is 0, this means the whole title is encoded.
				var job = new EncodeJob
				{
					SourcePath = fileToQueue,
					OutputPath = queueOutputPath,
					EncodingProfile = this.presetsViewModel.SelectedPreset.Preset.EncodingProfile.Clone(),
					Title = 1,
					RangeType = VideoRangeType.Chapters,
					ChapterStart = 0,
					ChapterEnd = 0,
					ChosenAudioTracks = new List<int> { 1 },
					Subtitles = new Subtitles(),
					UseDefaultChapterNames = true
				};

				if (Directory.Exists(fileToQueue))
				{
					job.SourceType = SourceType.VideoFolder;
				}
				else if (File.Exists(fileToQueue))
				{
					job.SourceType = SourceType.File;
				}

				if (job.SourceType != SourceType.None)
				{
					var jobVM = new EncodeJobViewModel(job);
					jobVM.ManualOutputPath = false;
					itemsToQueue.Add(jobVM);
				}
			}

			// This dialog will scan the items in the list, calculating length.
			var scanMultipleDialog = new ScanMultipleDialogViewModel(itemsToQueue);
			WindowManager.OpenDialog(scanMultipleDialog, this.main);

			var failedFiles = new List<string>();
			foreach (EncodeJobViewModel jobVM in itemsToQueue)
			{
				// Only queue items with a successful scan
				if (jobVM.HandBrakeInstance.Titles.Count > 0)
				{
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
					"The following file(s) could not be recognized and were not added to the queue:" + Environment.NewLine + Environment.NewLine + string.Join(Environment.NewLine, failedFiles),
					"Error scanning video file(s)",
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

				this.RaisePropertyChanged("TaskDetails");

				if (this.totalTasks == 1)
				{
					this.EncodeQueue[0].IsOnlyItem = true;
				}
			}

			this.RaisePropertyChanged("QueuedTabHeader");
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
		}

		public HashSet<string> GetQueuedFiles()
		{
			return new HashSet<string>(this.EncodeQueue.Select(j => j.Job.OutputPath), StringComparer.OrdinalIgnoreCase);
		}

		private void EncodeNextJob()
		{
			this.taskNumber++;
			this.RaisePropertyChanged("TaskDetails");

			if (this.CurrentJob.HandBrakeInstance == null)
			{
				var onDemandInstance = new HandBrakeInstance();
				onDemandInstance.Initialize(Settings.Default.LogVerbosity);
				onDemandInstance.ScanCompleted += (o, e) =>
				{
					this.CurrentJob.HandBrakeInstance = onDemandInstance;
					Title encodeTitle = onDemandInstance.Titles.FirstOrDefault(title => title.TitleNumber == this.CurrentJob.Job.Title);

					if (encodeTitle != null)
					{
						this.StartEncode();
						DispatchService.BeginInvoke(this.PauseCommand.RaiseCanExecuteChanged);
					}
					else
					{
						this.OnEncodeCompleted(this, new EncodeCompletedEventArgs { Error = true });
					}
				};

				onDemandInstance.StartScan(this.CurrentJob.Job.SourcePath, Settings.Default.PreviewCount, this.CurrentJob.Job.Title);
			}
			else
			{
				this.StartEncode();
			}
		}

		private void StartEncode()
		{
			EncodeJob job = this.CurrentJob.Job;

			this.logger.Log("Starting job " + this.taskNumber + "/" + this.totalTasks);
			this.logger.Log("  Path: " + job.SourcePath);
			this.logger.Log("  Title: " + job.Title);
			this.logger.Log("  Chapters: " + job.ChapterStart + "-" + job.ChapterEnd);
			this.CurrentJob.HandBrakeInstance.EncodeProgress += this.OnEncodeProgress;
			this.CurrentJob.HandBrakeInstance.EncodeCompleted += this.OnEncodeCompleted;

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
						"Could not create output directory. Error details: " + Environment.NewLine + Environment.NewLine + exception.ToString(),
						"Error creating directory",
						MessageBoxButton.OK,
						MessageBoxImage.Error);
				}
			}

			this.currentJobEta = TimeSpan.Zero;
			this.errorLoggedDuringJob = false;
			this.EncodeQueue[0].ReportEncodeStart(this.totalTasks == 1);
			this.CurrentJob.HandBrakeInstance.StartEncode(this.CurrentJob.Job);
		}

		private void OnEncodeProgress(object sender, EncodeProgressEventArgs e)
		{
			if (this.EncodeQueue.Count == 0)
			{
				return;
			}

			EncodeJob currentJob = this.EncodeQueue[0].Job;
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
						TimeSpan eta = TimeSpan.FromSeconds((long)(((1.0 - this.OverallEncodeProgressFraction) * this.elapsedQueueEncodeTime.Elapsed.TotalSeconds) / this.OverallEncodeProgressFraction));
						this.EstimatedTimeRemaining = Utilities.FormatTimeSpan(eta);
					}

					double currentJobRemainingWork = this.EncodeQueue[0].Cost - currentJobCompletedWork;

					this.currentJobEta =
						TimeSpan.FromSeconds(currentJobRemainingWork / overallWorkCompletionRate);
					this.EncodeQueue[0].Eta = this.currentJobEta;
				}
			}

			this.EncodeQueue[0].PercentComplete = (int)((currentJobCompletedWork / this.EncodeQueue[0].Cost) * 100.0);

			if (e.EstimatedTimeLeft >= TimeSpan.Zero)
			{
				this.CurrentFps = Math.Round(e.CurrentFrameRate, 1);
				this.AverageFps = Math.Round(e.AverageFrameRate, 1);
				this.EncodeSpeedDetailsAvailable = true;
			}
		}

		private void OnEncodeCompleted(object sender, EncodeCompletedEventArgs e)
		{
			DispatchService.Invoke(() =>
			{
				// Unregister from events. This instance may be used again.
				this.EncodeQueue[0].HandBrakeInstance.EncodeProgress -= this.OnEncodeProgress;
				this.EncodeQueue[0].HandBrakeInstance.EncodeCompleted -= this.OnEncodeCompleted;

				if (this.encodeStopped)
				{
					// If the encode was stopped manually
					this.StopEncodingAndReport();
					this.EncodeQueue[0].ReportEncodeEnd();

					if (this.totalTasks == 1)
					{
						this.EncodeQueue.Clear();
					}

					this.logger.Log("Encoding stopped");
				}
				else
				{
					// If the encode completed successfully
					this.completedQueueWork += this.EncodeQueue[0].Cost;

					var outputFileInfo = new FileInfo(this.CurrentJob.Job.OutputPath);
					bool succeeded = true;
					if (e.Error)
					{
						succeeded = false;
						this.logger.LogError("Encode failed. HandBrake reported an error.");
					}
					else if (this.errorLoggedDuringJob)
					{
						succeeded = false;
						this.logger.LogError("Encode failed. Error(s) were reported during the encode.");
					}
					else if (!outputFileInfo.Exists)
					{
						succeeded = false;
						this.logger.LogError("Encode failed. HandBrake reported no error but the expected output file was not found.");
					}
					else if (outputFileInfo.Length == 0)
					{
						succeeded = false;
						this.logger.LogError("Encode failed. HandBrake reported no error but the output file was empty.");
					}

					this.CompletedJobs.Add(new EncodeResultViewModel(
						new EncodeResult
						{
							Destination = this.CurrentJob.Job.OutputPath,
							Succeeded = succeeded,
							EncodeTime = this.CurrentJob.EncodeTime
						}));
					this.RaisePropertyChanged("CompletedItemsCount");
					this.RaisePropertyChanged("CompletedTabHeader");

					HandBrakeInstance finishedInstance = this.EncodeQueue[0].HandBrakeInstance;
					this.EncodeQueue.RemoveAt(0);
					this.RaisePropertyChanged("QueuedTabHeader");

					this.main.CleanupHandBrakeInstance(finishedInstance);

					this.logger.Log("Job completed");

					if (this.EncodeQueue.Count == 0)
					{
						this.SelectedTabIndex = CompletedTabIndex;
						this.StopEncodingAndReport();

						this.logger.Log("Queue completed");
						this.logger.Log("");

						Unity.Container.Resolve<TrayService>().ShowBalloonMessage("Encoding completed", "VidCoder has finished all encode jobs in the queue.");

						switch (this.EncodeCompleteAction)
						{
							case EncodeCompleteAction.DoNothing:
								break;
							case EncodeCompleteAction.Sleep:
								this.systemOperations.Sleep();
								break;
							case EncodeCompleteAction.LogOff:
								this.systemOperations.LogOff();
								break;
							case EncodeCompleteAction.Shutdown:
								this.systemOperations.ShutDown();
								break;
							default:
								throw new ArgumentOutOfRangeException();
						}
					}
					else
					{
						this.EncodeNextJob();
					}
				}
			});
		}

		private void PauseEncoding()
		{
			this.CurrentJob.HandBrakeInstance.PauseEncode();
			this.EncodeProgressState = TaskbarItemProgressState.Paused;
			this.CurrentJob.ReportEncodePause();

			this.Paused = true;
		}

		private void ResumeEncoding()
		{
			this.CurrentJob.HandBrakeInstance.ResumeEncode();
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
			var jobPersistGroup = new EncodeJobPersistGroup();
			foreach (EncodeJobViewModel jobVM in this.EncodeQueue)
			{
				jobPersistGroup.EncodeJobs.Add(new EncodeJobWithMetadata { Job = jobVM.Job, ManualOutputPath = jobVM.ManualOutputPath });
			}

			EncodeJobsPersist.EncodeJobs = jobPersistGroup;
		}

		private void RefreshCanEnqueue()
		{
			this.RaisePropertyChanged("CanEnqueueMultipleTitles");
			this.RaisePropertyChanged("CanEnqueue");

			this.AddToQueueCommand.RaiseCanExecuteChanged();
			this.QueueTitlesCommand.RaiseCanExecuteChanged();
		}

		private void AutoPauseEncoding(object sender, EventArgs e)
		{
			DispatchService.Invoke(() =>
			{
				if (this.Encoding && !this.Paused)
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
	}
}
