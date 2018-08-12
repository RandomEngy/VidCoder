﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Media;
using System.Reactive.Linq;
using System.Security;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shell;
using HandBrake.Interop.Interop.EventArgs;
using HandBrake.Interop.Interop.Json.Scan;
using Microsoft.AnyContainer;
using ReactiveUI;
using VidCoder.Extensions;
using VidCoder.Model;
using VidCoder.Resources;
using VidCoder.Services.Notifications;
using VidCoder.Services.Windows;
using VidCoder.ViewModel;
using VidCoder.ViewModel.DataModels;
using VidCoderCommon.Extensions;
using VidCoderCommon.Model;
using Color = System.Windows.Media.Color;

namespace VidCoder.Services
{
	/// <summary>
	/// Controls the queue and actual processing of encode jobs.
	/// </summary>
	public class ProcessingService : ReactiveObject
	{
		public const int QueuedTabIndex = 0;
		public const int CompletedTabIndex = 1;

		private const double StopWarningThresholdMinutes = 5;

		private IAppLogger logger = Resolver.Resolve<IAppLogger>();
		private IProcessAutoPause autoPause = Resolver.Resolve<IProcessAutoPause>();
		private ISystemOperations systemOperations = Resolver.Resolve<ISystemOperations>();
		private IMessageBoxService messageBoxService = Resolver.Resolve<IMessageBoxService>();
		private MainViewModel main = Resolver.Resolve<MainViewModel>();
		private OutputPathService outputVM = Resolver.Resolve<OutputPathService>();
		private PresetsService presetsService = Resolver.Resolve<PresetsService>();
		private PickersService pickersService = Resolver.Resolve<PickersService>();
		private IWindowManager windowManager = Resolver.Resolve<IWindowManager>();
		private IToastNotificationService toastNotificationService = Resolver.Resolve<IToastNotificationService>();
		private bool encoding;
		private bool paused;
		private EncodeCompleteReason encodeCompleteReason;
		private Stopwatch elapsedQueueEncodeTime;
		private long pollCount;
		private double completedQueueWork;
		private double totalQueueCost;
		private TimeSpan overallEtaSpan;
		private List<EncodeCompleteAction> encodeCompleteActions;
		private bool canShowEta;
		private double overallWorkCompletionRate;
		private bool profileEditedSinceLastQueue;

		public ProcessingService()
		{
			this.EncodeQueue = new ReactiveList<EncodeJobViewModel>();
			this.EncodingJobList = new ReactiveList<EncodeJobViewModel>();

			DispatchUtilities.BeginInvoke(() =>
			{
				IList<EncodeJobWithMetadata> jobs = EncodeJobStorage.EncodeJobs;
				foreach (EncodeJobWithMetadata job in jobs)
				{
					this.EncodeQueue.Add(new EncodeJobViewModel(job.Job) { SourceParentFolder = job.SourceParentFolder, ManualOutputPath = job.ManualOutputPath, NameFormatOverride = job.NameFormatOverride, PresetName = job.PresetName });
				}

				this.EncodeQueue.CollectionChanged +=
					(o, e) =>
					{
						this.SaveEncodeQueue();

						if (e.Action != NotifyCollectionChangedAction.Replace && e.Action != NotifyCollectionChangedAction.Move)
						{
							this.RefreshEncodeCompleteActions();
						}
					};
			});

			this.autoPause.PauseEncoding += this.AutoPauseEncoding;
			this.autoPause.ResumeEncoding += this.AutoResumeEncoding;

			this.presetsService.PresetChanged += (o, e) =>
			{
				this.profileEditedSinceLastQueue = true;
			};

			this.CompletedJobs = new ReactiveList<EncodeResultViewModel>();
			this.CompletedJobs.CollectionChanged +=
				(o, e) =>
				{
					if (e.Action != NotifyCollectionChangedAction.Replace && e.Action != NotifyCollectionChangedAction.Move)
					{
						this.RefreshEncodeCompleteActions();
					}
				};

			this.RefreshEncodeCompleteActions();

			// PauseVisible
			this.WhenAnyValue(x => x.Encoding, x => x.Paused, (encoding, paused) =>
			{
				return encoding && !paused;
			}).ToProperty(this, x => x.PauseVisible, out this.pauseVisible);

			// ProgressBarColor
			this.WhenAnyValue(x => x.Paused)
				.Select(paused =>
				{
					if (this.Paused)
					{
						return new SolidColorBrush(Color.FromRgb(255, 230, 0));
					}
					else
					{
						return new SolidColorBrush(Color.FromRgb(0, 200, 0));
					}
				}).ToProperty(this, x => x.ProgressBarColor, out this.progressBarColor);

			// OverallEncodeProgressPercent
			this.WhenAnyValue(x => x.OverallEncodeProgressFraction)
				.Select(progressFraction => progressFraction * 100)
				.ToProperty(this, x => x.OverallEncodeProgressPercent, out this.overallEncodeProgressPercent);

			var queueCountObservable = this.EncodeQueue.CountChanged.StartWith(this.EncodeQueue.Count);

			// QueuedTabHeader
			queueCountObservable
				.Select(count =>
				{
					if (count == 0)
					{
						return MainRes.Queued;
					}

					return string.Format(MainRes.QueuedWithTotal, count);
				}).ToProperty(this, x => x.QueuedTabHeader, out this.queuedTabHeader);

			// QueueHasItems
			queueCountObservable
				.Select(count =>
				{
					return count > 0;
				}).ToProperty(this, x => x.QueueHasItems, out this.queueHasItems);

			// EncodeButtonText
			this.WhenAnyValue(x => x.Encoding)
				.Select(encoding =>
				{
					if (encoding)
					{
						return MainRes.Resume;
					}
					else
					{
						return MainRes.Encode;
					}
				}).ToProperty(this, x => x.EncodeButtonText, out this.encodeButtonText);

			// CanTryEnqueueMultipleTitles
			this.main.WhenAnyValue(x => x.HasVideoSource, x => x.SourceData, (hasVideoSource, sourceData) =>
			{
				return hasVideoSource && sourceData != null && sourceData.Titles.Count > 1;
			}).ToProperty(this, x => x.CanTryEnqueueMultipleTitles, out this.canTryEnqueueMultipleTitles);

			var completedCountObservable = this.CompletedJobs.CountChanged.StartWith(this.CompletedJobs.Count);

			// CompletedItemsCount
			completedCountObservable.ToProperty(this, x => x.CompletedItemsCount, out this.completedItemsCount);

			// CompletedTabHeader
			completedCountObservable
				.Select(completedCount =>
				{
					return string.Format(MainRes.CompletedWithTotal, completedCount);
				}).ToProperty(this, x => x.CompletedTabHeader, out this.completedTabHeader);

			// JobsEncodingCount
			this.EncodingJobList.CountChanged.StartWith(0).ToProperty(this, x => x.JobsEncodingCount, out this.jobsEncodingCount);

			this.canPauseOrStopObservable = this.WhenAnyValue(x => x.CanPauseOrStop);

			if (Config.ResumeEncodingOnRestart && this.EncodeQueue.Count > 0)
			{
				DispatchUtilities.BeginInvoke(() =>
					{
						this.StartEncodeQueue();
					});
			}
		}

		public ReactiveList<EncodeJobViewModel> EncodeQueue { get; }

		public ReactiveList<EncodeJobViewModel> EncodingJobList { get; }

		public ReactiveList<EncodeResultViewModel> CompletedJobs { get; }

		private ObservableAsPropertyHelper<int> completedItemsCount;
		public int CompletedItemsCount => this.completedItemsCount.Value;

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

