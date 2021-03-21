using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Media;
using System.Reactive;
using System.Reactive.Linq;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shell;
using DynamicData;
using DynamicData.Aggregation;
using DynamicData.Binding;
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
using System.Reactive.Subjects;
using System.Text;
using HandBrake.Interop.Interop;
using HandBrake.Interop.Interop.Interfaces.EventArgs;
using Microsoft.WindowsAPICodePack.Shell;
using FileInfo = System.IO.FileInfo;

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

		private IAppLogger logger = StaticResolver.Resolve<IAppLogger>();
		private IAutoPause autoPause = StaticResolver.Resolve<IAutoPause>();
		private ISystemOperations systemOperations = StaticResolver.Resolve<ISystemOperations>();
		private IMessageBoxService messageBoxService = StaticResolver.Resolve<IMessageBoxService>();
		private MainViewModel main = StaticResolver.Resolve<MainViewModel>();
		private OutputPathService outputPathService = StaticResolver.Resolve<OutputPathService>();
		private PresetsService presetsService = StaticResolver.Resolve<PresetsService>();
		private PickersService pickersService = StaticResolver.Resolve<PickersService>();
		private IWindowManager windowManager = StaticResolver.Resolve<IWindowManager>();
		private IToastNotificationService toastNotificationService = StaticResolver.Resolve<IToastNotificationService>();
		private IAppThemeService appThemeService = StaticResolver.Resolve<IAppThemeService>();
		private bool paused;
		private EncodeCompleteReason encodeCompleteReason;
		private List<EncodeCompleteAction> encodeCompleteActions;
		private ScanMultipleDialogViewModel scanMultipleDialogViewModel;
		private bool selectedQueueItemModifyable = true;
		private BehaviorSubject<bool> selectedQueueItemModifyableSubject;
		private IDisposable simultaneousJobsSubscription;

		public ProcessingService()
		{
			IObservable<IChangeSet<EncodeJobViewModel>> encodeQueueObservable = this.EncodeQueue.Connect();
			encodeQueueObservable.Bind(this.EncodeQueueBindable).Subscribe();

			IObservable<IChangeSet<EncodeJobViewModel>> encodingJobsObservable = this.encodingJobList.Connect();
			encodingJobsObservable.Bind(this.EncodingJobListBindable).Subscribe();

			DispatchUtilities.BeginInvoke(() =>
			{
				IList<EncodeJobWithMetadata> jobs = EncodeJobStorage.EncodeJobs;

				this.EncodeQueue.Edit(encodeQueueInnerList =>
				{
					foreach (EncodeJobWithMetadata job in jobs)
					{
						encodeQueueInnerList.Add(new EncodeJobViewModel(job.Job)
						{
							SourceParentFolder = job.SourceParentFolder,
							ManualOutputPath = job.ManualOutputPath,
							NameFormatOverride = job.NameFormatOverride,
							PresetName = job.PresetName,
							VideoSource = job.VideoSource,
							VideoSourceMetadata = job.VideoSourceMetadata
						});
					}
				});

				encodeQueueObservable.Subscribe(changeSet =>
				{
					this.SaveEncodeQueue();
					if (changeSet.Adds > 0 || changeSet.Removes > 0)
					{
						this.RefreshEncodeCompleteActions();
					}
				});

				if (Config.ResumeEncodingOnRestart && this.EncodeQueue.Count > 0)
				{
					DispatchUtilities.BeginInvoke(() =>
					{
						this.StartEncodeQueue();
					});
				}
			});

			this.autoPause.PauseEncoding += this.AutoPauseEncoding;
			this.autoPause.ResumeEncoding += this.AutoResumeEncoding;

			this.presetsService.PresetChanged += (o, e) =>
			{
				if (this.EncodeQueue.Count > 0)
				{
					this.ShowApplyToQueueButton = true;
				}
			};



			this.completedJobs = new SourceList<EncodeResultViewModel>();
			IObservable<IChangeSet<EncodeResultViewModel>> completedJobsObservable = this.completedJobs.Connect();
			completedJobsObservable.Bind(this.CompletedJobsBindable).Subscribe();

			completedJobsObservable.Subscribe(changeSet =>
			{
				if (changeSet.Adds > 0 || changeSet.Removes > 0)
				{
					this.RefreshEncodeCompleteActions();
				}
			});

			this.RefreshEncodeCompleteActions();

			// PauseVisible
			this.WhenAnyValue(x => x.Encoding, x => x.Paused, (encoding, paused) =>
			{
				return encoding && !paused;
			}).ToProperty(this, x => x.PauseVisible, out this.pauseVisible);

			// ProgressBarColor
			IObservable<bool> pausedObservable = this.WhenAnyValue(x => x.Paused);
			IObservable<AppTheme> themeObservable = this.appThemeService.AppThemeObservable;
			Observable.CombineLatest(
				pausedObservable,
				themeObservable,
				(paused, theme) =>
				{
					if (paused)
					{
						return (Brush)Application.Current.Resources["ProgressBarPausedBrush"];
					}
					else
					{
						return (Brush)Application.Current.Resources["ProgressBarBrush"];
					}
				}).ToProperty(this, x => x.ProgressBarBrush, out this.progressBarBrush);

			// OverallEncodeProgressPercent
			this.WorkTracker.WhenAnyValue(x => x.OverallEncodeProgressFraction)
				.Select(progressFraction => progressFraction * 100)
				.ToProperty(this, x => x.OverallEncodeProgressPercent, out this.overallEncodeProgressPercent);

			this.QueueCountObservable = encodeQueueObservable.Count().StartWith(this.EncodeQueue.Count);

			this.QueueCountObservable.Subscribe(newCount =>
			{
				if (newCount == 0)
				{
					this.ShowApplyToQueueButton = false;
				}
			});

			this.presetsService.WhenAnyValue(x => x.SelectedPreset).Skip(1).Subscribe(_ =>
			{
				if (this.EncodeQueue.Count > 0)
				{
					this.ShowApplyToQueueButton = true;
				}
			});

			// QueuedTabHeader
			this.QueueCountObservable
				.Select(count =>
				{
					if (count == 0)
					{
						return MainRes.Queued;
					}

					return string.Format(MainRes.QueuedWithTotal, count);
				}).ToProperty(this, x => x.QueuedTabHeader, out this.queuedTabHeader);

			// QueueHasItems
			this.QueueCountObservable
				.Select(count =>
				{
					return count > 0;
				}).ToProperty(this, x => x.QueueHasItems, out this.queueHasItems);

			IObservable<bool> encodingObservable = this.WhenAnyValue(x => x.Encoding);

			// CanClearQueue
			Observable.CombineLatest(
				this.QueueCountObservable,
				encodingObservable,
				(queueCount, encoding) =>
				{
					return !encoding && queueCount > 0;
				}).ToProperty(this, x => x.CanClearQueue, out this.canClearQueue);

			// EncodeButtonText
			encodingObservable
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

			var completedCountObservable = completedJobsObservable.Count();

			// CompletedItemsCount
			completedCountObservable.ToProperty(this, x => x.CompletedItemsCount, out this.completedItemsCount);

			// CompletedTabHeader
			completedCountObservable
				.Select(completedCount =>
				{
					return string.Format(MainRes.CompletedWithTotal, completedCount);
				}).ToProperty(this, x => x.CompletedTabHeader, out this.completedTabHeader);

			// JobsEncodingCount
			this.EncodingJobsCountObservable = encodingJobsObservable.Count();
			this.EncodingJobsCountObservable.ToProperty(this, x => x.JobsEncodingCount, out this.jobsEncodingCount);

			this.simultaneousJobsSubscription = Config.Observables.MaxSimultaneousEncodes.Skip(1).Subscribe(maxSimultaneousEncodes =>
			{
				if (this.Encoding && maxSimultaneousEncodes > 1)
				{
					this.EncodeNextJobs();
				}
			});

			this.selectedQueueItemModifyableSubject = new BehaviorSubject<bool>(this.selectedQueueItemModifyable);
		}

		public QueueWorkTracker WorkTracker { get; } = new QueueWorkTracker();

		public SourceList<EncodeJobViewModel> EncodeQueue { get; } = new SourceList<EncodeJobViewModel>();

		public IObservable<int> QueueCountObservable { get; }

		public ObservableCollectionExtended<EncodeJobViewModel> EncodeQueueBindable { get; } = new ObservableCollectionExtended<EncodeJobViewModel>();

		private readonly SourceList<EncodeJobViewModel> encodingJobList = new SourceList<EncodeJobViewModel>();

		public IEnumerable<EncodeJobViewModel> EncodingJobs
		{
			get
			{
				return this.encodingJobList.Items;
			}
		}

		public IObservable<int> EncodingJobsCountObservable { get; }

		public ObservableCollectionExtended<EncodeJobViewModel> EncodingJobListBindable { get; } = new ObservableCollectionExtended<EncodeJobViewModel>();

		private readonly SourceList<EncodeResultViewModel> completedJobs;

		public ObservableCollectionExtended<EncodeResultViewModel> CompletedJobsBindable { get; } = new ObservableCollectionExtended<EncodeResultViewModel>();

		private ObservableAsPropertyHelper<int> completedItemsCount;
		public int CompletedItemsCount => this.completedItemsCount.Value;

		private bool encoding;
		public bool Encoding
		{
			get
			{
				return this.encoding;
			}

			set
			{
				this.encoding = value;
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

				if (value)
				{
					this.WorkTracker.ReportEncodePause();
				}
				else
				{
					this.WorkTracker.ReportEncodeResume();
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

		private bool showApplyToQueueButton;
		public bool ShowApplyToQueueButton
		{
			get { return this.showApplyToQueueButton; }
			set { this.RaiseAndSetIfChanged(ref this.showApplyToQueueButton, value); }
		}

		public IObservable<bool> SelectedQueueItemModifyableObservable => this.selectedQueueItemModifyableSubject;

		/// <summary>
		/// When the selected items change, update the subject if needed.
		/// </summary>
		public void OnSelectedQueueItemsChanged()
		{
			bool next = this.EncodeQueue.Items.Any(job => job.IsSelected && !job.Encoding);
			if (next != this.selectedQueueItemModifyable)
			{
				this.selectedQueueItemModifyableSubject.OnNext(next);
				this.selectedQueueItemModifyable = next;
			}
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

		private ObservableAsPropertyHelper<bool> canClearQueue;
		public bool CanClearQueue => this.canClearQueue.Value;

		private bool encodeSpeedDetailsAvailable;
		public bool EncodeSpeedDetailsAvailable
		{
			get { return this.encodeSpeedDetailsAvailable; }
			set { this.RaiseAndSetIfChanged(ref this.encodeSpeedDetailsAvailable, value); }
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

		private ObservableAsPropertyHelper<double> overallEncodeProgressPercent;
		public double OverallEncodeProgressPercent => this.overallEncodeProgressPercent.Value;

		private ObservableAsPropertyHelper<Brush> progressBarBrush;
		public Brush ProgressBarBrush => this.progressBarBrush.Value;

		private int selectedTabIndex;
		public int SelectedTabIndex
		{
			get { return this.selectedTabIndex; }
			set { this.RaiseAndSetIfChanged(ref this.selectedTabIndex, value); }
		}

		private ReactiveCommand<Unit, Unit> encode;
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
							if (this.EncodeQueue.Count == 0 && !this.TryQueue())
							{
								return;
							}

							this.SelectedTabIndex = QueuedTabIndex;

							this.StartEncodeQueue();
						}
					},
					Observable.CombineLatest(
						this.QueueCountObservable,
						this.main.WhenAnyValue(y => y.HasVideoSource),
						(queueCount, hasVideoSource) =>
						{
							return queueCount > 0 || hasVideoSource;
						})));
			}
		}

		private ReactiveCommand<Unit, Unit> addToQueue;
		public ICommand AddToQueue
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

		private ReactiveCommand<Unit, Unit> queueFiles;
		public ICommand QueueFiles
		{
			get
			{
				return this.queueFiles ?? (this.queueFiles = ReactiveCommand.Create(() =>
				{
					IList<string> fileNames = FileService.Instance.GetFileNames(Config.RememberPreviousFiles ? Config.LastInputFileFolder : null);
					if (fileNames != null && fileNames.Count > 0)
					{
						Config.LastInputFileFolder = Path.GetDirectoryName(fileNames[0]);

						this.QueueMultiple(fileNames);
					}
				}));
			}
		}

		private ReactiveCommand<Unit, Unit> queueTitlesAction;
		public ICommand QueueTitlesAction
		{
			get
			{
				return this.queueTitlesAction ?? (this.queueTitlesAction = ReactiveCommand.Create(
					() =>
					{
						this.windowManager.OpenOrFocusWindow<QueueTitlesWindowViewModel>();
					},
					this.WhenAnyValue(x => x.CanTryEnqueueMultipleTitles)));
			}
		}

		private ReactiveCommand<Unit, Unit> applyCurrentPresetToQueue;
		public ICommand ApplyCurrentPresetToQueue
		{
			get
			{
				return this.applyCurrentPresetToQueue ?? (this.applyCurrentPresetToQueue = ReactiveCommand.Create(() =>
				{
					if (this.EncodeQueue.Count > 0)
					{
						var newJobs = new List<EncodeJobViewModel>();

						Preset newPreset = this.presetsService.SelectedPreset.Preset;

						foreach (EncodeJobViewModel job in this.EncodeQueue.Items)
						{
							if (!job.Encoding)
							{
								this.ApplyPresetToJob(job, newPreset);
								newJobs.Add(job);
							}
						}

						if (this.encodingJobList.Count < this.EncodeQueue.Count)
						{
							// Clear out the queue and re-add the updated jobs so all the changes get reflected.
							this.EncodeQueue.Edit(encodeQueueInnerList =>
							{
								// Remove everything that's not encoding
								int encodingJobsCount = this.encodingJobList.Count;
								encodeQueueInnerList.RemoveRange(encodingJobsCount, encodeQueueInnerList.Count - encodingJobsCount);

								// Add the changed jobs back
								encodeQueueInnerList.AddRange(newJobs);
							});
						}
					}

					this.ShowApplyToQueueButton = false;
				}));
			}
		}

		private void ApplyPresetToJob(EncodeJobViewModel job, Preset newPreset)
		{
			if (this.Encoding)
			{
				this.WorkTracker.ReportRemovedFromQueue(job.Work);
			}

			VCProfile newProfile = newPreset.EncodingProfile;
			job.PresetName = newPreset.Name;
			job.Job.EncodingProfile = newProfile;
			job.Job.FinalOutputPath = Path.ChangeExtension(job.Job.FinalOutputPath, OutputPathService.GetExtensionForProfile(newProfile));
			job.CalculateWork();

			if (this.Encoding)
			{
				this.WorkTracker.ReportAddedToQueue(job.Work);
			}
		}

		private ReactiveCommand<Unit, Unit> pause;
		public ICommand Pause
		{
			get
			{
				return this.pause ?? (this.pause = ReactiveCommand.Create(
					() =>
					{
						this.PauseEncoding();
						this.autoPause.ReportPause();
					},
					this.canPauseOrStopSubject));
			}
		}

		private ReactiveCommand<Unit, Unit> stopEncode;
		public ICommand StopEncode
		{
			get
			{
				return this.stopEncode ?? (this.stopEncode = ReactiveCommand.Create(
					() =>
					{
						if (this.EncodeQueue.Items.First().EncodeTime > TimeSpan.FromMinutes(StopWarningThresholdMinutes))
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

						this.Stop();
					},
					this.canPauseOrStopSubject));
			}
		}

		private ReactiveCommand<Unit, Unit> moveSelectedJobsToTop;
		public ICommand MoveSelectedJobsToTop
		{
			get
			{
				return this.moveSelectedJobsToTop ?? (this.moveSelectedJobsToTop = ReactiveCommand.Create(
					() =>
					{
						IList<EncodeJobViewModel> jobsToMove = this.GetSelectedSortedNonEncodingJobs();
						if (jobsToMove.Count > 0)
						{
							this.EncodeQueue.Edit(encodeQueueInnerList =>
							{
								int insertPosition = 0;
								for (int i = 0; i < encodeQueueInnerList.Count; i++)
								{
									if (!encodeQueueInnerList[i].Encoding)
									{
										insertPosition = i;
										break;
									}
								}

								foreach (EncodeJobViewModel jobToMove in jobsToMove)
								{
									encodeQueueInnerList.Remove(jobToMove);
								}

								for (int i = jobsToMove.Count - 1; i >= 0; i--)
								{
									encodeQueueInnerList.Insert(insertPosition, jobsToMove[i]);
								}
							});
						}
					},
					this.SelectedQueueItemModifyableObservable));
			}
		}

		private ReactiveCommand<Unit, Unit> moveSelectedJobsToBottom;
		public ICommand MoveSelectedJobsToBottom
		{
			get
			{
				return this.moveSelectedJobsToBottom ?? (this.moveSelectedJobsToBottom = ReactiveCommand.Create(
					() =>
					{
						IList<EncodeJobViewModel> jobsToMove = this.GetSelectedSortedNonEncodingJobs();
						if (jobsToMove.Count > 0)
						{
							this.EncodeQueue.Edit(encodeQueueInnerList =>
							{
								foreach (EncodeJobViewModel jobToMove in jobsToMove)
								{
									encodeQueueInnerList.Remove(jobToMove);
								}

								foreach (EncodeJobViewModel jobToMove in jobsToMove)
								{
									encodeQueueInnerList.Add(jobToMove);
								}
							});
						}
					},
					this.SelectedQueueItemModifyableObservable));
			}
		}

		/// <summary>
		/// Gets a sorted list of the selected jobs in queue that are not currently encoding.
		/// </summary>
		/// <returns>A sorted list of the selected jobs in queue that are not currently encoding.</returns>
		private IList<EncodeJobViewModel> GetSelectedSortedNonEncodingJobs()
		{
			// This list may have items that are encoding and it may be misordered.
			IList<EncodeJobViewModel> rawSelectedJobs = this.main.SelectedJobs;

			// First get all of the desired jobs to select in a hash set
			var selectedJobsSet = new HashSet<EncodeJobViewModel>();
			foreach (EncodeJobViewModel selectedJob in rawSelectedJobs)
			{
				if (!selectedJob.Encoding)
				{
					selectedJobsSet.Add(selectedJob);
				}
			}

			// Now run down the list in order, adding items that match
			var result = new List<EncodeJobViewModel>();
			foreach (EncodeJobViewModel queueJob in this.EncodeQueue.Items)
			{
				if (selectedJobsSet.Contains(queueJob))
				{
					result.Add(queueJob);
				}
			}

			return result;
		}

		private ReactiveCommand<Unit, Unit> importQueue;
		public ICommand ImportQueue
		{
			get
			{
				return this.importQueue ?? (this.importQueue = ReactiveCommand.Create(() =>
				{
					string presetFileName = FileService.Instance.GetFileNameLoad(
						null,
						MainRes.ImportQueueFilePickerTitle,
						CommonRes.QueueFileFilter + "|*.vjqueue");
					if (presetFileName != null)
					{
						try
						{
							StaticResolver.Resolve<IQueueImportExport>().Import(presetFileName);
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

		private ReactiveCommand<Unit, Unit> exportQueue;
		public ICommand ExportQueue
		{
			get
			{
				return this.exportQueue ?? (this.exportQueue = ReactiveCommand.Create(() =>
				{
					var encodeJobs = new List<EncodeJobWithMetadata>();
					foreach (EncodeJobViewModel jobVM in this.EncodeQueue.Items)
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

					StaticResolver.Resolve<IQueueImportExport>().Export(encodeJobs);
				}));
			}
		}

		private ReactiveCommand<Unit, Unit> applyCurrentPresetToSelectedJobs;
		public ICommand ApplyCurrentPresetToSelectedJobs
		{
			get
			{
				return this.applyCurrentPresetToSelectedJobs ?? (this.applyCurrentPresetToSelectedJobs = ReactiveCommand.Create(
					() =>
					{
						IList<EncodeJobViewModel> selectedJobs = this.main.SelectedJobs;
						Preset newPreset = this.presetsService.SelectedPreset.Preset;


						this.EncodeQueue.Edit(encodeQueueInnerList =>
						{
							foreach (EncodeJobViewModel selectedJob in selectedJobs)
							{
								if (!selectedJob.Encoding)
								{
									int jobIndex = encodeQueueInnerList.IndexOf(selectedJob);
									encodeQueueInnerList.RemoveAt(jobIndex);

									this.ApplyPresetToJob(selectedJob, newPreset);

									encodeQueueInnerList.Insert(jobIndex, selectedJob);
								}
							}
						});
					},
					this.SelectedQueueItemModifyableObservable));
			}
		}

		private ReactiveCommand<Unit, Unit> removeSelectedJobs;
		public ICommand RemoveSelectedJobs
		{
			get
			{
				return this.removeSelectedJobs ?? (this.removeSelectedJobs = ReactiveCommand.Create(
					() => { this.RemoveSelectedJobsImpl(); },
					this.SelectedQueueItemModifyableObservable));
			}
		}

		private void RemoveSelectedJobsImpl()
		{
			IList<EncodeJobViewModel> selectedJobs = this.main.SelectedJobs;

			this.EncodeQueue.Edit(encodeQueueInnerList =>
			{
				foreach (EncodeJobViewModel selectedJob in selectedJobs)
				{
					if (!selectedJob.Encoding)
					{
						encodeQueueInnerList.Remove(selectedJob);

						if (this.Encoding)
						{
							this.TotalTasks--;
							this.WorkTracker.ReportRemovedFromQueue(selectedJob.Work);
						}
					}
				}
			});
		}

		private ReactiveCommand<Unit, Unit> removeAllJobs;
		public ICommand RemoveAllJobs
		{
			get
			{
				return this.removeAllJobs ?? (this.removeAllJobs = ReactiveCommand.Create(() =>
				{
					MessageBoxResult dialogResult = Utilities.MessageBox.Show(
						MainRes.ClearQueueConfirmationMessage,
						MainRes.DeleteSourceFilesConfirmationTitle,
						MessageBoxButton.OKCancel);

					if (dialogResult == MessageBoxResult.OK && !this.Encoding)
					{
						this.EncodeQueue.Clear();
					}
				}));
			}
		}

		private bool hasFailedItems;
		public bool HasFailedItems
		{
			get { return this.hasFailedItems; }
			set { this.RaiseAndSetIfChanged(ref this.hasFailedItems, value); }
		}

		private ReactiveCommand<Unit, Unit> retryFailed;
		public ICommand RetryFailed
		{
			get
			{
				return this.retryFailed ?? (this.retryFailed = ReactiveCommand.Create(() =>
				{
					int oldEncodeQueueCount = this.EncodeQueue.Count;

					var itemsToQueue = new List<EncodeJobViewModel>();
					this.completedJobs.Edit(completedJobsInnerList =>
					{
						for (int i = completedJobsInnerList.Count - 1; i >= 0; i--)
						{
							EncodeResultViewModel completedItem = completedJobsInnerList[i];
							if (!completedItem.EncodeResult.Succeeded)
							{
								itemsToQueue.Add(completedItem.Job);
								completedJobsInnerList.RemoveAt(i);
							}
						}
					});

					this.QueueMultiple(itemsToQueue);

					this.HasFailedItems = false;

					if (oldEncodeQueueCount == 0)
					{
						this.StartEncodeQueue();
					}
				}));
			}
		}

		private ReactiveCommand<Unit, Unit> clearCompleted;
		public ICommand ClearCompleted
		{
			get
			{
				return this.clearCompleted ?? (this.clearCompleted = ReactiveCommand.Create(() =>
				{
					var itemsToClear = new List<EncodeResultViewModel>(this.completedJobs.Items);
					List<string> deletionCandidates = this.GetDeletionCandidates(itemsToClear);

					if (this.PromptAndDeleteSourceFiles(deletionCandidates))
					{
						this.completedJobs.Clear();
						this.HasFailedItems = false;
					}
				}));
			}
		}

		private ReactiveCommand<Unit, Unit> clearFailed;
		public ICommand ClearFailed
		{
			get
			{
				return this.clearFailed ?? (this.clearFailed = ReactiveCommand.Create(() =>
				{
					this.ClearCompletedItems(encodeResultViewModel => !encodeResultViewModel.EncodeResult.Succeeded);
					this.HasFailedItems = false;
				}));
			}
		}

		private ReactiveCommand<Unit, Unit> clearSucceeded;
		public ICommand ClearSucceeded
		{
			get
			{
				return this.clearSucceeded ?? (this.clearSucceeded = ReactiveCommand.Create(() =>
				{
					var itemsToClear = this.completedJobs.Items.Where(completedJob => completedJob.EncodeResult.Succeeded);
					List<string> deletionCandidates = this.GetDeletionCandidates(itemsToClear);

					if (this.PromptAndDeleteSourceFiles(deletionCandidates))
					{
						this.ClearCompletedItems(encodeResultViewModel => encodeResultViewModel.EncodeResult.Succeeded);
					}
				}));
			}
		}

		/// <summary>
		/// Gets candidate files for deletion given the completed items being cleared.
		/// </summary>
		/// <param name="itemsToClear">The completed items being cleared.</param>
		/// <returns>The list of items to delete.</returns>
		private List<string> GetDeletionCandidates(IEnumerable<EncodeResultViewModel> itemsToClear)
		{
			var deletionCandidates = new List<string>();

			if (Config.DeleteSourceFilesOnClearingCompleted)
			{
				int totalItems = 0;
				int failedItems = 0;
				int notExistItems = 0;
				int readOnlyItems = 0;
				int itemsInEncodeQueue = 0;
				int itemsCurrentlyScanned = 0;

				foreach (var itemToClear in itemsToClear)
				{
					totalItems++;

					// Mark for deletion if item succeeded
					if (itemToClear.EncodeResult.Succeeded)
					{
						// And if file exists and is not read-only
						string sourcePath = itemToClear.Job.Job.SourcePath;
						var fileInfo = new FileInfo(sourcePath);
						var directoryInfo = new DirectoryInfo(sourcePath);

						if (fileInfo.Exists || directoryInfo.Exists)
						{
							if (fileInfo.Exists && !fileInfo.IsReadOnly || directoryInfo.Exists && !directoryInfo.Attributes.HasFlag(FileAttributes.ReadOnly))
							{
								// And if it's not currently scanned or in the encode queue
								bool sourceInEncodeQueue = this.EncodeQueue.Items.Any(job => string.Compare(job.Job.SourcePath, sourcePath, StringComparison.OrdinalIgnoreCase) == 0);
								if (!sourceInEncodeQueue)
								{
									if (!this.main.HasVideoSource || string.Compare(this.main.SourcePath, sourcePath, StringComparison.OrdinalIgnoreCase) != 0)
									{
										deletionCandidates.Add(sourcePath);
									}
									else
									{
										itemsCurrentlyScanned++;
									}
								}
								else
								{
									itemsInEncodeQueue++;
								}
							}
							else
							{
								readOnlyItems++;
							}
						}
						else
						{
							notExistItems++;
						}
					}
					else
					{
						failedItems++;
					}
				}

				var builder = new StringBuilder();
				builder.AppendLine("Prepared candidates for deletion");
				builder.AppendLine("Total: " + totalItems);
				builder.Append("Eligible deletion candidates: " + deletionCandidates.Count);
				if (failedItems > 0)
				{
					builder.AppendLine();
					builder.Append("Skipped due to failed status: " + failedItems);
				}

				if (notExistItems > 0)
				{
					builder.AppendLine();
					builder.Append("Skipped due to file(s) no longer existing: " + notExistItems);
				}

				if (readOnlyItems > 0)
				{
					builder.AppendLine();
					builder.Append("Skipped due to file(s) being read only: " + readOnlyItems);
				}

				if (itemsInEncodeQueue > 0)
				{
					builder.AppendLine();
					builder.Append("Skipped due to file(s) existing in encode queue: " + itemsInEncodeQueue);
				}

				if (itemsCurrentlyScanned > 0)
				{
					builder.AppendLine();
					builder.Append("Skipped due to file being currently scanned: " + itemsCurrentlyScanned);
				}

				this.logger.Log(builder.ToString());
			}

			return deletionCandidates;
		}

		/// <summary>
		/// Prompts the user to delete the given source files, and deletes them if the user answers yes.
		/// </summary>
		/// <param name="deletionCandidates">The files to prompt to delete</param>
		/// <returns>True if the files should be cleared from the list.</returns>
		private bool PromptAndDeleteSourceFiles(IList<string> deletionCandidates)
		{
			bool clearItems = true;
			if (deletionCandidates.Count > 0)
			{
				switch (CustomConfig.DeleteSourceFilesMode)
				{
					case DeleteSourceFilesMode.DeleteWithConfirmation:
						MessageBoxResult dialogResult = Utilities.MessageBox.Show(
							string.Format(MainRes.DeleteSourceFilesConfirmationMessage, deletionCandidates.Count),
							MainRes.DeleteSourceFilesConfirmationTitle,
							MessageBoxButton.YesNoCancel);
						if (dialogResult == MessageBoxResult.Yes)
						{
							int filesDeleted = 0;
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

									filesDeleted++;
								}
								catch (Exception exception)
								{
									Utilities.MessageBox.Show(string.Format(MainRes.CouldNotDeleteFile, pathToDelete, exception));
								}
							}

							this.logger.Log($"Deleted {filesDeleted} source video(s).");
						}
						else if (dialogResult == MessageBoxResult.Cancel)
						{
							clearItems = false;
						}
						break;
					case DeleteSourceFilesMode.Recycle:
					default:
						try
						{
							int result = FileOperationApiWrapper.SendToRecycle(deletionCandidates);
							if (result > 0)
							{
								Utilities.MessageBox.Show(MainRes.CouldNotRecycleFile);
								this.logger.LogError("Could not send files to recycle bin: Error code 0x" + result.ToString("X2"));
							}
							else
							{
								this.logger.Log($"Sent {deletionCandidates.Count} source video(s) to Recycle Bin");
							}
						}
						catch (Exception exception)
						{
							Utilities.MessageBox.Show(MainRes.CouldNotRecycleFile);
							this.logger.LogError("Could not recycle files: " + exception.ToString());
						}

						break;
				}
			}

			return clearItems;
		}

		/// <summary>
		/// Clears the completed items that match the given selector.
		/// </summary>
		/// <param name="selector">The selector to test the items.</param>
		private void ClearCompletedItems(Func<EncodeResultViewModel, bool> selector)
		{
			this.completedJobs.Edit(completedJobsInnerList =>
			{
				for (int i = completedJobsInnerList.Count - 1; i >= 0; i--)
				{
					EncodeResultViewModel completedItem = completedJobsInnerList[i];
					if (selector(completedItem))
					{
						completedJobsInnerList.RemoveAt(i);
					}
				}
			});
		}

		/// <summary>
		/// Adds the given source to the encode queue.
		/// </summary>
		/// <param name="source">The path to the source file to encode.</param>
		/// <param name="destination">The destination path for the encoded file.</param>
		/// <param name="presetName">The name of the preset to use to encode.</param>
		/// <param name="pickerName">The name of the picker to use. Will use default picker if null.</param>
		/// <returns>True if the item was successfully queued for processing.</returns>
		public void Process(string source, string destination, string presetName, string pickerName)
		{
			if (string.IsNullOrWhiteSpace(source))
			{
				throw new ArgumentException("source cannot be null or empty.");
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
			else
			{
				picker = pickersService.Pickers[0].Picker;
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
					AudioTracks = new List<ChosenAudioTrack> { new ChosenAudioTrack { TrackNumber = 1 } },
					FinalOutputPath = destination,
					UseDefaultChapterNames = true,
				});

				jobVM.VideoSource = videoSource;
				jobVM.PresetName = presetName;
				jobVM.ManualOutputPath = !string.IsNullOrWhiteSpace(destination);

				VCJob job = jobVM.Job;

				SourceTitle title = jobVM.VideoSource.Titles.Single(t => t.Index == titleNumber);
				jobVM.Job.Length = title.Duration.ToSpan();

				// Choose the correct range based on picker settings
				this.AutoPickRange(job, title);

				// Choose the correct audio/subtitle tracks based on settings
				this.AutoPickAudio(job, title);
				this.AutoPickSubtitles(job, title);

				// Now that we have the title and subtitles we can determine the final output file name
				if (string.IsNullOrWhiteSpace(destination))
				{
					// Exclude all current queued files if overwrite is disabled
					HashSet<string> excludedPaths;
					if (picker.WhenFileExistsBatch == WhenFileExists.AutoRename)
					{
						excludedPaths = this.GetQueuedFiles();
					}
					else
					{
						excludedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
					}

					string pathToQueue = job.SourcePath;

					excludedPaths.Add(pathToQueue);
					string outputFolder = this.outputPathService.GetOutputFolder(pathToQueue, null, picker);
					string outputFileName = this.outputPathService.BuildOutputFileName(
						pathToQueue,
						this.outputPathService.CleanUpSourceName(picker, Utilities.GetSourceName(pathToQueue)),
						job.Title,
						title.Duration.ToSpan(),
						title.ChapterList.Count,
						multipleTitlesOnSource: videoSource.Titles.Count > 1,
						picker: picker);
					string outputExtension = this.outputPathService.GetOutputExtension();
					string queueOutputPath = Path.Combine(outputFolder, outputFileName + outputExtension);
					queueOutputPath = this.outputPathService.ResolveOutputPathConflicts(queueOutputPath, source, excludedPaths, isBatch: true, picker: picker, allowConflictDialog: false, allowQueueRemoval: true);

					job.FinalOutputPath = queueOutputPath;
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
			if (!this.EnsureValidOutputPath())
			{
				return false;
			}

			var newEncodeJobVM = this.main.CreateEncodeJobVM();

			Picker picker = this.pickersService.SelectedPicker.Picker;
			string resolvedOutputPath = this.outputPathService.ResolveOutputPathConflicts(newEncodeJobVM.Job.FinalOutputPath, newEncodeJobVM.Job.SourcePath, isBatch: false, picker, allowConflictDialog: true, allowQueueRemoval: true);
			if (resolvedOutputPath == null)
			{
				return false;
			}

			newEncodeJobVM.Job.FinalOutputPath = resolvedOutputPath;

			this.Queue(newEncodeJobVM);
			return true;
		}

		/// <summary>
		/// Queues the given Job. Assumed that the job has a populated Length.
		/// </summary>
		/// <param name="encodeJobViewModel">The job to add.</param>
		public void Queue(EncodeJobViewModel encodeJobViewModel)
		{
			this.QueueMultiple(new[] { encodeJobViewModel });
		}

		public void QueueTitles(List<SourceTitle> titles, int titleStartOverride, string nameFormatOverride)
		{
			int currentTitleNumber = titleStartOverride;

			Picker picker = this.pickersService.SelectedPicker.Picker;

			// Queue the selected titles
			var jobsToAdd = new List<EncodeJobViewModel>();
			foreach (SourceTitle title in titles)
			{
				VCProfile profile = this.presetsService.SelectedPreset.Preset.EncodingProfile;
				string queueSourceName = this.outputPathService.CleanUpSourceName(picker);

				int titleNumber = title.Index;
				if (titleStartOverride >= 0)
				{
					titleNumber = currentTitleNumber;
					currentTitleNumber++;
				}

				string outputFolder = this.outputPathService.GetOutputFolder(this.main.SourcePath, null, picker);

				var job = new VCJob
				{
					SourceType = this.main.SelectedSource.Type,
					SourcePath = this.main.SourcePath,
					EncodingProfile = profile.Clone(),
					Title = title.Index,
					ChapterStart = 1,
					ChapterEnd = title.ChapterList.Count,
					UseDefaultChapterNames = true
				};

				this.AutoPickRange(job, title);

				this.AutoPickAudio(job, title, useCurrentContext: true);
				this.AutoPickSubtitles(job, title, useCurrentContext: true);

				string queueOutputFileName = this.outputPathService.BuildOutputFileName(
					this.main.SourcePath,
					queueSourceName,
					titleNumber,
					title.Duration.ToSpan(),
					title.ChapterList.Count,
					nameFormatOverride,
					multipleTitlesOnSource: true);

				string extension = this.outputPathService.GetOutputExtension();
				string queueOutputPath = this.outputPathService.BuildOutputPath(queueOutputFileName, extension, sourcePath: null, outputFolder: outputFolder);

				job.FinalOutputPath = this.outputPathService.ResolveOutputPathConflicts(queueOutputPath, this.main.SourcePath, isBatch: true, picker, allowConflictDialog: false, allowQueueRemoval: true);

				var jobVM = new EncodeJobViewModel(job)
				{
					VideoSource = this.main.SourceData,
					VideoSourceMetadata = this.main.GetVideoSourceMetadata(),
					ManualOutputPath = false,
					NameFormatOverride = nameFormatOverride,
					PresetName = this.presetsService.SelectedPreset.DisplayName
				};

				jobsToAdd.Add(jobVM);
			}

			this.QueueMultiple(jobsToAdd);
		}

		private void RetryJobIfNeeded(EncodeJobViewModel encodeJobViewModel)
		{
			if (encodeJobViewModel.FailedTries < Config.EncodeRetries)
			{
				encodeJobViewModel.FailedTries++;

				int encodingItemCount = this.EncodeQueue.Items.Count(i => i.Encoding);
				this.EncodeQueue.Insert(encodingItemCount, encodeJobViewModel);

				if (this.Encoding)
				{
					this.TotalTasks++;
				}
			}
		}

		public void QueueMultiple(IEnumerable<EncodeJobViewModel> encodeJobViewModels, bool allowPickerProfileOverride = true)
		{
			var encodeJobList = encodeJobViewModels.ToList();
			if (encodeJobList.Count == 0)
			{
				return;
			}

			if (this.Encoding)
			{
				this.TotalTasks += encodeJobList.Count;
			}

			Picker picker = this.pickersService.SelectedPicker.Picker;
			PresetViewModel overridePresetViewModel = null;
			if (picker.UseEncodingPreset && !string.IsNullOrEmpty(picker.EncodingPreset) && allowPickerProfileOverride)
			{
				// Override the encoding preset
				overridePresetViewModel = this.presetsService.AllPresets.FirstOrDefault(p => p.Preset.Name == picker.EncodingPreset);
			}

			this.EncodeQueue.Edit(encodeQueueInnerList =>
			{
				foreach (var encodeJobViewModel in encodeJobList)
				{
					if (this.Encoding)
					{
						this.WorkTracker.ReportAddedToQueue(encodeJobViewModel.Work);
					}

					if (overridePresetViewModel != null)
					{
						encodeJobViewModel.Job.EncodingProfile = overridePresetViewModel.Preset.EncodingProfile.Clone();
						encodeJobViewModel.PresetName = picker.EncodingPreset;
					}

					encodeQueueInnerList.Add(encodeJobViewModel);
				}
			});

			// Select the Queued tab.
			if (this.SelectedTabIndex != QueuedTabIndex)
			{
				this.SelectedTabIndex = QueuedTabIndex;
			}

			// After adding new items, immediately start them
			if (this.Encoding && Config.MaxSimultaneousEncodes > 1 && !this.Paused)
			{
				this.EncodeNextJobs();
			}
		}

		public void QueueMultiple(IEnumerable<string> pathsToQueue)
		{
			this.QueueMultiple(pathsToQueue.Select(p => new SourcePath { Path = p }));
		}

		// Queues a list of files or video folders.
		public void QueueMultiple(IEnumerable<SourcePath> sourcePaths)
		{
			Picker picker = this.pickersService.SelectedPicker.Picker;

			// Exclude all current queued files if overwrite is disabled
			HashSet<string> excludedPaths;
			if (picker.WhenFileExistsBatch == WhenFileExists.AutoRename)
			{
				excludedPaths = this.GetQueuedFiles();
			}
			else
			{
				excludedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			}

			List<SourcePath> sourcePathList = sourcePaths.ToList();

			if (this.scanMultipleDialogViewModel != null)
			{
				if (this.scanMultipleDialogViewModel.TryAddScanPaths(sourcePathList))
				{
					return;
				}
			}

			// This dialog will scan the items in the list, calculating length.
			this.scanMultipleDialogViewModel = new ScanMultipleDialogViewModel(sourcePathList);
			this.windowManager.OpenDialog(this.scanMultipleDialogViewModel);

			if (this.scanMultipleDialogViewModel.CancelPending)
			{
				// Scan dialog was closed before it could complete. Abort.
				this.logger.Log("Batch scan canceled. Aborting queue operation.");
				this.scanMultipleDialogViewModel = null;
				return;
			}

			List<ScanMultipleDialogViewModel.ScanResult> scanResults = this.scanMultipleDialogViewModel.ScanResults;
			this.scanMultipleDialogViewModel = null;

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

			foreach (EncodeJobViewModel jobViewModel in itemsToQueue)
			{
				var titles = jobViewModel.VideoSource.Titles;

				VCJob job = jobViewModel.Job;
				SourceTitle title = titles.Single(t => t.Index == job.Title);
				job.Length = title.Duration.ToSpan();

				// Choose the correct range based on picker settings
				this.AutoPickRange(job, title);

				// Choose the correct audio/subtitle tracks based on settings
				this.AutoPickAudio(job, title);
				this.AutoPickSubtitles(job, title);

				// Now that we have the title and subtitles we can determine the final output file name
				string fileToQueue = job.SourcePath;

				excludedPaths.Add(fileToQueue);
				string outputFolder = this.outputPathService.GetOutputFolder(fileToQueue, jobViewModel.SourceParentFolder);
				string outputFileName = this.outputPathService.BuildOutputFileName(
					fileToQueue,
					this.outputPathService.CleanUpSourceName(picker, Utilities.GetSourceNameFile(fileToQueue)),
					job.Title,
					title.Duration.ToSpan(),
					title.ChapterList.Count,
					multipleTitlesOnSource: titles.Count > 1);
				string outputExtension = this.outputPathService.GetOutputExtension();
				string queueOutputPath = Path.Combine(outputFolder, outputFileName + outputExtension);
				queueOutputPath = this.outputPathService.ResolveOutputPathConflicts(queueOutputPath, fileToQueue, excludedPaths, isBatch: true, picker, allowConflictDialog: false, allowQueueRemoval: true);

				job.FinalOutputPath = queueOutputPath;

				excludedPaths.Add(queueOutputPath);
			}

			this.QueueMultiple(itemsToQueue);

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
				this.WorkTracker.ReportRemovedFromQueue(job.Work);
			}
		}

		public void StartEncodeQueue()
		{
			this.logger.Log("Starting queue");
			this.logger.ShowStatus(MainRes.StartedEncoding);

			// If we had a "when done with current jobs" complete action last time, reset it to nothing.
			if (this.EncodeCompleteAction.Trigger == EncodeCompleteTrigger.DoneWithCurrentJobs)
			{
				this.EncodeCompleteAction = this.EncodeCompleteActions.Single(a => a.ActionType == EncodeCompleteActionType.DoNothing);
			}

			this.TotalTasks = this.EncodeQueue.Count;
			this.TaskNumber = 0;

			this.Encoding = true;
			this.WorkTracker.ReportEncodeStart(this.EncodeQueue.Items.Select(job => job.Work));
			SystemSleepManagement.PreventSleep();
			this.Paused = false;

			// If the encode is stopped we will change
			this.encodeCompleteReason = EncodeCompleteReason.Finished;
			this.autoPause.ReportStart();

			this.EncodeNextJobs();

			this.RebuildEncodingJobsList();

			// User had the window open when the encode ended last time, so we re-open when starting the queue again.
			if (Config.EncodeDetailsWindowOpen)
			{
				this.windowManager.OpenOrFocusWindow<EncodeDetailsWindowViewModel>();
			}
		}

		private void RebuildEncodingJobsList()
		{
			this.encodingJobList.Edit(encodingJobInnerList =>
			{
				encodingJobInnerList.Clear();
				foreach (var encodeJobViewModel in this.EncodeQueue.Items.Where(j => j.Encoding))
				{
					encodingJobInnerList.Add(encodeJobViewModel);
				}
			});
		}

		public HashSet<string> GetQueuedFiles()
		{
			return new HashSet<string>(this.EncodeQueue.Items.Select(j => j.Job.FinalOutputPath), StringComparer.OrdinalIgnoreCase);
		}

		private void RunForAllEncodeProxies(Func<IEncodeProxy, Task> proxyAction, string actionName)
		{
			foreach (IEncodeProxy encodeProxy in this.EncodeQueue.Items.Where(j => j.Encoding).Select(j => j.EncodeProxy))
			{
				if (encodeProxy != null)
				{
					this.StartEncodeProxyAction(encodeProxy, proxyAction, actionName);
				}
			}
		}

		private async void StartEncodeProxyAction(IEncodeProxy encodeProxy, Func<IEncodeProxy, Task> proxyAction, string actionName)
		{
			await this.RunEncodeProxyAction(encodeProxy, proxyAction, actionName).ConfigureAwait(false);
		}

		private async Task RunEncodeProxyAction(IEncodeProxy encodeProxy, Func<IEncodeProxy, Task> proxyAction, string actionName)
		{
			if (encodeProxy != null)
			{
				try
				{
					await proxyAction(encodeProxy).ConfigureAwait(false);
				}
				catch (Exception exception)
				{
					this.logger.LogError($"Encode proxy action '{actionName}' failed:" + Environment.NewLine + exception);
				}
			}
		}

		private void RunForAllEncodingJobs(Action<EncodeJobViewModel> jobAction)
		{
			foreach (var jobViewModel in this.EncodeQueue.Items.Where(j => j.Encoding))
			{
				jobAction(jobViewModel);
			}
		}

		public void Stop()
		{
			this.encodeCompleteReason = EncodeCompleteReason.Manual;

			this.RunForAllEncodeProxies(encodeProxy => encodeProxy.StopEncodeAsync(), "Stop");

			this.logger.ShowStatus(MainRes.StoppedEncoding);
		}

		public async Task StopAndWaitAsync(EncodeCompleteReason reason)
		{
			this.encodeCompleteReason = reason;

			var stopTasks = this.EncodeQueue.Items.Where(j => j.Encoding).Select(j => this.RunEncodeProxyAction(j.EncodeProxy, encodeProxy => encodeProxy.StopAndWaitAsync(), "StopAndWait"));
			await Task.WhenAll(stopTasks).ConfigureAwait(false);
		}

		public IList<EncodeJobWithMetadata> GetQueueStorageJobs()
		{
			var jobs = new List<EncodeJobWithMetadata>();
			foreach (EncodeJobViewModel jobVM in this.EncodeQueue.Items)
			{
				jobs.Add(
					new EncodeJobWithMetadata
					{
						Job = jobVM.Job,
						SourceParentFolder = jobVM.SourceParentFolder,
						ManualOutputPath = jobVM.ManualOutputPath,
						NameFormatOverride = jobVM.NameFormatOverride,
						PresetName = jobVM.PresetName,
						VideoSource = jobVM.VideoSource,
						VideoSourceMetadata = jobVM.VideoSourceMetadata
					});
			}

			return jobs;
		}

		private void EncodeNextJobs()
		{
			if (this.EncodeCompleteAction.Trigger == EncodeCompleteTrigger.DoneWithCurrentJobs)
			{
				return;
			}

			// Make sure the top N jobs are encoding.
			var encodeQueueList = this.EncodeQueue.Items.ToList();
			int maxEncodes = Config.MaxSimultaneousEncodes > 0 ? Config.MaxSimultaneousEncodes : 1;
			for (int i = 0; i < maxEncodes && i < this.EncodeQueue.Count; i++)
			{
				EncodeJobViewModel jobViewModel = encodeQueueList[i];

				if (!jobViewModel.Encoding)
				{
					this.TaskNumber++;
					this.StartEncode(jobViewModel);
				}
			}

			this.RefreshEncodeCompleteActions();
		}

		private void StartEncode(EncodeJobViewModel jobViewModel)
		{
			VCJob job = jobViewModel.Job;

			var encodeLogger = StaticResolver.Resolve<AppLoggerFactory>().ResolveEncodeLogger(job.FinalOutputPath);
			jobViewModel.EncodeLogger = encodeLogger;
			jobViewModel.EncodeSpeedDetailsAvailable = false;

			encodeLogger.Log("Starting job " + this.TaskNumber + "/" + this.TotalTasks);
			encodeLogger.Log("  Source path: " + job.SourcePath);
			encodeLogger.Log("  Destination path: " + job.FinalOutputPath);
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

			encodeLogger.Log("  Preset: " + jobViewModel.PresetName);

			this.logger.Log("Starting encode: " + job.FinalOutputPath);

			jobViewModel.ReportEncodeStart();

			string destinationDirectory = Path.GetDirectoryName(job.FinalOutputPath);
			if (!Directory.Exists(destinationDirectory))
			{
				try
				{
					Directory.CreateDirectory(destinationDirectory);
				}
				catch (Exception exception)
				{
					encodeLogger.LogError(string.Format(MainRes.DirectoryCreateErrorMessage, exception));
					this.OnEncodeCompleted(jobViewModel, VCEncodeResultCode.ErrorCouldNotCreateOutputDirectory);
					return;
				}
			}

			try
			{
				job.PartOutputPath = GetPartFilePath(job.FinalOutputPath);
			}
			catch (Exception exception)
			{
				encodeLogger.Log("Could not use temporary encoding path. Will instead output directly." + Environment.NewLine + exception);
			}

			jobViewModel.EncodeProxy = Utilities.CreateEncodeProxy();
			jobViewModel.EncodeProxy.EncodeProgress += (sender, args) =>
			{
				this.OnEncodeProgress(jobViewModel, args);
			};
			jobViewModel.EncodeProxy.EncodeCompleted += (sender, args) =>
			{
				this.OnEncodeCompleted(jobViewModel, args.Result);
			};
			jobViewModel.EncodeProxy.EncodeStarted += (sender, args) =>
			{
				this.OnEncodeStarted(jobViewModel);
			};

			// When a job is starting, we can't pause it or stop it.
			jobViewModel.CanPauseOrStop = false;
			this.canPauseOrStopSubject.OnNext(false);

			if (!string.IsNullOrWhiteSpace(jobViewModel.DebugEncodeJsonOverride))
			{
				jobViewModel.EncodeProxy.StartEncodeAsync(jobViewModel.DebugEncodeJsonOverride, encodeLogger);
			}
			else
			{
				jobViewModel.EncodeProxy.StartEncodeAsync(job, encodeLogger, false, 0, 0, 0);
			}
		}

		private BehaviorSubject<bool> canPauseOrStopSubject = new BehaviorSubject<bool>(false);

		private void OnEncodeStarted(EncodeJobViewModel jobViewModel)
		{
			DispatchUtilities.BeginInvoke(() =>
			{
				// After the encode has reported that it's started, we can now pause/stop it.
				jobViewModel.CanPauseOrStop = true;

				if (this.encodingJobList.Items.All(job => job.CanPauseOrStop))
				{
					this.canPauseOrStopSubject.OnNext(true);
				}
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

			jobViewModel.Work.CompletedWork = jobCompletedWork;

			if (this.WorkTracker.CanShowEta)
			{
				double jobRemainingWork = jobViewModel.Work.Cost - jobCompletedWork;

				if (this.WorkTracker.OverallWorkCompletionRate == 0)
				{
					jobViewModel.Eta = TimeSpan.MaxValue;
				}
				else
				{
					try
					{
						jobViewModel.Eta = TimeSpan.FromSeconds(jobRemainingWork / this.WorkTracker.OverallWorkCompletionRate);
					}
					catch (OverflowException)
					{
						jobViewModel.Eta = TimeSpan.MaxValue;
					}
				}
			}

			jobViewModel.FractionComplete = jobCompletedWork / jobViewModel.Work.Cost;
			jobViewModel.CurrentPassId = e.PassId;
			jobViewModel.PassProgressFraction = e.FractionComplete;
			jobViewModel.RefreshEncodeTimeDisplay();

			try
			{
				var outputFileInfo = new FileInfo(jobViewModel.Job.InProgressOutputPath);
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
				inProgressJobsCompletedWork += job.Work.CompletedWork;
			});

			this.WorkTracker.CalculateOverallEncodeProgress(inProgressJobsCompletedWork);

			double currentFps = 0;
			double averageFps = 0;

			foreach (var jobViewModel in this.EncodeQueue.Items.Where(jobViewModel => jobViewModel.Encoding && jobViewModel.EncodeSpeedDetailsAvailable))
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
		}

		private void OnEncodeCompleted(EncodeJobViewModel finishedJobViewModel, VCEncodeResultCode result)
		{
			if (finishedJobViewModel == null)
			{
				throw new ArgumentNullException(nameof(finishedJobViewModel));
			}

			finishedJobViewModel.EncodeProxy?.Dispose();

			DispatchUtilities.BeginInvoke(() =>
			{
				IAppLogger encodeLogger = finishedJobViewModel.EncodeLogger;
				string finalOutputPath = finishedJobViewModel.Job.FinalOutputPath;
				FileInfo directOutputFileInfo;
				try
				{
					directOutputFileInfo = new FileInfo(finishedJobViewModel.Job.InProgressOutputPath);
				}
				catch (Exception exception)
				{
					throw new InvalidOperationException("Could not get info for output file: " + finishedJobViewModel.Job.InProgressOutputPath, exception);
				}

				EncodeResultViewModel addedResult = null;
				bool encodingStopped = false;

				finishedJobViewModel.CanPauseOrStop = false;
				EncodeResultStatus status = EncodeResultStatus.Succeeded;

				finishedJobViewModel.ReportEncodeEnd();

				if (this.encodeCompleteReason != EncodeCompleteReason.Finished)
				{
					// If the encode was stopped manually
					this.StopEncodingAndReport();

					// Try to clean up the failed file
					TryHandleFailedFile(directOutputFileInfo, encodeLogger, "stopped", finalOutputPath);

					// If the user still has the video source up, clear the queue so they can change settings and try again.
					// If the source isn't loaded they probably want to keep it in the queue.
					if (this.TotalTasks == 1
						&& this.encodeCompleteReason == EncodeCompleteReason.Manual
						&& this.main.HasVideoSource
						&& this.main.SourcePath == this.EncodeQueue.Items.First().Job.SourcePath)
					{
						this.EncodeQueue.Clear();
					}

					encodingStopped = true;
					encodeLogger.Log("Encoding stopped");
					this.logger.Log("Encoding stopped");
				}
				else
				{
					// If the encode finished naturally
					this.WorkTracker.ReportFinished(finishedJobViewModel.Work);

					long outputFileLength = 0;

					if (result != VCEncodeResultCode.Succeeded)
					{
						status = EncodeResultStatus.Failed;
						encodeLogger.LogError("Encode failed with code " + result.ToString());
					}
					else if (!directOutputFileInfo.Exists)
					{
						status = EncodeResultStatus.Failed;
						encodeLogger.LogError("Encode failed. HandBrake reported no error but the expected output file was not found.");
					}
					else
					{
						outputFileLength = directOutputFileInfo.Length;
						if (outputFileLength == 0)
						{
							status = EncodeResultStatus.Failed;
							encodeLogger.LogError("Encode failed. HandBrake reported no error but the output file was empty.");
						}
					}

					string failedFilePath = null;

					if (status == EncodeResultStatus.Succeeded && finishedJobViewModel.Job.PartOutputPath != null)
					{
						// Rename from in progress path to final path
						try
						{
							if (File.Exists(finishedJobViewModel.Job.FinalOutputPath))
							{
								File.Delete(finishedJobViewModel.Job.FinalOutputPath);
							}

							File.Move(finishedJobViewModel.Job.PartOutputPath, finishedJobViewModel.Job.FinalOutputPath);
						}
						catch (Exception exception)
						{
							status = EncodeResultStatus.Failed;
							encodeLogger.LogError($"Could not rename to final output path. Output is left as '{finishedJobViewModel.Job.PartOutputPath}'" + Environment.NewLine + exception);
						}
					}
					else if (status != EncodeResultStatus.Succeeded)
					{
						// If failed, rename or delete the failed file as configured in settings
						failedFilePath = TryHandleFailedFile(directOutputFileInfo, encodeLogger, "failed", finalOutputPath);
					}

					if (status == EncodeResultStatus.Succeeded && !FileUtilities.IsDirectory(finishedJobViewModel.Job.SourcePath))
					{
						if (Config.PreserveModifyTimeFiles)
						{
							try
							{
								var inputShellFile = ShellFile.FromFilePath(finishedJobViewModel.Job.SourcePath);
								var outputShellFile = ShellFile.FromFilePath(finishedJobViewModel.Job.FinalOutputPath);

								try
								{
									DateTime? inputFileMediaCreatedTime = inputShellFile.Properties.System.Media.DateEncoded.Value;
									if (inputFileMediaCreatedTime != null)
									{
										outputShellFile.Properties.System.Media.DateEncoded.Value = inputFileMediaCreatedTime;
									}
								}
								catch (Exception exception)
								{
									encodeLogger.LogError("Could not set encoded date on file: " + exception);
								}

								File.SetCreationTimeUtc(finishedJobViewModel.Job.FinalOutputPath, inputShellFile.Properties.System.DateCreated.Value.Value);

								// Set "last write" time last so it isn't reset by another property edit.
								File.SetLastWriteTimeUtc(finishedJobViewModel.Job.FinalOutputPath, inputShellFile.Properties.System.DateModified.Value.Value);

								// Writing Created/Modified time does not work via the ShellFile API, otherwise we would use this approach to set them all at once.
								//ShellPropertyWriter propertyWriter = outputShellFile.Properties.GetPropertyWriter();
								//propertyWriter.WriteProperty(SystemProperties.System.DateCreated, inputShellFile.Properties.System.DateCreated.Value);
								//propertyWriter.WriteProperty(SystemProperties.System.DateModified, inputShellFile.Properties.System.DateModified.Value);
								//propertyWriter.Close();
							}
							catch (Exception exception)
							{
								encodeLogger.LogError("Could not set create/modify dates on file: " + exception);
							}
						}
					}

					addedResult = new EncodeResultViewModel(
						new EncodeResult
						{
							Destination = finishedJobViewModel.Job.FinalOutputPath,
							FailedFilePath = failedFilePath,
							Status = status,
							EncodeTime = finishedJobViewModel.EncodeTime,
							LogPath = encodeLogger.LogPath,
							SizeBytes = outputFileLength
						},
						finishedJobViewModel);
					this.completedJobs.Add(addedResult);

					if (status != EncodeResultStatus.Succeeded)
					{
						this.HasFailedItems = true;
					}

					this.EncodeQueue.Remove(finishedJobViewModel);

					// Run post-encode job
					var picker = this.pickersService.SelectedPicker.Picker;
					if (status == EncodeResultStatus.Succeeded && picker.PostEncodeActionEnabled && !string.IsNullOrWhiteSpace(picker.PostEncodeExecutable))
					{
						string arguments = this.outputPathService.ReplaceArguments(picker.PostEncodeArguments, picker, finishedJobViewModel)
							.Replace("{file}", finalOutputPath)
							.Replace("{folder}", Path.GetDirectoryName(finalOutputPath));

						var process = new ProcessStartInfo(
							picker.PostEncodeExecutable,
							arguments);
						System.Diagnostics.Process.Start(process);

						encodeLogger.Log($"Started post-encode action. Executable: {picker.PostEncodeExecutable} , Arguments: {arguments}");
					}

					encodeLogger.Log("Job completed (Elapsed Time: " + finishedJobViewModel.EncodeTime.FormatFriendly() + ")");
					this.logger.Log("Job completed: " + finishedJobViewModel.Job.FinalOutputPath);

					if (status != EncodeResultStatus.Succeeded)
					{
						this.RetryJobIfNeeded(finishedJobViewModel);
					}

					if (this.EncodeQueue.Count == 0)
					{
						this.SelectedTabIndex = CompletedTabIndex;
						this.StopEncodingAndReport();

						this.logger.Log("Queue completed");
						this.logger.ShowStatus(MainRes.EncodeCompleted);
						this.logger.Log("");

						this.TriggerQueueCompleteNotificationIfBackground();

						if (Config.PlaySoundOnCompletion && this.EncodeCompleteAction.Trigger == EncodeCompleteTrigger.None)
						{
							this.PlayEncodeCompleteSound();
						}

						encodingStopped = true;
						this.TriggerEncodeCompleteAction();
					}
					else if (this.EncodeCompleteAction.Trigger == EncodeCompleteTrigger.DoneWithCurrentJobs && !this.EncodeQueue.Items.Any(j => j.Encoding))
					{
						this.SelectedTabIndex = CompletedTabIndex;
						this.StopEncodingAndReport();

						encodingStopped = true;
						this.TriggerEncodeCompleteAction();
					}
					else
					{
						this.EncodeNextJobs();
					}
				}

				this.RebuildEncodingJobsList();

				if (encodingStopped)
				{
					this.windowManager.Close<EncodeDetailsWindowViewModel>(userInitiated: false);
				}

				string encodeLogPath = encodeLogger.LogPath;
				encodeLogger.Dispose();

				string logAffix;
				if (this.encodeCompleteReason != EncodeCompleteReason.Finished)
				{
					logAffix = "aborted";
				}
				else
				{
					logAffix = status == EncodeResultStatus.Succeeded ? "succeeded" : "failed";
				}

				string finalLogPath;
				if (CustomConfig.UseWorkerProcess)
				{
					finalLogPath = ApplyStatusAffixToLogPath(encodeLogPath, logAffix);
					encodeLogger.LogPath = finalLogPath;
				}
				else
				{
					finalLogPath = encodeLogPath;
				}

				if (addedResult != null)
				{
					addedResult.EncodeResult.LogPath = finalLogPath;
				}

				if (finalLogPath != null)
				{
					if (Config.CopyLogToOutputFolder)
					{
						if (CustomConfig.UseWorkerProcess)
						{
							string logCopyPath = Path.Combine(Path.GetDirectoryName(finalOutputPath), Path.GetFileName(finalLogPath));

							try
							{
								File.Copy(finalLogPath, logCopyPath);
							}
							catch (Exception exception)
							{
								this.logger.LogError("Could not copy log file to output directory: " + exception);
							}
						}
						else
						{
							this.logger.LogError("Cannot copy log to output folder: encode logs are only supported when using worker process.");
						}
					}
				}

				if (Config.CopyLogToCustomFolder)
				{
					try
					{
						if (!Directory.Exists(Config.LogCustomFolder))
						{
							Directory.CreateDirectory(Config.LogCustomFolder);
						}

						string logCopyPath = Path.Combine(Config.LogCustomFolder, Path.GetFileName(finalLogPath));

						File.Copy(finalLogPath, logCopyPath);
					}
					catch (Exception exception)
					{
						this.logger.LogError("Could not copy log file to output directory: " + exception);
					}
				}
			});
		}

		private void TriggerQueueCompleteNotificationIfBackground()
		{
			if (!Utilities.IsInForeground)
			{
				StaticResolver.Resolve<TrayService>().ShowBalloonMessage(MainRes.EncodeCompleteBalloonTitle, MainRes.EncodeCompleteBalloonMessage);
				if (Utilities.UwpApisAvailable)
				{
					const string toastFormat =
						"<?xml version=\"1.0\" encoding=\"utf-8\"?><toast><visual><binding template=\"ToastGeneric\"><text>{0}</text><text>{1}</text></binding></visual></toast>";

					string toastString = string.Format(toastFormat, SecurityElement.Escape(MainRes.EncodeCompleteBalloonTitle), SecurityElement.Escape(MainRes.EncodeCompleteBalloonMessage));

					this.toastNotificationService.Clear();
					this.toastNotificationService.ShowToast(toastString);
				}
			}
		}

		private void PlayEncodeCompleteSound()
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

		private void TriggerEncodeCompleteAction()
		{
			if (this.EncodeCompleteAction.ActionType != EncodeCompleteActionType.DoNothing && !Config.TriggerEncodeCompleteActionWithErrors && this.completedJobs.Items.Any(job => !job.EncodeResult.Succeeded))
			{
				StaticResolver.Resolve<IMessageBoxService>().Show(MainRes.EncodeCompleteActionAbortedDueToErrorsMessage);
				this.logger.Log(MainRes.EncodeCompleteActionAbortedDueToErrorsMessage);
				return;
			}

			switch (this.EncodeCompleteAction.ActionType)
			{
				case EncodeCompleteActionType.DoNothing:
				case EncodeCompleteActionType.StopEncoding:
					break;
				case EncodeCompleteActionType.EjectDisc:
					this.systemOperations.Eject(this.EncodeCompleteAction.DriveLetter);
					break;
				case EncodeCompleteActionType.CloseProgram:
					this.windowManager.Close(this.main);
					break;
				case EncodeCompleteActionType.Sleep:
				case EncodeCompleteActionType.LogOff:
				case EncodeCompleteActionType.Shutdown:
				case EncodeCompleteActionType.Hibernate:
					this.windowManager.OpenWindow(new ShutdownWarningWindowViewModel(this.EncodeCompleteAction.ActionType));
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		private static string ApplyStatusAffixToLogPath(string encodeLogPath, string affix)
		{
			if (encodeLogPath != null)
			{
				string directory = Path.GetDirectoryName(encodeLogPath);
				string logBaseName = Path.GetFileNameWithoutExtension(encodeLogPath);
				string extension = Path.GetExtension(encodeLogPath);

				string newEncodeLogPath = Path.Combine(directory, logBaseName + "-" + affix + extension);
				try
				{
					File.Move(encodeLogPath, newEncodeLogPath);
					return newEncodeLogPath;
				}
				catch
				{
					// Don't worry about it if moving failed.
				}
			}

			return encodeLogPath;
		}

		private static string TryHandleFailedFile(FileInfo directOutputFileInfo, IAppLogger encodeLogger, string reason, string finalOutputPath)
		{
			if (Config.KeepFailedFiles)
			{
				if (directOutputFileInfo.Exists)
				{
					try
					{
						string directory = Path.GetDirectoryName(finalOutputPath);
						string outputBaseName = Path.GetFileNameWithoutExtension(finalOutputPath);
						string extension = Path.GetExtension(finalOutputPath);

						string destinationPath = Path.Combine(directory, outputBaseName + "." + reason + extension);
						destinationPath = FileUtilities.CreateUniqueFileName(destinationPath, new HashSet<string>());
						directOutputFileInfo.MoveTo(destinationPath);
						return destinationPath;
					}
					catch (Exception exception)
					{
						encodeLogger.Log($"Could not rename failed file '{directOutputFileInfo.FullName}'." + Environment.NewLine + exception);
					}
				}
			}
			else
			{
				try
				{
					directOutputFileInfo.Delete();
				}
				catch (Exception exception)
				{
					encodeLogger.Log($"Could not clean up failed file '{directOutputFileInfo.FullName}'." + Environment.NewLine + exception);
				}
			}

			return null;
		}

		private void PauseEncoding()
		{
			this.RunForAllEncodeProxies(encodeProxy => encodeProxy.PauseEncodeAsync(), nameof(IEncodeProxy.PauseEncodeAsync));
			this.RunForAllEncodingJobs(job => job.ReportEncodePause());

			this.Paused = true;
		}

		private void ResumeEncoding()
		{
			this.RunForAllEncodeProxies(encodeProxy => encodeProxy.ResumeEncodeAsync(), nameof(IEncodeProxy.ResumeEncodeAsync));
			this.RunForAllEncodingJobs(job => job.ReportEncodeResume());

			this.Paused = false;

			// Some more jobs may have been added while paused. Start them if needed.
			if (Config.MaxSimultaneousEncodes > 1)
			{
				this.EncodeNextJobs();
			}
		}

		private void StopEncodingAndReport()
		{
			this.WorkTracker.ReportEncodeStop();
			this.Encoding = false;
			this.EncodeSpeedDetailsAvailable = false;
			SystemSleepManagement.AllowSleep();
			this.autoPause.ReportStop();
		}

		private void SaveEncodeQueue()
		{
			EncodeJobStorage.EncodeJobs = this.GetQueueStorageJobs();
		}

		private void RefreshEncodeCompleteActions()
		{
			if (this.completedJobs == null)
			{
				return;
			}

			EncodeCompleteAction oldCompleteAction = this.EncodeCompleteAction;

			if (this.EncodeQueue.Items.Any(j => !j.Encoding))
			{
				// If any items are not encoding yet, show both "when done with current items" choices and "when done with queue" choices

				this.encodeCompleteActions =
					new List<EncodeCompleteAction>
					{
						new EncodeCompleteAction { ActionType = EncodeCompleteActionType.DoNothing, Trigger = EncodeCompleteTrigger.None },
						new EncodeCompleteAction { ActionType = EncodeCompleteActionType.CloseProgram, Trigger = EncodeCompleteTrigger.DoneWithQueue, ShowTriggerInDisplay = true },
						new EncodeCompleteAction { ActionType = EncodeCompleteActionType.Sleep, Trigger = EncodeCompleteTrigger.DoneWithQueue, ShowTriggerInDisplay = true },
						new EncodeCompleteAction { ActionType = EncodeCompleteActionType.LogOff, Trigger = EncodeCompleteTrigger.DoneWithQueue, ShowTriggerInDisplay = true },
						new EncodeCompleteAction { ActionType = EncodeCompleteActionType.Hibernate, Trigger = EncodeCompleteTrigger.DoneWithQueue, ShowTriggerInDisplay = true },
						new EncodeCompleteAction { ActionType = EncodeCompleteActionType.Shutdown, Trigger = EncodeCompleteTrigger.DoneWithQueue, ShowTriggerInDisplay = true },
						new EncodeCompleteAction { ActionType = EncodeCompleteActionType.StopEncoding, Trigger = EncodeCompleteTrigger.DoneWithCurrentJobs, ShowTriggerInDisplay = true },
						new EncodeCompleteAction { ActionType = EncodeCompleteActionType.CloseProgram, Trigger = EncodeCompleteTrigger.DoneWithCurrentJobs, ShowTriggerInDisplay = true },
						new EncodeCompleteAction { ActionType = EncodeCompleteActionType.Sleep, Trigger = EncodeCompleteTrigger.DoneWithCurrentJobs, ShowTriggerInDisplay = true },
						new EncodeCompleteAction { ActionType = EncodeCompleteActionType.LogOff, Trigger = EncodeCompleteTrigger.DoneWithCurrentJobs, ShowTriggerInDisplay = true },
						new EncodeCompleteAction { ActionType = EncodeCompleteActionType.Hibernate, Trigger = EncodeCompleteTrigger.DoneWithCurrentJobs, ShowTriggerInDisplay = true },
						new EncodeCompleteAction { ActionType = EncodeCompleteActionType.Shutdown, Trigger = EncodeCompleteTrigger.DoneWithCurrentJobs, ShowTriggerInDisplay = true },
					};
			}
			else
			{
				// If all items are encoding, only show queue options

				this.encodeCompleteActions =
					new List<EncodeCompleteAction>
					{
						new EncodeCompleteAction { ActionType = EncodeCompleteActionType.DoNothing },
						new EncodeCompleteAction { ActionType = EncodeCompleteActionType.CloseProgram, Trigger = EncodeCompleteTrigger.DoneWithQueue },
						new EncodeCompleteAction { ActionType = EncodeCompleteActionType.Sleep, Trigger = EncodeCompleteTrigger.DoneWithQueue },
						new EncodeCompleteAction { ActionType = EncodeCompleteActionType.LogOff, Trigger = EncodeCompleteTrigger.DoneWithQueue },
						new EncodeCompleteAction { ActionType = EncodeCompleteActionType.Hibernate, Trigger = EncodeCompleteTrigger.DoneWithQueue },
						new EncodeCompleteAction { ActionType = EncodeCompleteActionType.Shutdown, Trigger = EncodeCompleteTrigger.DoneWithQueue },
					};
			}

			// Applicable drives to eject are those in the queue or completed items list
			var applicableDrives = new HashSet<string>();
			foreach (EncodeJobViewModel job in this.EncodeQueue.Items)
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

			foreach (EncodeResultViewModel result in this.completedJobs.Items)
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
				this.encodeCompleteActions.Insert(1, new EncodeCompleteAction { ActionType = EncodeCompleteActionType.EjectDisc, DriveLetter = drive, Trigger = EncodeCompleteTrigger.DoneWithQueue });
			}

			this.RaisePropertyChanged(nameof(this.EncodeCompleteActions));

			// Transfer over the previously selected item. First look for exact match.
			this.encodeCompleteAction = this.encodeCompleteActions[0];
			bool foundMatch = false;
			for (int i = 1; i < this.encodeCompleteActions.Count; i++)
			{
				if (this.encodeCompleteActions[i].Equals(oldCompleteAction))
				{
					this.encodeCompleteAction = this.encodeCompleteActions[i];
					foundMatch = true;
					break;
				}
			}

			// If no exact match, try again but ignore the trigger
			if (!foundMatch && oldCompleteAction != null)
			{
				for (int i = 1; i < this.encodeCompleteActions.Count; i++)
				{
					EncodeCompleteAction action = this.encodeCompleteActions[i];
					if (action.ActionType == oldCompleteAction.ActionType && action.DriveLetter == oldCompleteAction.DriveLetter)
					{
						this.encodeCompleteAction = this.encodeCompleteActions[i];
						break;
					}
				}
			}

			this.RaisePropertyChanged(nameof(this.EncodeCompleteAction));
		}

		private bool EnsureValidOutputPath()
		{
			if (this.outputPathService.PathIsValid())
			{
				return true;
			}

			StaticResolver.Resolve<IMessageBoxService>().Show(
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

		/// <summary>
		/// Gets the .part file path given the final output path.
		/// </summary>
		/// <param name="outputFilePath"></param>
		/// <returns>The .part file path.</returns>
		/// <exception cref="PathTooLongException">Thrown when path is too long.</exception>
		/// <exception cref="IOException">Thrown when unable to delete old part file.</exception>
		private static string GetPartFilePath(string outputFilePath)
		{
			string directory = Path.GetDirectoryName(outputFilePath);
			string extension = Path.GetExtension(outputFilePath);
			string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(outputFilePath);

			string partPath = Path.Combine(directory, fileNameWithoutExtension + ".part" + extension);

			// This will throw if the path is too long.
			partPath = Path.GetFullPath(partPath);

			if (File.Exists(partPath))
			{
				File.Delete(partPath);
			}

			return partPath;
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

			int outputIndex = 0;

			job.AudioTracks = new List<ChosenAudioTrack>();
			switch (picker.AudioSelectionMode)
			{
				case AudioSelectionMode.Disabled:
					if (title.AudioList.Count > 0)
					{
						if (useCurrentContext)
						{
							// With previous context, pick similarly
							foreach (AudioTrackViewModel audioVM in this.main.AudioTracks.Items.Where(t => t.Selected))
							{
								int audioIndex = audioVM.TrackIndex;

								if (title.AudioList.Count > audioIndex && this.main.SelectedTitle.AudioList[audioIndex].LanguageCode == title.AudioList[audioIndex].LanguageCode)
								{
									job.AudioTracks.Add(new ChosenAudioTrack { TrackNumber = audioIndex + 1, Name = GetPickerAudioName(picker, outputIndex++) });
								}
							}

							// If we didn't manage to match any existing audio tracks, use the first audio track.
							if (job.AudioTracks.Count == 0)
							{
								job.AudioTracks.Add(new ChosenAudioTrack { TrackNumber = 1, Name = GetPickerAudioName(picker, outputIndex++) });
							}
						}
						else
						{
							// With no previous context, just pick the first track
							job.AudioTracks.Add(new ChosenAudioTrack { TrackNumber = 1, Name = GetPickerAudioName(picker, outputIndex++) });
						}
					}

					break;
				case AudioSelectionMode.First:
				case AudioSelectionMode.ByIndex:
				case AudioSelectionMode.Language:
				case AudioSelectionMode.All:
					job.AudioTracks.AddRange(ChooseAudioTracks(title.AudioList, picker).Select(track => new ChosenAudioTrack { TrackNumber = track.TrackNumber, Name = GetPickerAudioName(picker, outputIndex++) }));

					break;
				default:
					throw new ArgumentOutOfRangeException();
			}

			// If none get chosen, pick the first one.
			if (job.AudioTracks.Count == 0 && title.AudioList.Count > 0)
			{
				job.AudioTracks.Add(new ChosenAudioTrack { TrackNumber = 1 });
			}

			// Use the picker to apply names to the chosen audio tracks
			for (int i = 0; i < job.AudioTracks.Count; i++)
			{
				job.AudioTracks[i].Name = GetPickerAudioName(picker, i);
			}
		}

		/// <summary>
		/// Returns the 0-based track indices that should be included. Valid for all modes but Disabled.
		/// </summary>
		/// <param name="audioTracks">The audio tracks on the input video.</param>
		/// <param name="picker">The picker to use.</param>
		/// <returns>The audio tracks that should be included.</returns>
		public static IList<SourceAudioTrack> ChooseAudioTracks(IList<SourceAudioTrack> audioTracks, Picker picker)
		{
			var result = new List<SourceAudioTrack>();
			IList<SourceAudioTrack> chosenAudioTracks;
			switch (picker.AudioSelectionMode)
			{
				case AudioSelectionMode.Disabled:
					throw new ArgumentException("Disabled is an invalid mode.");
				case AudioSelectionMode.First:
					if (audioTracks.Count > 0)
					{
						result.Add(audioTracks[0]);
					}

					break;
				case AudioSelectionMode.ByIndex:
					// 1-based
					IList<int> chosenAudioTrackIndices = ParseUtilities.ParseCommaSeparatedListToPositiveIntegers(picker.AudioIndices);

					// Filter out indices that are out of range and look up the tracks
					result.AddRange(chosenAudioTrackIndices.Where(i => i <= audioTracks.Count).Select(i => audioTracks[i - 1]));

					break;
				case AudioSelectionMode.Language:
					chosenAudioTracks = ChooseAudioTracksFromLanguages(audioTracks, picker.AudioLanguageCodes, picker.AudioLanguageAll);
					result.AddRange(chosenAudioTracks);

					break;
				case AudioSelectionMode.All:
					if (picker.AudioLanguageCodes != null && picker.AudioLanguageCodes.Count > 0)
					{
						// All tracks with certain languages first
						chosenAudioTracks = ChooseAudioTracksFromLanguages(audioTracks, picker.AudioLanguageCodes, includeAllTracks: true);
						result.AddRange(chosenAudioTracks);

						foreach (SourceAudioTrack track in audioTracks)
						{
							if (!chosenAudioTracks.Contains(track))
							{
								result.Add(track);
							}
						}
					}
					else
					{
						// All tracks, no ordering on language
						foreach (SourceAudioTrack track in audioTracks)
						{
							result.Add(track);
						}
					}

					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(picker), picker, null);
			}

			// If none are chosen and we have not explicitly left it out, add the first one.
			if (result.Count == 0 && audioTracks.Count > 0 && picker.AudioSelectionMode != AudioSelectionMode.ByIndex)
			{
				result.Add(audioTracks[0]);
			}

			return result;
		}

		/// <summary>
		/// Chooses audio tracks matching given language codes.
		/// </summary>
		/// <param name="audioTracks">The list of audio tracks to look in.</param>
		/// <param name="languageCodes">The codes for the languages to include.</param>
		/// <param name="includeAllTracks">True if all tracks should be included rather than just the first.</param>
		/// <returns>The chosen audio tracks.</returns>
		private static IList<SourceAudioTrack> ChooseAudioTracksFromLanguages(IList<SourceAudioTrack> audioTracks, IList<string> languageCodes, bool includeAllTracks)
		{
			var result = new List<SourceAudioTrack>();

			foreach (string code in languageCodes)
			{
				foreach (SourceAudioTrack track in audioTracks)
				{
					if (track.LanguageCode == code)
					{
						result.Add(track);

						if (!includeAllTracks)
						{
							break;
						}
					}
				}
			}

			return result;
		}

		/// <summary>
		/// Gets the audio track name according to the picker.
		/// </summary>
		/// <param name="picker">The picker to use to determine the audio track name.</param>
		/// <param name="index">The 0-based index into the output audio track list.</param>
		/// <returns>The name for the audio track, or null if one should not be set.</returns>
		public static string GetPickerAudioName(Picker picker, int index)
		{
			if (!picker.UseCustomAudioTrackNames || picker.AudioTrackNames == null)
			{
				return null;
			}

			if (index >= picker.AudioTrackNames.Count)
			{
				return null;
			}

			string name = picker.AudioTrackNames[index];
			if (name == string.Empty)
			{
				return null;
			}

			return name;
		}

		// Automatically pick the correct subtitles on the given job.
		// Only relies on input from settings and the current title.
		private void AutoPickSubtitles(VCJob job, SourceTitle title, bool useCurrentContext = false)
		{
			Picker picker = this.pickersService.SelectedPicker.Picker;

			job.Subtitles = new VCSubtitles { SourceSubtitles = new List<ChosenSourceSubtitle>(), FileSubtitles = new List<FileSubtitle>() };
			switch (picker.SubtitleSelectionMode)
			{
				case SubtitleSelectionMode.Disabled:
					// Only pick subtitles when we have previous context.
					if (useCurrentContext)
					{
						foreach (ChosenSourceSubtitle sourceSubtitle in this.main.CurrentSubtitles.SourceSubtitles)
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
				case SubtitleSelectionMode.ByIndex:
				case SubtitleSelectionMode.ForeignAudioSearch:
				case SubtitleSelectionMode.Language:
				case SubtitleSelectionMode.All:
					job.Subtitles.SourceSubtitles.AddRange(ChooseSubtitles(
						title,
						picker,
						job.AudioTracks.Count > 0 ? job.AudioTracks[0].TrackNumber : -1,
						job.EncodingProfile.ContainerName));

					break;
				default:
					throw new ArgumentOutOfRangeException();
			}

			// Use the picker to apply track names to the chosen subtitles
			for (int i = 0; i < job.Subtitles.SourceSubtitles.Count; i++)
			{
				job.Subtitles.SourceSubtitles[i].Name = GetPickerSubtitleName(picker, i);
			}
		}

		/// <summary>
		/// Returns all the source subtitles that should be included.
		/// </summary>
		/// <param name="title">The title to pick from.</param>
		/// <param name="picker">The picker to use.</param>
		/// <param name="chosenAudioTrack">The (1-based) main audio track currently selected, or -1 if no audio track is selected.</param>
		/// <returns></returns>
		public static IList<ChosenSourceSubtitle> ChooseSubtitles(SourceTitle title, Picker picker, int chosenAudioTrack, string containerName)
		{
			var result = new List<ChosenSourceSubtitle>();
			IList<SourceSubtitleTrack> chosenSubtitleTracks;

			int containerId = HandBrakeEncoderHelpers.GetContainer(containerName).Id;

			if (picker.SubtitleAddForeignAudioScan)
			{
				result.Add(new ChosenSourceSubtitle
				{
					TrackNumber = 0,
					BurnedIn = picker.SubtitleBurnInSelection.ForeignAudioIncluded(),
					ForcedOnly = true,
					Default = false
				});
			}

			switch (picker.SubtitleSelectionMode)
			{
				case SubtitleSelectionMode.Disabled:
					throw new ArgumentException("Disabled is an invalid mode.");
				case SubtitleSelectionMode.None:
					break;
				case SubtitleSelectionMode.First:
					if (title.SubtitleList.Count > 0)
					{
						result.Add(new ChosenSourceSubtitle
						{
							TrackNumber = 1,
							BurnedIn = picker.SubtitleBurnInSelection.FirstTrackIncluded()
								|| !HandBrakeEncoderHelpers.SubtitleCanPassthrough(title.SubtitleList[0].Source, containerId),
							ForcedOnly = picker.SubtitleForcedOnly,
							Default = picker.SubtitleDefault
						});
					}

					break;
				case SubtitleSelectionMode.ByIndex:
					// 1-based
					IList<int> chosenSubtitleTrackNumbersParsed = ParseUtilities.ParseCommaSeparatedListToPositiveIntegers(picker.SubtitleIndices);
					var subtitleIndicesThatPointToRealTracks = chosenSubtitleTrackNumbersParsed.Where(i => i <= title.SubtitleList.Count).ToList();
					int? defaultSubtitleIndex = picker.SubtitleDefaultIndex;

					// If there are multiple tracks specified, weed out the ones that can't pass through.
					if (subtitleIndicesThatPointToRealTracks.Count > 1)
					{
						for (int i = subtitleIndicesThatPointToRealTracks.Count - 1; i >= 0; i--)
						{
							if (!HandBrakeEncoderHelpers.SubtitleCanPassthrough(title.SubtitleList[subtitleIndicesThatPointToRealTracks[i] - 1].Source, containerId))
							{
								subtitleIndicesThatPointToRealTracks.RemoveAt(i);
							}
						}
					}

					if (subtitleIndicesThatPointToRealTracks.Count > 1)
					{
						bool defaultChosen = false;
						bool isFirstTrack = true;
						foreach (int trackNumber in subtitleIndicesThatPointToRealTracks)
						{
							bool burnIn = isFirstTrack && picker.SubtitleBurnInSelection.FirstTrackIncluded();
							bool isDefault = false;
							if (!burnIn && !defaultChosen && defaultSubtitleIndex != null && defaultSubtitleIndex.Value == trackNumber)
							{
								isDefault = true;
								defaultChosen = true;
							}

							result.Add(new ChosenSourceSubtitle
							{
								TrackNumber = trackNumber,
								BurnedIn = burnIn,
								ForcedOnly = picker.SubtitleForcedOnly,
								Default = isDefault
							});

							isFirstTrack = false;
						}
					}
					else if (subtitleIndicesThatPointToRealTracks.Count > 0)
					{
						result.Add(new ChosenSourceSubtitle
						{
							TrackNumber = subtitleIndicesThatPointToRealTracks[0],
							BurnedIn = picker.SubtitleBurnInSelection.FirstTrackIncluded() || !HandBrakeEncoderHelpers.SubtitleCanPassthrough(title.SubtitleList[subtitleIndicesThatPointToRealTracks[0] - 1].Source, containerId),
							ForcedOnly = picker.SubtitleForcedOnly,
							Default = defaultSubtitleIndex != null && defaultSubtitleIndex.Value == subtitleIndicesThatPointToRealTracks[0]
						});
					}

					break;
				case SubtitleSelectionMode.Language:
					chosenSubtitleTracks = ChooseSubtitlesFromLanguages(title.SubtitleList, title.AudioList, chosenAudioTrack, picker.SubtitleLanguageCodes, picker.SubtitleLanguageAll, picker.SubtitleLanguageOnlyIfDifferent);
					if (chosenSubtitleTracks.Count > 1)
					{
						// Multiple

						// First track
						result.Add(new ChosenSourceSubtitle
						{
							TrackNumber = chosenSubtitleTracks[0].TrackNumber,
							BurnedIn = picker.SubtitleBurnInSelection.FirstTrackIncluded(),
							ForcedOnly = picker.SubtitleForcedOnly,
							Default = picker.SubtitleDefault
						});

						// The rest
						foreach (var sourceSubtitleTrack in chosenSubtitleTracks.Skip(1))
						{
							result.Add(new ChosenSourceSubtitle
							{
								TrackNumber = sourceSubtitleTrack.TrackNumber,
								BurnedIn = false,
								ForcedOnly = picker.SubtitleForcedOnly,
								Default = false
							});
						}
					}
					else if (chosenSubtitleTracks.Count > 0)
					{
						// Single
						result.Add(new ChosenSourceSubtitle
						{
							TrackNumber = chosenSubtitleTracks[0].TrackNumber,
							BurnedIn = picker.SubtitleBurnInSelection.FirstTrackIncluded(),
							ForcedOnly = picker.SubtitleForcedOnly,
							Default = picker.SubtitleDefault
						});
					}

					break;
				case SubtitleSelectionMode.All:
					List<SourceSubtitleTrack> subtitlesThatCanPassthrough = FilterToPassthroughSourceSubtitles(title.SubtitleList, containerId);
					if (picker.SubtitleLanguageCodes != null && picker.SubtitleLanguageCodes.Count > 0)
					{
						// First the chosen language tracks to bring to the top
						chosenSubtitleTracks = ChooseSubtitlesFromLanguages(subtitlesThatCanPassthrough, title.AudioList, chosenAudioTrack, picker.SubtitleLanguageCodes, includeAllTracks: true, onlyIfDifferentFromAudio: false);

						// Then everything else
						foreach (var sourceSubtitleTrack in subtitlesThatCanPassthrough)
						{
							if (!chosenSubtitleTracks.Contains(sourceSubtitleTrack))
							{
								chosenSubtitleTracks.Add(sourceSubtitleTrack);
							}
						}
					}
					else
					{
						chosenSubtitleTracks = new List<SourceSubtitleTrack>();

						foreach (var sourceSubtitleTrack in subtitlesThatCanPassthrough)
						{
							chosenSubtitleTracks.Add(sourceSubtitleTrack);
						}
					}

					if (chosenSubtitleTracks.Count > 0)
					{
						// First track
						bool burnFirstTrack = picker.SubtitleBurnInSelection.FirstTrackIncluded();
						result.Add(new ChosenSourceSubtitle
						{
							TrackNumber = chosenSubtitleTracks[0].TrackNumber,
							BurnedIn = burnFirstTrack,
							ForcedOnly = picker.SubtitleForcedOnly,
							Default = picker.SubtitleDefault && !burnFirstTrack
						});

						// The rest
						foreach (var sourceSubtitleTrack in chosenSubtitleTracks.Skip(1))
						{
							result.Add(new ChosenSourceSubtitle
							{
								TrackNumber = sourceSubtitleTrack.TrackNumber,
								BurnedIn = false,
								ForcedOnly = picker.SubtitleForcedOnly,
								Default = false
							});
						}
					}

					break;
				default:
					throw new ArgumentOutOfRangeException();
			}

			return result;
		}

		/// <summary>
		/// Gets the subtitle name according to the picker.
		/// </summary>
		/// <param name="picker">The picker to use to determine the subtitle name.</param>
		/// <param name="index">The 0-based index into the output subtitle list.</param>
		/// <returns>The name for the subtitle, or null if one should not be set.</returns>
		public static string GetPickerSubtitleName(Picker picker, int index)
		{
			if (!picker.UseCustomSubtitleTrackNames || picker.SubtitleTrackNames == null)
			{
				return null;
			}

			if (index >= picker.SubtitleTrackNames.Count)
			{
				return null;
			}

			string name = picker.SubtitleTrackNames[index];
			if (name == string.Empty)
			{
				return null;
			}

			return name;
		}

		private static List<SourceSubtitleTrack> FilterToPassthroughSourceSubtitles(List<SourceSubtitleTrack> sourceSubtitleList, int containerId)
		{
			// Do we have the right options here?
			// You might only be able to burn and you might only be able to pass through
			return sourceSubtitleList.Where(s => HandBrakeEncoderHelpers.SubtitleCanPassthrough(s.Source, containerId)).ToList();
		}

		private static IList<SourceSubtitleTrack> ChooseSubtitlesFromLanguages(IList<SourceSubtitleTrack> sourceSubtitleTracks, IList<SourceAudioTrack> sourceAudioTracks, int chosenAudioTrack, IList<string> languageCodes, bool includeAllTracks, bool onlyIfDifferentFromAudio)
		{
			var result = new List<SourceSubtitleTrack>();
			string audioLanguageCode = null;

			if (chosenAudioTrack > 0 && sourceAudioTracks.Count > 0)
			{
				audioLanguageCode = sourceAudioTracks[chosenAudioTrack - 1].LanguageCode;
			}

			foreach (string code in languageCodes)
			{
				if (!onlyIfDifferentFromAudio || code != audioLanguageCode)
				{
					for (int i = 0; i < sourceSubtitleTracks.Count; i++)
					{
						SourceSubtitleTrack track = sourceSubtitleTracks[i];

						if (track.LanguageCode == code)
						{
							result.Add(track);

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

		/// <summary>
		/// Populates range and length information on job.
		/// </summary>
		/// <param name="job">The job to pick the range on.</param>
		/// <param name="title">The title the job is applied to.</param>
		private void AutoPickRange(VCJob job, SourceTitle title)
		{
			Picker picker = this.pickersService.SelectedPicker.Picker;
			switch (picker.PickerTimeRangeMode)
			{
				case PickerTimeRangeMode.Chapters:
					var chapterRange = PickerUtilities.GetChapterRange(picker, title);

					job.RangeType = VideoRangeType.Chapters;
					job.ChapterStart = chapterRange.chapterStart;
					job.ChapterEnd = chapterRange.chapterEnd;
					job.Length = title.GetChapterRangeDuration(chapterRange.chapterStart, chapterRange.chapterEnd);

					break;
				case PickerTimeRangeMode.Time:
					TimeSpan rangeStart = picker.TimeRangeStart;
					TimeSpan rangeEnd = picker.TimeRangeEnd;

					if (rangeStart >= rangeEnd)
					{
						job.RangeType = VideoRangeType.All;
						job.Length = title.Duration.ToSpan();
						return;
					}

					job.RangeType = VideoRangeType.Seconds;
					var range = this.GetRangeFromPicker(title, picker);

					job.SecondsStart = range.start.TotalSeconds;
					job.SecondsEnd = range.end.TotalSeconds;
					job.Length = range.end - range.start;

					break;
				case PickerTimeRangeMode.All:
				default:
					job.RangeType = VideoRangeType.All;
					job.Length = title.Duration.ToSpan();
					break;
			}
		}

		public (TimeSpan start, TimeSpan end) GetRangeFromPicker(SourceTitle title, Picker picker)
		{
			TimeSpan titleDuration = title.Duration.ToSpan();
			TimeSpan rangeStart = picker.TimeRangeStart;
			TimeSpan rangeEnd = picker.TimeRangeEnd;

			TimeSpan duration = rangeEnd - rangeStart;

			// Make sure the end of the range doesn't go past the video end.
			// If it does, preserve the duration of the same clip and move toward the start
			if (rangeEnd > title.Duration.ToSpan())
			{
				rangeEnd = titleDuration;
				rangeStart = titleDuration - duration;

				if (rangeStart < TimeSpan.Zero)
				{
					rangeStart = TimeSpan.Zero;
				}
			}

			return (rangeStart, rangeEnd);
		}
	}
}