				this.RaisePropertyChanged();
			}
		}

		private ObservableAsPropertyHelper<bool> queueHasItems;
		public bool QueueHasItems => this.queueHasItems.Value;

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

				this.RaisePropertyChanged();
			}
		}

		private int totalTasks;
		public int TotalTasks
		{
			get { return this.totalTasks; }
			set { this.RaiseAndSetIfChanged(ref this.totalTasks, value); }
		}

		private int taskNumber;
		public int TaskNumber
		{
			get { return this.taskNumber; }
			set { this.RaiseAndSetIfChanged(ref this.taskNumber, value); }
		}

		private TimeSpan overallEncodeTime;
		public TimeSpan OverallEncodeTime
		{
			get { return this.overallEncodeTime; }
			set { this.RaiseAndSetIfChanged(ref this.overallEncodeTime, value); }
		}

		private TimeSpan overallEta;
		public TimeSpan OverallEta
		{
			get { return this.overallEta; }
			set { this.RaiseAndSetIfChanged(ref this.overallEta, value); }
		}

		private ObservableAsPropertyHelper<string> encodeButtonText;
		public string EncodeButtonText => this.encodeButtonText.Value;

		private ObservableAsPropertyHelper<bool> pauseVisible;
		public bool PauseVisible => this.pauseVisible.Value;

		private ObservableAsPropertyHelper<string> queuedTabHeader;
		public string QueuedTabHeader => this.queuedTabHeader.Value;

		private ObservableAsPropertyHelper<string> completedTabHeader;
		public string CompletedTabHeader => this.completedTabHeader.Value;

		private ObservableAsPropertyHelper<bool> canTryEnqueueMultipleTitles;
		public bool CanTryEnqueueMultipleTitles => this.canTryEnqueueMultipleTitles.Value;

		private ObservableAsPropertyHelper<int> jobsEncodingCount;
		public int JobsEncodingCount => this.jobsEncodingCount.Value;

		private bool encodeSpeedDetailsAvailable;
		public bool EncodeSpeedDetailsAvailable
		{
			get { return this.encodeSpeedDetailsAvailable; }
			set { this.RaiseAndSetIfChanged(ref this.encodeSpeedDetailsAvailable, value); }
		}

		private string estimatedTimeRemaining;
		public string EstimatedTimeRemaining
		{
			get { return this.estimatedTimeRemaining; }
			set { this.RaiseAndSetIfChanged(ref this.estimatedTimeRemaining, value); }
		}

		public List<EncodeCompleteAction> EncodeCompleteActions
		{
			get
			{
				return this.encodeCompleteActions;
			}
		} 

		private EncodeCompleteAction encodeCompleteAction;
		public EncodeCompleteAction EncodeCompleteAction
		{
			get { return this.encodeCompleteAction; }
			set { this.RaiseAndSetIfChanged(ref this.encodeCompleteAction, value); }
		}

		private double currentFps;
		public double CurrentFps
		{
			get { return this.currentFps; }
			set { this.RaiseAndSetIfChanged(ref this.currentFps, value); }
		}

		private double averageFps;
		public double AverageFps
		{
			get { return this.averageFps; }
			set { this.RaiseAndSetIfChanged(ref this.averageFps, value); }
		}

		private double overallEncodeProgressFraction;
		public double OverallEncodeProgressFraction
		{
			get { return this.overallEncodeProgressFraction; }
			set { this.RaiseAndSetIfChanged(ref this.overallEncodeProgressFraction, value); }
		}

		private ObservableAsPropertyHelper<double> overallEncodeProgressPercent;
		public double OverallEncodeProgressPercent => this.overallEncodeProgressPercent.Value;

		private TaskbarItemProgressState encodeProgressState;
		public TaskbarItemProgressState EncodeProgressState
		{
			get { return this.encodeProgressState; }
			set { this.RaiseAndSetIfChanged(ref this.encodeProgressState, value); }
		}

		private ObservableAsPropertyHelper<Brush> progressBarColor;
		public Brush ProgressBarColor => this.progressBarColor.Value;

		private int selectedTabIndex;
		public int SelectedTabIndex
		{
			get { return this.selectedTabIndex; }
			set { this.RaiseAndSetIfChanged(ref this.selectedTabIndex, value); }
		}

		private ReactiveCommand encode;
		public ICommand Encode
		{
			get
			{
				return this.encode ?? (this.encode = ReactiveCommand.Create(
					() =>
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
							else if (profileEditedSinceLastQueue)
							{
								// If the encoding profile has changed since the last time we queued an item, we'll prompt to apply the current
								// encoding profile to all queued items.

								var messageBoxService = Resolver.Resolve<IMessageBoxService>();
								MessageBoxResult result = messageBoxService.Show(
									this.main,
									MainRes.EncodingSettingsChangedMessage,
									MainRes.EncodingSettingsChangedTitle,
									MessageBoxButton.YesNo);

								if (result == MessageBoxResult.Yes)
								{
									var newJobs = new List<EncodeJobViewModel>();

									foreach (EncodeJobViewModel job in this.EncodeQueue)
									{
										VCProfile newProfile = this.presetsService.SelectedPreset.Preset.EncodingProfile;
										job.Job.EncodingProfile = newProfile;
										job.Job.OutputPath = Path.ChangeExtension(job.Job.OutputPath, OutputPathService.GetExtensionForProfile(newProfile));

										newJobs.Add(job);
									}

									// Clear out the queue and re-add the updated jobs so all the changes get reflected.
									this.EncodeQueue.Clear();
									foreach (var job in newJobs)
									{
										this.EncodeQueue.Add(job);
									}
								}
							}

							this.SelectedTabIndex = QueuedTabIndex;

							this.StartEncodeQueue();
						}
					},
					Observable.CombineLatest(
						this.EncodeQueue.CountChanged.StartWith(this.EncodeQueue.Count),
						this.main.WhenAnyValue(y => y.HasVideoSource),
						(queueCount, hasVideoSource) =>
						{
							return queueCount > 0 || hasVideoSource;
						})));
			}
		}

		private ReactiveCommand addToQueue;
		public ReactiveCommand AddToQueue
		{
			get
			{
				return this.addToQueue ?? (this.addToQueue = ReactiveCommand.Create(
					() =>
					{
						this.TryQueue();
					},
					this.main.WhenAnyValue(x => x.HasVideoSource)));
			}
		}

		private ReactiveCommand queueFiles;
		public ReactiveCommand QueueFiles
		{
			get
			{
				return this.queueFiles ?? (this.queueFiles = ReactiveCommand.Create(() =>
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

		private ReactiveCommand queueTitlesAction;
		public ReactiveCommand QueueTitlesAction
		{
			get
			{
				return this.queueTitlesAction ?? (this.queueTitlesAction = ReactiveCommand.Create(
					() =>
					{
						if (!this.EnsureDefaultOutputFolderSet())
						{
							return;
						}

						this.windowManager.OpenOrFocusWindow(typeof(QueueTitlesWindowViewModel));
					},
					this.WhenAnyValue(x => x.CanTryEnqueueMultipleTitles)));
			}
		}
		
		private ReactiveCommand pause;
		public ReactiveCommand Pause
		{
			get
			{
				return this.pause ?? (this.pause = ReactiveCommand.Create(
					() =>
					{
						this.PauseEncoding();
						this.autoPause.ReportPause();
					},
					this.canPauseOrStopObservable));
			}
		}

		private ReactiveCommand stopEncode;
		public ReactiveCommand StopEncode
		{
			get
			{
				return this.stopEncode ?? (this.stopEncode = ReactiveCommand.Create(
					() =>
					{
						if (this.EncodeQueue[0].EncodeTime > TimeSpan.FromMinutes(StopWarningThresholdMinutes))
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

						this.Stop(EncodeCompleteReason.Manual);
					},
					this.canPauseOrStopObservable));
			}
		}

		private ReactiveCommand moveSelectedJobsToTop;
		public ReactiveCommand MoveSelectedJobsToTop
		{
			get
			{
				return this.moveSelectedJobsToTop ?? (this.moveSelectedJobsToTop = ReactiveCommand.Create(() =>
				{
					List<EncodeJobViewModel> jobsToMove = this.main.SelectedJobs.Where(j => !j.Encoding).ToList();
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

		private ReactiveCommand moveSelectedJobsToBottom;
		public ReactiveCommand MoveSelectedJobsToBottom
		{
			get
			{
				return this.moveSelectedJobsToBottom ?? (this.moveSelectedJobsToBottom = ReactiveCommand.Create(() =>
				{
					List<EncodeJobViewModel> jobsToMove = this.main.SelectedJobs.Where(j => !j.Encoding).ToList();
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

		private ReactiveCommand importQueue;
		public ReactiveCommand ImportQueue
		{
			get
			{
				return this.importQueue ?? (this.importQueue = ReactiveCommand.Create(() =>
				{
					string presetFileName = FileService.Instance.GetFileNameLoad(
						null,
						MainRes.ImportQueueFilePickerTitle,
						CommonRes.QueueFileFilter + "|*.xml;*.vjqueue");
					if (presetFileName != null)
					{
						try
						{
							Resolver.Resolve<IQueueImportExport>().Import(presetFileName);
							this.messageBoxService.Show(MainRes.QueueImportSuccessMessage, CommonRes.Success, System.Windows.MessageBoxButton.OK);
						}
						catch (Exception)
						{
							this.messageBoxService.Show(MainRes.QueueImportErrorMessage, MainRes.ImportErrorTitle, System.Windows.MessageBoxButton.OK);
						}
					}
				}));
			}
		}

		private ReactiveCommand exportQueue;
		public ReactiveCommand ExportQueue
		{
			get
			{
				return this.exportQueue ?? (this.exportQueue = ReactiveCommand.Create(() =>
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

					Resolver.Resolve<IQueueImportExport>().Export(encodeJobs);
				}));
			}
		}

		private ReactiveCommand removeSelectedJobs;
		public ReactiveCommand RemoveSelectedJobs
		{
			get
			{
				return this.removeSelectedJobs ?? (this.removeSelectedJobs = ReactiveCommand.Create(() => { this.RemoveSelectedJobsImpl(); }));
			}
		}

		private void RemoveSelectedJobsImpl()
		{
			IList<EncodeJobViewModel> selectedJobs = this.main.SelectedJobs;

			foreach (EncodeJobViewModel selectedJob in selectedJobs)
			{
				if (!selectedJob.Encoding)
				{
					this.EncodeQueue.Remove(selectedJob);

					if (this.Encoding)
					{
						this.TotalTasks--;
						this.totalQueueCost -= selectedJob.Cost;
					}
				}
			}
		}

		private ReactiveCommand clearCompleted;
		public ReactiveCommand ClearCompleted
		{
			get
			{
				return this.clearCompleted ?? (this.clearCompleted = ReactiveCommand.Create(() =>
				{
					var removedItems = new List<EncodeResultViewModel>(this.CompletedJobs);
					this.CompletedJobs.Clear();
					var deletionCandidates = new List<string>();

					foreach (var removedItem in removedItems)
					{
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
										FileUtilities.DeleteDirectory(pathToDelete);
									}
								}
								catch (Exception exception)
								{
									Utilities.MessageBox.Show(string.Format(MainRes.CouldNotDeleteFile, pathToDelete, exception));
								}
							}
						}
					}
				}));
			}
		}

		/// <summary>
		/// Adds the given source to the encode queue.
		/// </summary>
		/// <param name="source">The path to the source file to encode.</param>
		/// <param name="destination">The destination path for the encoded file.</param>
		/// <param name="presetName">The name of the preset to use to encode.</param>
		/// <param name="pickerName">The name of the picker to use.</param>
		/// <returns>True if the item was successfully queued for processing.</returns>
		public void Process(string source, string destination, string presetName, string pickerName)
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

			VCProfile profile = this.presetsService.GetProfileByName(presetName);
			if (profile == null)
			{
				throw new ArgumentException("Cannot find preset: " + presetName);
			}

			PickerViewModel pickerVM = this.pickersService.Pickers.FirstOrDefault(p => p.Picker.Name == pickerName);
			Picker picker = null;
			if (pickerVM != null)
			{
				picker = pickerVM.Picker;
			}
			

			var scanMultipleDialog = new ScanMultipleDialogViewModel(new List<SourcePath> { new SourcePath { Path = source } });
			this.windowManager.OpenDialog(scanMultipleDialog);

			VideoSource videoSource = scanMultipleDialog.ScanResults[0].VideoSource;

		    List<int> titleNumbers;
            if (videoSource != null)
		    {
		        titleNumbers = this.PickTitles(videoSource, picker);
		    }
		    else
		    {
		        titleNumbers = new List<int>();
		    }

			foreach (int titleNumber in titleNumbers)
			{
				var jobVM = new EncodeJobViewModel(new VCJob
				{
					SourcePath = source,
					SourceType = Utilities.GetSourceType(source),
					Title = titleNumber,
					RangeType = VideoRangeType.All,
					EncodingProfile = profile,
					ChosenAudioTracks = new List<int> { 1 },
					OutputPath = destination,
					UseDefaultChapterNames = true,
				});

				jobVM.VideoSource = videoSource;
				jobVM.PresetName = presetName;
				jobVM.ManualOutputPath = !string.IsNullOrWhiteSpace(destination);

				VCJob job = jobVM.Job;

				SourceTitle title = jobVM.VideoSource.Titles.Single(t => t.Index == titleNumber);
				jobVM.Job.Length = title.Duration.ToSpan();

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
					string outputFolder = this.outputVM.GetOutputFolder(pathToQueue, null, picker);
					string outputFileName = this.outputVM.BuildOutputFileName(
						pathToQueue,
						Utilities.GetSourceName(pathToQueue),
						job.Title, 
						title.Duration.ToSpan(), 
						title.ChapterList.Count,
						multipleTitlesOnSource: videoSource.Titles.Count > 1,
						picker: picker);
					string outputExtension = this.outputVM.GetOutputExtension();
					string queueOutputPath = Path.Combine(outputFolder, outputFileName + outputExtension);
					queueOutputPath = this.outputVM.ResolveOutputPathConflicts(queueOutputPath, excludedPaths, isBatch: true);

					job.OutputPath = queueOutputPath;
				}

				this.Queue(jobVM);
			}

			this.logger.Log("Queued " + titleNumbers.Count + " titles from " + source);

			if (titleNumbers.Count > 0 && !this.Encoding)
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
		/// Queues the given Job. Assumed that the job has a populated Length.
		/// </summary>
		/// <param name="encodeJobVM">The job to add.</param>
		public void Queue(EncodeJobViewModel encodeJobVM)
		{
			if (this.Encoding)
			{
				this.TotalTasks++;
				this.totalQueueCost += encodeJobVM.Cost;
			}

			Picker picker = this.pickersService.SelectedPicker.Picker;
			if (picker.UseEncodingPreset && !string.IsNullOrEmpty(picker.EncodingPreset))
			{
				// Override the encoding preset
				var presetViewModel = this.presetsService.AllPresets.FirstOrDefault(p => p.Preset.Name == picker.EncodingPreset);
				if (presetViewModel != null)
				{
					encodeJobVM.Job.EncodingProfile = presetViewModel.Preset.EncodingProfile.Clone();
					encodeJobVM.PresetName = picker.EncodingPreset;
				}
			}

			this.EncodeQueue.Add(encodeJobVM);

			this.profileEditedSinceLastQueue = false;

			// Select the Queued tab.
			if (this.SelectedTabIndex != QueuedTabIndex)
			{
				this.SelectedTabIndex = QueuedTabIndex;
			}
		}

		public void QueueTitles(List<SourceTitle> titles, int titleStartOverride, string nameFormatOverride)
		{
			int currentTitleNumber = titleStartOverride;

			Picker picker = this.pickersService.SelectedPicker.Picker;

			// Queue the selected titles
			List<SourceTitle> titlesToQueue = titles;
			foreach (SourceTitle title in titlesToQueue)
			{
				VCProfile profile = this.presetsService.SelectedPreset.Preset.EncodingProfile;
				string queueSourceName = this.main.SourceName;
				if (this.main.SelectedSource.Type == SourceType.Disc)
				{
					queueSourceName = this.outputVM.TranslateDiscSourceName(queueSourceName);
				}

				int titleNumber = title.Index;
				if (titleStartOverride >= 0)
				{
					titleNumber = currentTitleNumber;
					currentTitleNumber++;
				}

				string outputDirectoryOverride = null;
				if (picker.OutputDirectoryOverrideEnabled)
				{
					outputDirectoryOverride = picker.OutputDirectoryOverride;
				}

				var job = new VCJob
				{
					SourceType = this.main.SelectedSource.Type,
					SourcePath = this.main.SourcePath,
					EncodingProfile = profile.Clone(),
					Title = title.Index,
					ChapterStart = 1,
					ChapterEnd = title.ChapterList.Count,
					UseDefaultChapterNames = true,
					Length = title.Duration.ToSpan()
				};

				this.AutoPickAudio(job, title, useCurrentContext: true);
				this.AutoPickSubtitles(job, title, useCurrentContext: true);

				string queueOutputFileName = this.outputVM.BuildOutputFileName(
					this.main.SourcePath,
					queueSourceName,
					titleNumber,
					title.Duration.ToSpan(),
					title.ChapterList.Count,
					nameFormatOverride,
					multipleTitlesOnSource: true);

				string extension = this.outputVM.GetOutputExtension();
				string queueOutputPath = this.outputVM.BuildOutputPath(queueOutputFileName, extension, sourcePath: null, outputFolder: outputDirectoryOverride);

				job.OutputPath = this.outputVM.ResolveOutputPathConflicts(queueOutputPath, isBatch: true);

				var jobVM = new EncodeJobViewModel(job)
				{
					VideoSource = this.main.SourceData,
					VideoSourceMetadata = this.main.GetVideoSourceMetadata(),
					ManualOutputPath = false,
					NameFormatOverride = nameFormatOverride,
					PresetName = this.presetsService.SelectedPreset.DisplayName
				};

				this.Queue(jobVM);
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

			List<SourcePath> sourcePathList = sourcePaths.ToList();
			//List<string> sourcePathStrings = sourcePathList.Select(p => p.Path).ToList();

			// This dialog will scan the items in the list, calculating length.
			var scanMultipleDialog = new ScanMultipleDialogViewModel(sourcePathList);
			this.windowManager.OpenDialog(scanMultipleDialog);

			if (scanMultipleDialog.CancelPending)
			{
				// Scan dialog was closed before it could complete. Abort.
				this.logger.Log("Batch scan cancelled. Aborting queue operation.");
				return;
			}

            List<ScanMultipleDialogViewModel.ScanResult> scanResults = scanMultipleDialog.ScanResults;

            var itemsToQueue = new List<EncodeJobViewModel>();
			var failedFiles = new List<string>();

			foreach (ScanMultipleDialogViewModel.ScanResult scanResult in scanResults)
			{
			    SourcePath sourcePath = scanResult.SourcePath;

			    if (scanResult.VideoSource?.Titles != null && scanResult.VideoSource.Titles.Count > 0)
			    {
			        VideoSource videoSource = scanResult.VideoSource;

			        List<int> titleNumbers = this.PickTitles(videoSource);

			        foreach (int titleNumber in titleNumbers)
			        {
			            var job = new VCJob
			            {
			                SourcePath = sourcePath.Path,
			                EncodingProfile = this.presetsService.SelectedPreset.Preset.EncodingProfile.Clone(),
			                Title = titleNumber,
			                RangeType = VideoRangeType.All,
			                UseDefaultChapterNames = true
			            };

			            if (sourcePath.SourceType == SourceType.None)
			            {
			                if (Directory.Exists(sourcePath.Path))
			                {
			                    job.SourceType = SourceType.DiscVideoFolder;
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
			                jobVM.VideoSource = videoSource;
			                jobVM.SourceParentFolder = sourcePath.ParentFolder;
			                jobVM.ManualOutputPath = false;
			                jobVM.PresetName = this.presetsService.SelectedPreset.DisplayName;
			                itemsToQueue.Add(jobVM);
			            }
			        }
			    }
			    else
			    {
                    // If the scan call failed outright or has no titles, mark as failed.
			        failedFiles.Add(sourcePath.Path);
			    }
			}

			foreach (EncodeJobViewModel jobVM in itemsToQueue)
			{
				var titles = jobVM.VideoSource.Titles;

				VCJob job = jobVM.Job;
				SourceTitle title = titles.Single(t => t.Index == job.Title);
				job.Length = title.Duration.ToSpan();

				// Choose the correct audio/subtitle tracks based on settings
				this.AutoPickAudio(job, title);
				this.AutoPickSubtitles(job, title);

				// Now that we have the title and subtitles we can determine the final output file name
				string fileToQueue = job.SourcePath;

				excludedPaths.Add(fileToQueue);
				string outputFolder = this.outputVM.GetOutputFolder(fileToQueue, jobVM.SourceParentFolder);
				string outputFileName = this.outputVM.BuildOutputFileName(
					fileToQueue, 
					Utilities.GetSourceNameFile(fileToQueue),
					job.Title, 
					title.Duration.ToSpan(),
					title.ChapterList.Count,
					multipleTitlesOnSource: titles.Count > 1);
				string outputExtension = this.outputVM.GetOutputExtension();
				string queueOutputPath = Path.Combine(outputFolder, outputFileName + outputExtension);
				queueOutputPath = this.outputVM.ResolveOutputPathConflicts(queueOutputPath, excludedPaths, isBatch: true);

				job.OutputPath = queueOutputPath;

				excludedPaths.Add(queueOutputPath);

				this.Queue(jobVM);
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

		public void RemoveQueueJobAndOthersIfSelected(EncodeJobViewModel job)
		{
			if (job.IsSelected)
			{
				// If job is selected, remove all jobs that are selected
				this.RemoveSelectedJobsImpl();
			}
			else
			{
				// If not, remove only this job.
				this.RemoveQueueJob(job);
			}
		}

		public void RemoveQueueJob(EncodeJobViewModel job)
		{
			this.EncodeQueue.Remove(job);

			if (this.Encoding)
			{
				this.TotalTasks--;
				this.totalQueueCost -= job.Cost;
			}
		}

		public void StartEncodeQueue()
		{
			this.EncodeProgressState = TaskbarItemProgressState.Normal;
			this.logger.Log("Starting queue");
			this.logger.ShowStatus(MainRes.StartedEncoding);

			this.TotalTasks = this.EncodeQueue.Count;
			this.TaskNumber = 0;
			this.canShowEta = false;
			this.overallWorkCompletionRate = 0;

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

			// If the encode is stopped we will change
			this.encodeCompleteReason = EncodeCompleteReason.Succeeded;
			this.autoPause.ReportStart();

			this.EncodeNextJobs();

			this.RebuildEncodingJobsList();

			// User had the window open when the encode ended last time, so we re-open when starting the queue again.
			if (Config.EncodeDetailsWindowOpen)
			{
				this.windowManager.OpenOrFocusWindow(typeof(EncodeDetailsWindowViewModel));
			}
		}

		private void RebuildEncodingJobsList()
		{
			this.EncodingJobList.Clear();
			foreach (var encodeJobViewModel in this.EncodeQueue.Where(j => j.Encoding))
			{
				this.EncodingJobList.Add(encodeJobViewModel);
			}
		}

		public HashSet<string> GetQueuedFiles()
		{
			return new HashSet<string>(this.EncodeQueue.Select(j => j.Job.OutputPath), StringComparer.OrdinalIgnoreCase);
		}

		private void RunForAllEncodeProxies(Action<IEncodeProxy> proxyAction)
		{
			foreach (var encodeProxy in this.EncodeQueue.Where(j => j.Encoding).Select(j => j.EncodeProxy))
			{
				if (encodeProxy != null)
				{
					proxyAction(encodeProxy);
				}
			}
		}

		private void RunForAllEncodingJobs(Action<EncodeJobViewModel> jobAction)
		{
			foreach (var jobViewModel in this.EncodeQueue.Where(j => j.Encoding))
			{
				jobAction(jobViewModel);
			}
		}

		public void Stop(EncodeCompleteReason reason)
		{
			if (reason == EncodeCompleteReason.Succeeded)
			{
				throw new ArgumentOutOfRangeException(nameof(reason));
			}

			this.encodeCompleteReason = reason;

			if (reason == EncodeCompleteReason.AppExit)
			{
				this.RunForAllEncodeProxies(encodeProxy => encodeProxy.StopAndWait());
			}
			else
			{
				this.RunForAllEncodeProxies(encodeProxy => encodeProxy.StopEncode());

				this.logger.ShowStatus(MainRes.StoppedEncoding);
			}
		}

		public IList<EncodeJobWithMetadata> GetQueueStorageJobs()
		{
			var jobs = new List<EncodeJobWithMetadata>();
			foreach (EncodeJobViewModel jobVM in this.EncodeQueue)
			{
				jobs.Add(
					new EncodeJobWithMetadata
					{
						Job = jobVM.Job,
						SourceParentFolder = jobVM.SourceParentFolder,
						ManualOutputPath = jobVM.ManualOutputPath,
						NameFormatOverride = jobVM.NameFormatOverride,
						PresetName = jobVM.PresetName
					});
			}

			return jobs;
		}

		private void EncodeNextJobs()
		{
			// Make sure the top N jobs are encoding.
			for (int i = 0; i < Config.MaxSimultaneousEncodes && i < this.EncodeQueue.Count; i++)
			{
				EncodeJobViewModel jobViewModel = this.EncodeQueue[i];

				if (!jobViewModel.Encoding)
				{
					this.TaskNumber++;
					this.StartEncode(jobViewModel);
				}
			}
		}

		private void StartEncode(EncodeJobViewModel jobViewModel)
		{
			VCJob job = jobViewModel.Job;

			var encodeLogger = new AppLogger(this.logger, Path.GetFileName(job.OutputPath));
			jobViewModel.Logger = encodeLogger;
			jobViewModel.EncodeSpeedDetailsAvailable = false;

			encodeLogger.Log("Starting job " + this.TaskNumber + "/" + this.TotalTasks);
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

			jobViewModel.EncodeProxy = Utilities.CreateEncodeProxy();
			jobViewModel.EncodeProxy.EncodeProgress += (sender, args) =>
			{
				this.OnEncodeProgress(jobViewModel, args);
			};
			jobViewModel.EncodeProxy.EncodeCompleted += (sender, args) =>
			{
				this.OnEncodeCompleted(jobViewModel, args.Error);
			};
			jobViewModel.EncodeProxy.EncodeStarted += this.OnEncodeStarted;

			string destinationDirectory = Path.GetDirectoryName(job.OutputPath);
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

			this.CanPauseOrStop = false;

			jobViewModel.ReportEncodeStart();

			if (!string.IsNullOrWhiteSpace(jobViewModel.DebugEncodeJsonOverride))
			{
				jobViewModel.EncodeProxy.StartEncode(jobViewModel.DebugEncodeJsonOverride, encodeLogger);
			}
			else
			{
				jobViewModel.EncodeProxy.StartEncode(job, encodeLogger, false, 0, 0, 0);
			}
		}

		private bool canPauseOrStop;
		private IObservable<bool> canPauseOrStopObservable;

		public bool CanPauseOrStop
		{
			get { return this.canPauseOrStop; }
			set { this.RaiseAndSetIfChanged(ref this.canPauseOrStop, value); }
		}

		private void OnEncodeStarted(object sender, EventArgs e)
		{
			DispatchUtilities.BeginInvoke(() =>
			{
				// After the encode has reported that it's started, we can now pause/stop it.
				this.CanPauseOrStop = true;
			});
		}

		private void OnEncodeProgress(EncodeJobViewModel jobViewModel, EncodeProgressEventArgs e)
		{
			if (this.EncodeQueue.Count == 0)
			{
				return;
			}

			VCJob job = jobViewModel.Job;
			double passCost = job.Length.TotalSeconds;
			double scanPassCost = passCost / EncodeJobViewModel.SubtitleScanCostFactor;
			double jobCompletedWork = 0.0;

			if (jobViewModel.SubtitleScan)
			{
				switch (e.PassId)
				{
					case -1:
						jobCompletedWork += scanPassCost * e.FractionComplete;
						break;
					case 0:
					case 1:
						jobCompletedWork += scanPassCost;
						jobCompletedWork += passCost * e.FractionComplete;
						break;
					case 2:
						jobCompletedWork += scanPassCost;
						jobCompletedWork += passCost;
						jobCompletedWork += passCost * e.FractionComplete;
						break;
					default:
						break;
				}
			}
			else
			{
				switch (e.PassId)
				{
					case 0:
					case 1:
						jobCompletedWork += passCost * e.FractionComplete;
						break;
					case 2:
						jobCompletedWork += passCost;
						jobCompletedWork += passCost * e.FractionComplete;
						break;
					default:
						break;
				}
			}

			jobViewModel.CompletedWork = jobCompletedWork;

			if (this.canShowEta)
			{
				double jobRemainingWork = jobViewModel.Cost - jobCompletedWork;

				if (this.overallWorkCompletionRate == 0)
				{
					jobViewModel.Eta = TimeSpan.MaxValue;
				}
				else
				{
					try
					{
						jobViewModel.Eta = TimeSpan.FromSeconds(jobRemainingWork / this.overallWorkCompletionRate);
					}
					catch (OverflowException)
					{
						jobViewModel.Eta = TimeSpan.MaxValue;
					}
				}
			}

			jobViewModel.FractionComplete = jobCompletedWork / jobViewModel.Cost;
			jobViewModel.CurrentPassId = e.PassId;
			jobViewModel.PassProgressFraction = e.FractionComplete;
			jobViewModel.RefreshEncodeTimeDisplay();

			try
			{
				var outputFileInfo = new FileInfo(jobViewModel.Job.OutputPath);
				jobViewModel.FileSizeBytes = outputFileInfo.Length;
			}
			catch (Exception)
			{
			}

			if (e.EstimatedTimeLeft >= TimeSpan.Zero)
			{
				jobViewModel.CurrentFps = Math.Round(e.CurrentFrameRate, 1);
				jobViewModel.AverageFps = Math.Round(e.AverageFrameRate, 1);
				jobViewModel.EncodeSpeedDetailsAvailable = true;
			}

			this.UpdateOverallEncodeProgress();
		}

		private void UpdateOverallEncodeProgress()
		{
			double inProgressJobsCompletedWork = 0;
			this.RunForAllEncodingJobs(job =>
			{
				inProgressJobsCompletedWork += job.CompletedWork;
			});

			double totalCompletedWork = this.completedQueueWork + inProgressJobsCompletedWork;

			this.OverallEncodeProgressFraction = this.totalQueueCost > 0 ? totalCompletedWork / this.totalQueueCost : 0;

			double queueElapsedSeconds = this.elapsedQueueEncodeTime.Elapsed.TotalSeconds;
			this.overallWorkCompletionRate = queueElapsedSeconds > 0 ? totalCompletedWork / queueElapsedSeconds : 0;

			// Only update encode time every 5th update.
			if (Interlocked.Increment(ref this.pollCount) % 5 == 1)
			{
				if (!this.canShowEta && this.elapsedQueueEncodeTime != null && queueElapsedSeconds > 0.5 && this.OverallEncodeProgressFraction != 0.0)
				{
					this.canShowEta = true;
				}

				if (this.canShowEta)
				{
					if (this.OverallEncodeProgressFraction == 1.0)
					{
						this.EstimatedTimeRemaining = Utilities.FormatTimeSpan(TimeSpan.Zero);
					}
					else
					{
						if (this.OverallEncodeProgressFraction == 0)
						{
							this.overallEtaSpan = TimeSpan.MaxValue;
						}
						else
						{
							try
							{
								this.overallEtaSpan =
									TimeSpan.FromSeconds((long)(((1.0 - this.OverallEncodeProgressFraction) * queueElapsedSeconds) / this.OverallEncodeProgressFraction));
							}
							catch (OverflowException)
							{
								this.overallEtaSpan = TimeSpan.MaxValue;
							}
						}

						this.EstimatedTimeRemaining = Utilities.FormatTimeSpan(this.overallEtaSpan);
					}
				}
			}

			double currentFps = 0;
			double averageFps = 0;

			foreach (var jobViewModel in this.EncodeQueue.Where(jobViewModel => jobViewModel.Encoding && jobViewModel.EncodeSpeedDetailsAvailable))
			{
				currentFps += jobViewModel.CurrentFps;
				averageFps += jobViewModel.AverageFps;
				this.EncodeSpeedDetailsAvailable = true;
			}

			if (this.EncodeSpeedDetailsAvailable)
			{
				this.CurrentFps = currentFps;
				this.AverageFps = averageFps;
			}

			this.OverallEncodeTime = this.elapsedQueueEncodeTime.Elapsed;
			this.OverallEta = this.overallEtaSpan;
		}

		private void OnEncodeCompleted(EncodeJobViewModel finishedJobViewModel, bool error)
		{
			DispatchUtilities.BeginInvoke(() =>
			{
				IAppLogger encodeLogger = finishedJobViewModel.Logger;
				string outputPath = finishedJobViewModel.Job.OutputPath;

				this.CanPauseOrStop = false;

				if (this.encodeCompleteReason != EncodeCompleteReason.Succeeded)
				{
					// If the encode was stopped manually
					this.StopEncodingAndReport();
					finishedJobViewModel.ReportEncodeEnd();

					if (this.TotalTasks == 1 && this.encodeCompleteReason == EncodeCompleteReason.Manual)
					{
						this.EncodeQueue.Clear();
					}

					encodeLogger.Log("Encoding stopped");
				}
				else
				{
					// If the encode completed successfully
					this.completedQueueWork += finishedJobViewModel.Cost;

					var outputFileInfo = new FileInfo(finishedJobViewModel.Job.OutputPath);
					long outputFileLength = 0;

					EncodeResultStatus status = EncodeResultStatus.Succeeded;
					if (error)
					{
						status = EncodeResultStatus.Failed;
						encodeLogger.LogError("Encode failed.");
					}
					else if (!outputFileInfo.Exists)
					{
						status = EncodeResultStatus.Failed;
						encodeLogger.LogError("Encode failed. HandBrake reported no error but the expected output file was not found.");
					}
					else
					{
						outputFileLength = outputFileInfo.Length;
						if (outputFileLength == 0)
						{
							status = EncodeResultStatus.Failed;
							encodeLogger.LogError("Encode failed. HandBrake reported no error but the output file was empty.");
						}
					}

					if (Config.PreserveModifyTimeFiles)
					{
						try
						{
							if (status != EncodeResultStatus.Failed && !FileUtilities.IsDirectory(finishedJobViewModel.Job.SourcePath))
							{
								FileInfo info = new FileInfo(finishedJobViewModel.Job.SourcePath);

								File.SetCreationTimeUtc(finishedJobViewModel.Job.OutputPath, info.CreationTimeUtc);
								File.SetLastWriteTimeUtc(finishedJobViewModel.Job.OutputPath, info.LastWriteTimeUtc);
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
							Destination = finishedJobViewModel.Job.OutputPath,
							Status = status,
							EncodeTime = finishedJobViewModel.EncodeTime,
							LogPath = encodeLogger.LogPath,
							SizeBytes = outputFileLength
						},
						finishedJobViewModel));

					this.EncodeQueue.Remove(finishedJobViewModel);

					var picker = this.pickersService.SelectedPicker.Picker;
					if (status == EncodeResultStatus.Succeeded && !Utilities.IsRunningAsAppx && picker.PostEncodeActionEnabled && !string.IsNullOrWhiteSpace(picker.PostEncodeExecutable))
					{
						string arguments = outputVM.ReplaceArguments(picker.PostEncodeArguments, picker)
                            .Replace("{file}", outputPath)
                            .Replace("{folder}", Path.GetDirectoryName(outputPath));

						var process = new ProcessStartInfo(
							picker.PostEncodeExecutable,
							arguments);
						System.Diagnostics.Process.Start(process);

						encodeLogger.Log($"Started post-encode action. Executable: {picker.PostEncodeExecutable} , Arguments: {arguments}");
					}

					encodeLogger.Log("Job completed (Elapsed Time: " + Utilities.FormatTimeSpan(finishedJobViewModel.EncodeTime) + ")");

					if (this.EncodeQueue.Count == 0)
					{
						this.SelectedTabIndex = CompletedTabIndex;
						this.StopEncodingAndReport();

						this.logger.Log("Queue completed");
						this.logger.ShowStatus(MainRes.EncodeCompleted);
						this.logger.Log("");

						if (!Utilities.IsInForeground)
						{
							Resolver.Resolve<TrayService>().ShowBalloonMessage(MainRes.EncodeCompleteBalloonTitle, MainRes.EncodeCompleteBalloonMessage);
							if (this.toastNotificationService.ToastEnabled)
							{
								const string toastFormat =
									"<?xml version=\"1.0\" encoding=\"utf-8\"?><toast><visual><binding template=\"ToastGeneric\"><text>{0}</text><text>{1}</text></binding></visual></toast>";

								string toastString = string.Format(toastFormat, SecurityElement.Escape(MainRes.EncodeCompleteBalloonTitle), SecurityElement.Escape(MainRes.EncodeCompleteBalloonMessage));

								this.toastNotificationService.Clear();
								this.toastNotificationService.ShowToast(toastString);
							}
						}

						EncodeCompleteActionType actionType = this.EncodeCompleteAction.ActionType;
						if (Config.PlaySoundOnCompletion &&
							actionType != EncodeCompleteActionType.CloseProgram && 
							actionType != EncodeCompleteActionType.Sleep && 
							actionType != EncodeCompleteActionType.LogOff &&
							actionType != EncodeCompleteActionType.Shutdown &&
							actionType != EncodeCompleteActionType.Hibernate)
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

						switch (actionType)
						{
							case EncodeCompleteActionType.DoNothing:
								break;
							case EncodeCompleteActionType.EjectDisc:
								this.systemOperations.Eject(this.EncodeCompleteAction.DriveLetter);
								break;
							case EncodeCompleteActionType.CloseProgram:
								if (this.CompletedJobs.All(job => job.EncodeResult.Succeeded))
								{
									this.windowManager.Close(this.main);
								}
								break;
							case EncodeCompleteActionType.Sleep:
							case EncodeCompleteActionType.LogOff:
							case EncodeCompleteActionType.Shutdown:
							case EncodeCompleteActionType.Hibernate:
								this.windowManager.OpenWindow(new ShutdownWarningWindowViewModel(actionType));
								break;
							default:
								throw new ArgumentOutOfRangeException();
						}
					}
					else
					{
						this.EncodeNextJobs();
					}
				}

				this.RebuildEncodingJobsList();

				if (this.encodeCompleteReason == EncodeCompleteReason.Manual || this.EncodeQueue.Count == 0)
				{
					this.windowManager.Close<EncodeDetailsWindowViewModel>(userInitiated: false);
				}

				string encodeLogPath = encodeLogger.LogPath;
				encodeLogger.Dispose();

				if (Config.CopyLogToOutputFolder && encodeLogPath != null)
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
			this.RunForAllEncodeProxies(encodeProxy => encodeProxy.PauseEncode());
			this.EncodeProgressState = TaskbarItemProgressState.Paused;
			this.RunForAllEncodingJobs(job => job.ReportEncodePause());

			this.Paused = true;
		}

		private void ResumeEncoding()
		{
			this.RunForAllEncodeProxies(encodeProxy => encodeProxy.ResumeEncode());
			this.EncodeProgressState = TaskbarItemProgressState.Normal;
			this.RunForAllEncodingJobs(job => job.ReportEncodeResume());

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
			EncodeJobStorage.EncodeJobs = this.GetQueueStorageJobs();
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
					new EncodeCompleteAction { ActionType = EncodeCompleteActionType.CloseProgram },
					new EncodeCompleteAction { ActionType = EncodeCompleteActionType.Sleep },
					new EncodeCompleteAction { ActionType = EncodeCompleteActionType.LogOff },
					new EncodeCompleteAction { ActionType = EncodeCompleteActionType.Hibernate },
					new EncodeCompleteAction { ActionType = EncodeCompleteActionType.Shutdown },
				};

			// Applicable drives to eject are those in the queue or completed items list
			var applicableDrives = new HashSet<string>();
			foreach (EncodeJobViewModel job in this.EncodeQueue)
			{
				if (job.Job.SourceType == SourceType.Disc)
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
				if (result.Job.Job.SourceType == SourceType.Disc)
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

			this.RaisePropertyChanged(nameof(this.EncodeCompleteActions));

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

			this.RaisePropertyChanged(nameof(this.EncodeCompleteAction));
		}

		private bool EnsureDefaultOutputFolderSet()
		{
			if (!string.IsNullOrEmpty(Config.AutoNameOutputFolder))
			{
				return true;
			}

			var messageService = Resolver.Resolve<IMessageBoxService>();
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

			return this.outputVM.PickDefaultOutputFolderImpl();
		}

		private bool EnsureValidOutputPath()
		{
			if (this.outputVM.PathIsValid())
			{
				return true;
			}

			Resolver.Resolve<IMessageBoxService>().Show(
				MainRes.OutputPathNotValidMessage,
				MainRes.OutputPathNotValidTitle, 
				MessageBoxButton.OK,
				MessageBoxImage.Error);

			return false;
		}

		/// <summary>
		/// Picks title numbers to encode from a video source.
		/// </summary>
		/// <param name="videoSource">The scanned instance.</param>
		/// <param name="picker">The picker to use to pick the titles.</param>
		/// <returns>List of title numbers (1-based)</returns>
		private List<int> PickTitles(VideoSource videoSource, Picker picker = null)
		{
			var result = new List<int>();
			if (picker == null)
			{
				picker = this.pickersService.SelectedPicker.Picker;
			}

			if (picker.TitleRangeSelectEnabled)
			{
				TimeSpan startDuration = TimeSpan.FromMinutes(picker.TitleRangeSelectStartMinutes);
				TimeSpan endDuration = TimeSpan.FromMinutes(picker.TitleRangeSelectEndMinutes);

				foreach (SourceTitle title in videoSource.Titles)
				{
					TimeSpan titleDuration = title.Duration.ToSpan();
					if (titleDuration >= startDuration && titleDuration <= endDuration)
					{
						result.Add(title.Index);
					}
				}
			}
			else if (videoSource.Titles.Count > 0)
			{
				SourceTitle titleToEncode = Utilities.GetFeatureTitle(videoSource.Titles, videoSource.FeatureTitle);
				result.Add(titleToEncode.Index);
			}

			return result;
		}

		private void AutoPauseEncoding(object sender, EventArgs e)
		{
			DispatchUtilities.Invoke(() =>
			{
				if (this.Encoding && !this.Paused)
				{
					this.PauseEncoding();
				}
			});
		}

		private void AutoResumeEncoding(object sender, EventArgs e)
		{
			DispatchUtilities.Invoke(() =>
			{
				if (this.Encoding && this.Paused)
				{
					this.ResumeEncoding();
				}
			});
		}

		// Automatically pick the correct audio on the given job.
		// Only relies on input from settings and the current title.
		private void AutoPickAudio(VCJob job, SourceTitle title, bool useCurrentContext = false)
		{
			Picker picker = this.pickersService.SelectedPicker.Picker;

			job.ChosenAudioTracks = new List<int>();
			switch (picker.AudioSelectionMode)
			{
				case AudioSelectionMode.Disabled:
					if (title.AudioList.Count > 0)
					{
						if (useCurrentContext)
						{
							// With previous context, pick similarly
							foreach (AudioTrackViewModel audioVM in this.main.AudioTracks.Where(t => t.Selected))
							{
								int audioIndex = audioVM.TrackIndex;

								if (title.AudioList.Count > audioIndex && this.main.SelectedTitle.AudioList[audioIndex].LanguageCode == title.AudioList[audioIndex].LanguageCode)
								{
									job.ChosenAudioTracks.Add(audioIndex + 1);
								}
							}

							// If we didn't manage to match any existing audio tracks, use the first audio track.
							if (job.ChosenAudioTracks.Count == 0)
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
				case AudioSelectionMode.First:
				case AudioSelectionMode.Language:
				case AudioSelectionMode.All:
					job.ChosenAudioTracks.AddRange(ChooseAudioTracks(title.AudioList, picker).Select(i => i + 1));

					break;
				default:
					throw new ArgumentOutOfRangeException();
			}

			// If none get chosen, pick the first one.
			if (job.ChosenAudioTracks.Count == 0 && title.AudioList.Count > 0)
			{
				job.ChosenAudioTracks.Add(1);
			}
		}

		/// <summary>
		/// Returns the 0-based track indices that should be included. Valid for all modes but Disabled.
		/// </summary>
		/// <param name="audioTracks">The audio tracks on the input video.</param>
		/// <param name="picker">The picker to use.</param>
		/// <returns>The 0-based track indices that should be included.</returns>
		public static IList<int> ChooseAudioTracks(IList<SourceAudioTrack> audioTracks, Picker picker)
		{
			var result = new List<int>();
			IList<int> chosenAudioTrackIndices;
			switch (picker.AudioSelectionMode)
			{
				case AudioSelectionMode.Disabled:
					throw new ArgumentException("Disabled is an invalid mode.");
				case AudioSelectionMode.First:
					if (audioTracks.Count > 0)
					{
						result.Add(0);
					}

					break;
				case AudioSelectionMode.Language:
					chosenAudioTrackIndices = ChooseAudioTracksFromLanguages(audioTracks, picker.AudioLanguageCodes, picker.AudioLanguageAll);
					result.AddRange(chosenAudioTrackIndices);

					break;
				case AudioSelectionMode.All:
					if (picker.AudioLanguageCodes != null && picker.AudioLanguageCodes.Count > 0)
					{
						// All tracks with certain languages first
						chosenAudioTrackIndices = ChooseAudioTracksFromLanguages(audioTracks, picker.AudioLanguageCodes, includeAllTracks: true);
						result.AddRange(chosenAudioTrackIndices);

						for (int i = 0; i < audioTracks.Count; i++)
						{
							if (!chosenAudioTrackIndices.Contains(i))
							{
								result.Add(i);
							}
						}
					}
					else
					{
						// All tracks, no ordering on language
						for (int i = 0; i < audioTracks.Count; i++)
						{
							result.Add(i);
						}
					}

					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(picker), picker, null);
			}

			// If none are chosen, add the first one.
			if (result.Count == 0 && audioTracks.Count > 0)
			{
				result.Add(0);
			}

			return result;
		}

		/// <summary>
		/// Returns the 0-based indices of tracks chosen.
		/// </summary>
		/// <param name="audioTracks">The list of audio tracks to look in.</param>
		/// <param name="languageCodes">The codes for the languages to include.</param>
		/// <param name="includeAllTracks">True if all tracks should be included rather than just the first.</param>
		/// <returns></returns>
		private static IList<int> ChooseAudioTracksFromLanguages(IList<SourceAudioTrack> audioTracks, IList<string> languageCodes, bool includeAllTracks)
		{
			var result = new List<int>();

			foreach (string code in languageCodes)
			{
				for (int i = 0; i < audioTracks.Count; i++)
				{
					SourceAudioTrack track = audioTracks[i];

					if (track.LanguageCode == code)
					{
						result.Add(i);

						if (!includeAllTracks)
						{
							break;
						}
					}
				}
			}

			return result;
		}

		// Automatically pick the correct subtitles on the given job.
		// Only relies on input from settings and the current title.
		private void AutoPickSubtitles(VCJob job, SourceTitle title, bool useCurrentContext = false)
		{
			Picker picker = this.pickersService.SelectedPicker.Picker;

			job.Subtitles = new VCSubtitles { SourceSubtitles = new List<SourceSubtitle>(), SrtSubtitles = new List<SrtSubtitle>() };
			switch (picker.SubtitleSelectionMode)
			{
				case SubtitleSelectionMode.Disabled:
					// Only pick subtitles when we have previous context.
					if (useCurrentContext)
					{
						foreach (SourceSubtitle sourceSubtitle in this.main.CurrentSubtitles.SourceSubtitles)
						{
							if (sourceSubtitle.TrackNumber == 0)
							{
								job.Subtitles.SourceSubtitles.Add(sourceSubtitle.Clone());
							}
							else if (title.SubtitleList.Count > sourceSubtitle.TrackNumber - 1 && this.main.SelectedTitle.SubtitleList[sourceSubtitle.TrackNumber - 1].LanguageCode == title.SubtitleList[sourceSubtitle.TrackNumber - 1].LanguageCode)
							{
								job.Subtitles.SourceSubtitles.Add(sourceSubtitle.Clone());
							}
						}
					}
					break;
				case SubtitleSelectionMode.None:
				case SubtitleSelectionMode.First:
				case SubtitleSelectionMode.ForeignAudioSearch:
				case SubtitleSelectionMode.Language:
				case SubtitleSelectionMode.All:
					job.Subtitles.SourceSubtitles.AddRange(ChooseSubtitles(
						title, 
						picker, 
						job.ChosenAudioTracks.Count > 0 ? job.ChosenAudioTracks[0] : -1));

					break;
				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		/// <summary>
		/// Returns all the source subtitles that should be included.
		/// </summary>
		/// <param name="title">The title to pick from.</param>
		/// <param name="picker">The picker to use.</param>
		/// <param name="chosenAudioTrack">The (1-based) main audio track currently selected, or -1 if no audio track is selected.</param>
		/// <returns></returns>
		public static IList<SourceSubtitle> ChooseSubtitles(SourceTitle title, Picker picker, int chosenAudioTrack)
		{
			var result = new List<SourceSubtitle>();

			IList<int> chosenSubtitleIndices;

			switch (picker.SubtitleSelectionMode)
			{
				case SubtitleSelectionMode.Disabled:
					throw new ArgumentException("Disabled is an invalid mode.");
				case SubtitleSelectionMode.None:
					break;
				case SubtitleSelectionMode.First:
					if (title.SubtitleList.Count > 0)
					{
						result.Add(new SourceSubtitle
						{
							TrackNumber = 1,
							BurnedIn = picker.SubtitleBurnIn,
							ForcedOnly = picker.SubtitleForcedOnly,
							Default = picker.SubtitleDefault
						});
					}
					
					break;
				case SubtitleSelectionMode.ForeignAudioSearch:
					result.Add(new SourceSubtitle
					{
						TrackNumber = 0,
						BurnedIn = picker.SubtitleBurnIn,
						ForcedOnly = picker.SubtitleForcedOnly,
						Default = picker.SubtitleDefault
					});

					break;
				case SubtitleSelectionMode.Language:
					chosenSubtitleIndices = ChooseSubtitlesFromLanguages(title, chosenAudioTrack, picker.SubtitleLanguageCodes, picker.SubtitleLanguageAll, picker.SubtitleLanguageOnlyIfDifferent);
					if (chosenSubtitleIndices.Count > 1)
					{
						// Multiple

						// First track
						result.Add(new SourceSubtitle
						{
							TrackNumber = chosenSubtitleIndices[0] + 1,
							BurnedIn = false,
							ForcedOnly = picker.SubtitleForcedOnly,
							Default = picker.SubtitleDefault
						});

						// The rest
						result.AddRange(chosenSubtitleIndices.Skip(1).Select(i => new SourceSubtitle
						{
							TrackNumber = i + 1,
							BurnedIn = false,
							ForcedOnly = picker.SubtitleForcedOnly,
							Default = false
						}));
					}
					else if (chosenSubtitleIndices.Count > 0)
					{
						// Single
						result.Add(new SourceSubtitle
						{
							TrackNumber = chosenSubtitleIndices[0] + 1,
							BurnedIn = picker.SubtitleBurnIn,
							ForcedOnly = picker.SubtitleForcedOnly,
							Default = picker.SubtitleDefault
						});
					}

					break;
				case SubtitleSelectionMode.All:
					if (picker.SubtitleLanguageCodes != null && picker.SubtitleLanguageCodes.Count > 0)
					{
						chosenSubtitleIndices = ChooseSubtitlesFromLanguages(title, chosenAudioTrack, picker.SubtitleLanguageCodes, includeAllTracks: true, onlyIfDifferentFromAudio: false);
						
						for (int i = 0; i < title.SubtitleList.Count; i++)
						{
							if (!chosenSubtitleIndices.Contains(i))
							{
								chosenSubtitleIndices.Add(i);
							}
						}
					}
					else
					{
						chosenSubtitleIndices = new List<int>();

						for (int i = 0; i < title.SubtitleList.Count; i++)
						{
							chosenSubtitleIndices.Add(i);
						}
					}

					if (chosenSubtitleIndices.Count > 0)
					{
						// First track
						result.Add(new SourceSubtitle
						{
							TrackNumber = chosenSubtitleIndices[0] + 1,
							BurnedIn = false,
							ForcedOnly = picker.SubtitleForcedOnly,
							Default = picker.SubtitleDefault
						});

						// The rest
						result.AddRange(chosenSubtitleIndices.Skip(1).Select(i => new SourceSubtitle
						{
							TrackNumber = i + 1,
							BurnedIn = false,
							ForcedOnly = picker.SubtitleForcedOnly,
							Default = false
						}));
					}

					break;
				default:
					throw new ArgumentOutOfRangeException();
			}

			return result;
		}

		private static IList<int> ChooseSubtitlesFromLanguages(SourceTitle title, int chosenAudioTrack, IList<string> languageCodes, bool includeAllTracks, bool onlyIfDifferentFromAudio)
		{
			var result = new List<int>();
			string audioLanguageCode = null;

			if (chosenAudioTrack > 0 && title.AudioList.Count > 0)
			{
				audioLanguageCode = title.AudioList[chosenAudioTrack - 1].LanguageCode;
			}

			foreach (string code in languageCodes)
			{
				if (!onlyIfDifferentFromAudio || code != audioLanguageCode)
				{
					for (int i = 0; i < title.SubtitleList.Count; i++)
					{
						SourceSubtitleTrack track = title.SubtitleList[i];

						if (track.LanguageCode == code)
						{
							result.Add(i);

							if (!includeAllTracks)
							{
								break;
							}
						}
					}
				}
			}

			return result;
		} 
	}
}
