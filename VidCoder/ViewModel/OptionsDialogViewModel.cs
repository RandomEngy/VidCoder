using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Resources;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.AnyContainer;
using VidCoder.Model;
using VidCoder.Resources;
using VidCoder.Services;
using VidCoder.Services.Windows;
using ReactiveUI;
using VidCoder.Extensions;
using VidCoderCommon;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using VidCoderCommon.Model;
using Velopack;

namespace VidCoder.ViewModel;

public class OptionsDialogViewModel : ReactiveObject
{
	public const int UpdatesTabIndex = 3;

	private IWindowManager windowManager = StaticResolver.Resolve<IWindowManager>();

#pragma warning disable 169
	//private UpdateInfo betaInfo;
	private bool betaInfoAvailable;
#pragma warning restore 169

	private bool startedBetaInfoFetch = false;

	public OptionsDialogViewModel(IUpdater updateService)
	{
		this.Updater = updateService;

		this.Tabs = new List<string>
		{
			OptionsRes.GeneralTab,			// 0
			OptionsRes.ProcessTab,			// 1
			OptionsRes.AdvancedTab,			// 2
		};

		if (Utilities.SupportsUpdates)
		{
			this.Tabs.Add(OptionsRes.UpdatesTab); // 3
		}

		// UpdateStatus
		this.Updater
			.WhenAnyValue(x => x.State)
			.Select(state =>
			{
				switch (state)
				{
					case UpdateState.NotStarted:
						return string.Empty;
					case UpdateState.Checking:
						return OptionsRes.CheckingForUpdateStatus;
					case UpdateState.Downloading:
						return string.Format(OptionsRes.DownloadingUpdateStatus, this.Updater.LatestVersion);
					case UpdateState.UpToDate:
						return OptionsRes.UpToDateStatus;
					case UpdateState.UpdateReady:
						return string.Format(OptionsRes.UpdateReadyStatus, this.Updater.LatestVersion);
					case UpdateState.Failed:
						return OptionsRes.UpdateFailedStatus;
					default:
						throw new ArgumentOutOfRangeException();
				}
			})
			.ToProperty(this, x => x.UpdateStatus, out this.updateStatus, scheduler: Scheduler.Immediate);

		// DownloadingUpdate
		this.Updater
			.WhenAnyValue(x => x.State)
			.Select(state => state == UpdateState.Downloading)
			.ToProperty(this, x => x.DownloadingUpdate, out this.downloadingUpdate);

		// UpdateProgressPercent
		this.Updater
			.WhenAnyValue(x => x.UpdateDownloadProgressPercent)
			.ToProperty(this, x => x.UpdateProgressPercent, out this.updateProgressPercent);

		// CpuThrottlingDisplay
		this.WhenAnyValue(x => x.CpuThrottlingCores, x => x.CpuThrottlingMaxCores, (cores, maxCores) =>
		{
			double currentFraction = (double)cores / maxCores;
			return currentFraction.ToString("p0");
		}).ToProperty(this, x => x.CpuThrottlingDisplay, out this.cpuThrottlingDisplay, deferSubscription:true, scheduler: Scheduler.Immediate);

		this.AutoPauseProcesses = new ObservableCollection<string>();
		this.cpuThrottlingCores = (int)Math.Round(this.CpuThrottlingMaxCores * Config.CpuThrottlingFraction);
		if (this.cpuThrottlingCores < 1)
		{
			this.cpuThrottlingCores = 1;
		}

		if (this.cpuThrottlingCores > this.CpuThrottlingMaxCores)
		{
			this.cpuThrottlingCores = this.CpuThrottlingMaxCores;
		}

		List<string> autoPauseList = CustomConfig.AutoPauseProcesses;
		if (autoPauseList != null)
		{
			foreach (string process in autoPauseList)
			{
				this.AutoPauseProcesses.Add(process);
			}
		}

		// List of language codes and names: https://web.archive.org/web/20170115084704/https://www.microsoft.com/resources/msdn/goglobal/default.mspx

		this.LanguageChoices =
			new List<InterfaceLanguage>
				{
					new InterfaceLanguage { CultureCode = string.Empty, Display = OptionsRes.UseOSLanguage },
					new InterfaceLanguage { CultureCode = "en-US", Display = "English" },
					new InterfaceLanguage { CultureCode = "id-ID", Display = "Bahasa Indonesia / Indonesian" },
					new InterfaceLanguage { CultureCode = "bs-Latn-BA", Display = "bosanski (Bosna i Hercegovina) / Bosnian (Latin)" },
					new InterfaceLanguage { CultureCode = "cs-CZ", Display = "čeština / Czech" },
					new InterfaceLanguage { CultureCode = "de-DE", Display = "Deutsch / German" },
					new InterfaceLanguage { CultureCode = "es-ES", Display = "Español / Spanish" },
					new InterfaceLanguage { CultureCode = "eu-ES", Display = "Euskara / Basque" },
					new InterfaceLanguage { CultureCode = "fr-FR", Display = "Français / French" },
					new InterfaceLanguage { CultureCode = "it-IT", Display = "italiano / Italian" },
					new InterfaceLanguage { CultureCode = "hr-HR", Display = "hrvatski / Croatian" },
					new InterfaceLanguage { CultureCode = "hu-HU", Display = "Magyar / Hungarian" },
					new InterfaceLanguage { CultureCode = "nl-BE", Display = "Nederlands / Dutch" },
					new InterfaceLanguage { CultureCode = "pl-PL", Display = "polski / Polish" },
					new InterfaceLanguage { CultureCode = "pt-PT", Display = "Português / Portuguese" },
					new InterfaceLanguage { CultureCode = "pt-BR", Display = "Português (Brasil) / Portuguese (Brazil)" },
					new InterfaceLanguage { CultureCode = "tr-TR", Display = "Türkçe / Turkish" },
					new InterfaceLanguage { CultureCode = "vi-VN", Display = "Tiếng Việt / Vietnamese" },
					new InterfaceLanguage { CultureCode = "ar-EG", Display = "العربية‏ / Arabic" },
					new InterfaceLanguage { CultureCode = "ka-GE", Display = "ქართული / Georgian" },
					new InterfaceLanguage { CultureCode = "el-GR", Display = "ελληνικά / Greek" },
					new InterfaceLanguage { CultureCode = "ru-RU", Display = "русский / Russian" },
					new InterfaceLanguage { CultureCode = "ko-KO", Display = "한국어 / Korean" },
					new InterfaceLanguage { CultureCode = "zh-Hans", Display = "中文(简体) / Chinese (Simplified)" },
					new InterfaceLanguage { CultureCode = "zh-Hant", Display = "中文(繁體) / Chinese (Traditional)" },
					new InterfaceLanguage { CultureCode =  "ja-JP", Display = "日本語 / Japanese" },
				};

		this.AppThemeChoices = 
			new List<ComboChoice<AppThemeChoice>>
			{
				new ComboChoice<AppThemeChoice>(AppThemeChoice.Auto, EnumsRes.AppThemeChoice_Auto),
				new ComboChoice<AppThemeChoice>(AppThemeChoice.Light, EnumsRes.AppThemeChoice_Light),
				new ComboChoice<AppThemeChoice>(AppThemeChoice.Dark, EnumsRes.AppThemeChoice_Dark),
			};

		this.PriorityChoices = new List<ComboChoice>
			{
				new ComboChoice("High", OptionsRes.Priority_High),
				new ComboChoice("AboveNormal", OptionsRes.Priority_AboveNormal),
				new ComboChoice("Normal", OptionsRes.Priority_Normal),
				new ComboChoice("BelowNormal", OptionsRes.Priority_BelowNormal),
				new ComboChoice("Idle", OptionsRes.Priority_Idle),
			};

		this.PlayerChoices = Players.All;

		int tabIndex = Config.OptionsDialogLastTab;
		if (tabIndex >= this.Tabs.Count)
		{
			tabIndex = 0;
		}

		this.SelectedTabIndex = tabIndex;

		this.StartBetaInfoFetchIfNeeded();
	}

	private void StartBetaInfoFetchIfNeeded()
	{
		if (!CommonUtilities.Beta && !this.startedBetaInfoFetch && this.SelectedTabIndex == UpdatesTabIndex)
		{
			this.startedBetaInfoFetch = true;

			Task.Run(async () =>
			{
				this.latestBetaVersion = await this.GetLatestBetaVersionAsync();

				this.betaInfoAvailable = false;
				if (latestBetaVersion != null)
				{
					if (latestBetaVersion > Utilities.CurrentVersion)
					{
						this.betaInfoAvailable = true;
					}

					await DispatchUtilities.InvokeAsync(() =>
					{
						this.RaisePropertyChanged(nameof(this.BetaChangelogUrl));
						this.RaisePropertyChanged(nameof(this.BetaSectionVisible));
					});
				}
			});
		}
	}

	private async Task<Version> GetLatestBetaVersionAsync()
	{
		try
		{
			HttpClient client = new HttpClient();
			string updateText = await client.GetStringAsync("https://engy.us/VidCoder/Squirrel-Beta/RELEASES");

			using var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(updateText));
			using var reader = new StreamReader(memoryStream);

			// Find the last line, that will have the latest Beta version
			string line;
			string lastLine = null;
			while ((line = reader.ReadLine()) != null) {
				lastLine = line;
			}

			if (lastLine != null)
			{
				// A line something like:
				// B21889C29564A29F1CB1AFE0DCA085458B13195F VidCoder-Beta-7.6.0-full.nupkg 43323684
				var regex = new Regex(@"-([\d\.]+)-");
				Match match = regex.Match(lastLine);
				if (match.Success)
				{
					string versionString = match.Groups[1].Value;
					return new Version(versionString);
				}
			}
		}
		catch (Exception exception)
		{
			StaticResolver.Resolve<IAppLogger>().Log("Could not get beta version info." + Environment.NewLine + exception);
		}

		return null;
	}

	public IUpdater Updater { get; }

	public IList<string> Tabs { get; }

	private int selectedTabIndex;
	public int SelectedTabIndex
	{
		get => this.selectedTabIndex;
		set
		{
			this.RaiseAndSetIfChanged(ref this.selectedTabIndex, value);
			this.StartBetaInfoFetchIfNeeded();
			Config.OptionsDialogLastTab = this.SelectedTabIndex;
		}
	}

	public LogCoordinator LogCoordinator { get; } = StaticResolver.Resolve<LogCoordinator>();

	public List<InterfaceLanguage> LanguageChoices { get; }

	public List<ComboChoice<AppThemeChoice>> AppThemeChoices { get; }

	public AppThemeChoice AppTheme
	{
		get => CustomConfig.AppTheme;
		set => CustomConfig.AppTheme = value;
	}

	public List<ComboChoice> PriorityChoices { get; }

	public string InterfaceLanguageCode
	{
		get => Config.InterfaceLanguageCode;
		set
		{
			if (value != Config.InterfaceLanguageCode)
			{
				Config.InterfaceLanguageCode = value;
				StaticResolver.Resolve<IMessageBoxService>().Show(this, OptionsRes.NewLanguageRestartDialogMessage);
			}
		}
	}

	public string CurrentVersion
	{
		get
		{
			return string.Format(OptionsRes.CurrentVersionLabel, Utilities.VersionString);
		}
	}

	public List<ComboChoice<DragDropOrder>> DragDropOrderChoices { get; } = new List<ComboChoice<DragDropOrder>>
	{
		new ComboChoice<DragDropOrder>(DragDropOrder.Alphabetical, EnumsRes.DragDropOrder_Alphabetical),
		new ComboChoice<DragDropOrder>(DragDropOrder.Selected, EnumsRes.DragDropOrder_Selected)
	};

	public DragDropOrder DragDropOrder
	{
		get => CustomConfig.DragDropOrder;
		set
		{
			CustomConfig.DragDropOrder = value;
			this.RaisePropertyChanged();
		}
	}

	public bool UpdatesEnabledConfig
	{
		get => Config.UpdatesEnabled;
		set
		{
			Config.UpdatesEnabled = value;
			this.Updater.HandleUpdatedSettings(value);
			this.RaisePropertyChanged();
		}
	}

	public bool SupportsUpdates
	{
		get => Utilities.SupportsUpdates;
	}

	public List<ComboChoice<UpdateMode>> UpdateModeChoices { get; } = new List<ComboChoice<UpdateMode>>
	{
		new ComboChoice<UpdateMode>(UpdateMode.SilentNextLaunch, EnumsRes.UpdateMode_SilentNextLaunch),
		new ComboChoice<UpdateMode>(UpdateMode.PromptApplyImmediately, EnumsRes.UpdateMode_PromptApplyImmediately),
	};

	public UpdateMode UpdateMode
	{
		get => CustomConfig.UpdateMode;
		set
		{
			CustomConfig.UpdateMode = value;
			this.RaisePropertyChanged();
		}
	}

	private ObservableAsPropertyHelper<string> updateStatus;
	public string UpdateStatus => this.updateStatus.Value;

	private ObservableAsPropertyHelper<bool> downloadingUpdate;
	public bool DownloadingUpdate => this.downloadingUpdate.Value;

	private ObservableAsPropertyHelper<int> updateProgressPercent;
	public int UpdateProgressPercent => this.updateProgressPercent.Value;

	public string BetaUpdatesText => CommonUtilities.Beta ? OptionsRes.BetaUpdatesInBeta : OptionsRes.BetaUpdatesNonBeta;

	private Version latestBetaVersion;
	public string BetaChangelogUrl
	{
		get
		{
			if (CommonUtilities.Beta || this.latestBetaVersion == null)
			{
				return string.Empty;
			}

			return Utilities.GetChangelogUrl(this.latestBetaVersion, beta: true);
		}
	}

	public bool BetaSectionVisible => CommonUtilities.Beta || this.betaInfoAvailable;

	public List<IVideoPlayer> PlayerChoices { get; }

	public string SelectedPlayerId
	{
		get => Config.PreferredPlayer;
		set
		{
			Config.PreferredPlayer = value;
			this.RaisePropertyChanged();
		}
	}

	public bool UseCustomVideoPlayer
	{
		get => Config.UseCustomVideoPlayer;
		set
		{
			Config.UseCustomVideoPlayer = value;
			this.RaisePropertyChanged();
		}
	}

	public string CustomVideoPlayer
	{
		get => Config.CustomVideoPlayer;
		set
		{
			Config.CustomVideoPlayer = value;
			this.RaisePropertyChanged();
		}
	}

	public bool UseBuiltInPlayerForPreviews
	{
		get => Config.UseBuiltInPlayerForPreviews;
		set
		{
			Config.UseBuiltInPlayerForPreviews = value;
			this.RaisePropertyChanged();
		}
	}

	public bool UseCustomPreviewFolder
	{
		get => Config.UseCustomPreviewFolder;
		set
		{
			Config.UseCustomPreviewFolder = value;
			this.RaisePropertyChanged();
		}
	}

	public string PreviewOutputFolder
	{
		get => Config.PreviewOutputFolder;
		set
		{
			Config.PreviewOutputFolder = value;
			this.RaisePropertyChanged();
		}
	}

	public bool MinimizeToTray
	{
		get => Config.MinimizeToTray;
		set
		{
			Config.MinimizeToTray = value;
			this.RaisePropertyChanged();
		}
	}

	public bool PlaySoundOnCompletion
	{
		get => Config.PlaySoundOnCompletion;
		set
		{
			Config.PlaySoundOnCompletion = value;
			this.RaisePropertyChanged();
		}
	}

	public bool UseCustomCompletionSound
	{
		get => Config.UseCustomCompletionSound;
		set
		{
			Config.UseCustomCompletionSound = value;
			this.RaisePropertyChanged();
		}
	}

	public string CustomCompletionSound
	{
		get => Config.CustomCompletionSound;
		set
		{
			Config.CustomCompletionSound = value;
			this.RaisePropertyChanged();
		}
	}

	public List<ComboChoice<WatcherMode>> WatcherModeChoices { get; } = new List<ComboChoice<WatcherMode>>
	{
		new ComboChoice<WatcherMode>(WatcherMode.FileSystemWatcher, EnumsRes.WatcherMode_FileSystemWatcher),
		new ComboChoice<WatcherMode>(WatcherMode.Polling, EnumsRes.WatcherMode_Polling),
	};

	public WatcherMode WatcherMode
	{
		get => CustomConfig.WatcherMode;
		set
		{
			WatcherMode oldWatcherMode = CustomConfig.WatcherMode;
			CustomConfig.WatcherMode = value;
			if (oldWatcherMode != this.WatcherMode)
			{
				// The watcher mode changed. We need to tell the watcher process to restart the watch operation.
				StaticResolver.Resolve<WatcherProcessManager>().RefreshFromWatchedFolders();
			}

			this.RaisePropertyChanged();
		}
	}

	public int WatcherPollIntervalSeconds
	{
		get => Config.WatcherPollIntervalSeconds;
		set
		{
			int oldPollIntervalSeconds = Config.WatcherPollIntervalSeconds;
			Config.WatcherPollIntervalSeconds = value;

			if (oldPollIntervalSeconds != this.WatcherPollIntervalSeconds)
			{
				// The watcher poll interval changed. We need to tell the watcher process to restart the watch operation.
				StaticResolver.Resolve<WatcherProcessManager>().RefreshFromWatchedFolders();
			}

			this.RaisePropertyChanged();
		}
	}

	public List<ComboChoice<int>> LogVerbosityChoices { get; } = new List<ComboChoice<int>>
	{
		new ComboChoice<int>(0, LogRes.LogVerbosity_Minimized),
		new ComboChoice<int>(1, LogRes.LogVerbosity_Standard),
		new ComboChoice<int>(2, LogRes.LogVerbosity_Extended),
	};

	public int LogVerbosity
	{
		get => Config.LogVerbosity;
		set
		{
			Config.LogVerbosity = value;
			this.RaisePropertyChanged();
		}
	}

	public bool CopyLogToOutputFolder
	{
		get => Config.CopyLogToOutputFolder;
		set
		{
			Config.CopyLogToOutputFolder = value;
			this.RaisePropertyChanged();
		}
	}

	public bool CopyLogToCustomFolder
	{
		get => Config.CopyLogToCustomFolder;
		set
		{
			Config.CopyLogToCustomFolder = value;
			this.RaisePropertyChanged();
		}
	}


	public string LogCustomFolder
	{
		get => Config.LogCustomFolder;
		set
		{
			Config.LogCustomFolder = value;
			this.RaisePropertyChanged();
		}
	}

	public bool AutoPauseLowBattery
	{
		get => Config.AutoPauseLowBattery;
		set
		{
			Config.AutoPauseLowBattery = value;
			this.RaisePropertyChanged();
		}
	}

	public bool AutoPauseLowDiskSpace
	{
		get => Config.AutoPauseLowDiskSpace;
		set
		{
			Config.AutoPauseLowDiskSpace = value;
			this.RaisePropertyChanged();
		}
	}

	public int AutoPauseLowDiskSpaceGb
	{
		get => Config.AutoPauseLowDiskSpaceGb;
		set
		{
			Config.AutoPauseLowDiskSpaceGb = value;
			this.RaisePropertyChanged();
		}
	}

	public bool AutoPauseProcessesEnabled
	{
		get => Config.AutoPauseProcessesEnabled;
		set
		{
			Config.AutoPauseProcessesEnabled = value;
			this.RaisePropertyChanged();
		}
	}

	public ObservableCollection<string> AutoPauseProcesses { get; }

	private void SaveAutoPauseProcesses()
	{
		var autoPauseList = new List<string>();
		foreach (string process in this.AutoPauseProcesses)
		{
			autoPauseList.Add(process);
		}

		CustomConfig.AutoPauseProcesses = autoPauseList;
	}

	private string selectedProcess;
	public string SelectedProcess
	{
		get => this.selectedProcess;
		set => this.RaiseAndSetIfChanged(ref this.selectedProcess, value);
	}

	public int PreviewCount
	{
		get => Config.PreviewCount;
		set
		{
			Config.PreviewCount = value;
			this.RaisePropertyChanged();
		}
	}

	public string WorkerProcessPriority
	{
		get => Config.WorkerProcessPriority;
		set
		{
			Config.WorkerProcessPriority = value;
			this.RaisePropertyChanged();
		}
	}

	public bool RememberPreviousFiles
	{
		get => Config.RememberPreviousFiles;
		set
		{
			Config.RememberPreviousFiles = value;

			// Clear out any file/folder history when this is disabled.
			if (!value)
			{
				Config.LastCsvFolder = null;
				Config.LastInputFileFolder = null;
				Config.LastOutputFolder = null;
				Config.LastPresetExportFolder = null;
				Config.LastSubtitleFolder = null;
				Config.LastVideoTSFolder = null;

				Config.SourceHistory = null;
			}
		} 
	}

	public bool EnableNVDec
	{
		get => Config.EnableNVDec;
		set
		{
			Config.EnableNVDec = value;
			this.RaisePropertyChanged();
		}
	}

	public bool EnableLibDvdNav
	{
		get => Config.EnableLibDvdNav;
		set
		{
			Config.EnableLibDvdNav = value;
			this.RaisePropertyChanged();
		}
	}

	public bool PreserveModifyTimeFiles
	{
		get => Config.PreserveModifyTimeFiles;
		set
		{
			Config.PreserveModifyTimeFiles = value;
			this.RaisePropertyChanged();
		}
	}

	public bool ResumeEncodingOnRestart
	{
		get => Config.ResumeEncodingOnRestart;
		set
		{
			Config.ResumeEncodingOnRestart = value;
			this.RaisePropertyChanged();
		}
	}

	public bool KeepFailedFiles
	{
		get => Config.KeepFailedFiles;
		set
		{
			Config.KeepFailedFiles = value;
			this.RaisePropertyChanged();
		}
	}

	public bool TriggerEncodeCompleteActionWithErrors
	{
		get => Config.TriggerEncodeCompleteActionWithErrors;
		set
		{
			Config.TriggerEncodeCompleteActionWithErrors = value;
			this.RaisePropertyChanged();
		}
	}

	public bool ShowProgressInWindowTitle
	{
		get => Config.ShowProgressInWindowTitle;
		set
		{
			Config.ShowProgressInWindowTitle = value;
			this.RaisePropertyChanged();
		}
	}

	public bool UseWorkerProcess
	{
		get => Config.UseWorkerProcess;
		set
		{
			if (value != Config.UseWorkerProcess)
			{
				// Override the value that the app uses with the old choice
				CustomConfig.SetWorkerProcessOverride(Config.UseWorkerProcess);

				// Set the stored value to the new choice
				Config.UseWorkerProcess = value;

				StaticResolver.Resolve<IMessageBoxService>().Show(this, OptionsRes.WorkerProcessRestartDialogMessage);

				this.RaisePropertyChanged();
			}
		}
	}

	public int MinimumTitleLengthSeconds
	{
		get => Config.MinimumTitleLengthSeconds;
		set
		{
			Config.MinimumTitleLengthSeconds = value;
			this.RaisePropertyChanged();
		}
	}

	public int EncodeRetries
	{
		get => Config.EncodeRetries;
		set
		{
			Config.EncodeRetries = value;
			this.RaisePropertyChanged();
		}
	}

	private int cpuThrottlingCores;
	public int CpuThrottlingCores
	{
		get => this.cpuThrottlingCores;
		set
		{
			this.RaiseAndSetIfChanged(ref this.cpuThrottlingCores, value);
			Config.CpuThrottlingFraction = (double)value / this.CpuThrottlingMaxCores;
		}
	}

	public int CpuThrottlingMaxCores
	{
		get
		{
			return Environment.ProcessorCount;
		}
	}

	public int MaxSimultaneousEncodes
	{
		get => Config.MaxSimultaneousEncodes;
		set
		{
			Config.MaxSimultaneousEncodes = value;
			this.RaisePropertyChanged();
		}
	}

	public bool CapNVEnc
	{
		get => Config.CapNVEnc;
		set
		{
			Config.CapNVEnc = value;
			this.RaisePropertyChanged();
		}
	}

	private ObservableAsPropertyHelper<string> cpuThrottlingDisplay;
	public string CpuThrottlingDisplay => this.cpuThrottlingDisplay.Value;


	private ReactiveCommand<Unit, Unit> browseVideoPlayer;
	public ICommand BrowseVideoPlayer
	{
		get
		{
			return this.browseVideoPlayer ?? (this.browseVideoPlayer = ReactiveCommand.Create(
				() =>
				{
					string fileName = FileService.Instance.GetFileNameLoad(
						title: OptionsRes.VideoPlayerFilePickTitle,
						filter: Utilities.GetFilePickerFilter("exe"));

					if (fileName != null)
					{
						this.CustomVideoPlayer = fileName;
					}
				},
				this.WhenAnyValue(x => x.UseCustomVideoPlayer)));
		}
	}

	private ReactiveCommand<Unit, Unit> browseCompletionSound;
	public ICommand BrowseCompletionSound
	{
		get
		{
			return this.browseCompletionSound ?? (this.browseCompletionSound = ReactiveCommand.Create(
				() =>
				{
					string fileName = FileService.Instance.GetFileNameLoad(
						title: OptionsRes.CompletionWavFilePickTitle,
						filter: Utilities.GetFilePickerFilter("wav"));

					if (fileName != null)
					{
						this.CustomCompletionSound = fileName;
					}
				},
				this.WhenAnyValue(x => x.UseCustomCompletionSound)));
		}
	}

	private ReactiveCommand<Unit, Unit> openEncodingSettings;
	public ICommand OpenEncodingSettings
	{
		get
		{
			return this.openEncodingSettings ?? (this.openEncodingSettings = ReactiveCommand.Create(() =>
			{
				this.windowManager.Close(this);
				this.windowManager.OpenOrFocusWindow<EncodingWindowViewModel>();
			}));
		}
	}

	private ReactiveCommand<Unit, Unit> openPicker;
	public ICommand OpenPicker
	{
		get
		{
			return this.openPicker ?? (this.openPicker = ReactiveCommand.Create(() =>
			{
				this.windowManager.Close(this);
				this.windowManager.OpenOrFocusWindow<PickerWindowViewModel>();
			}));
		}
	}

	private ReactiveCommand<Unit, Unit> pickLogCustomFolder;
	public ICommand PickLogCustomFolder
	{
		get
		{
			return this.pickLogCustomFolder ?? (this.pickLogCustomFolder = ReactiveCommand.Create(() =>
			{
				string newFolder = FileService.Instance.GetFolderName(null);
				if (newFolder != null)
				{
					this.LogCustomFolder = newFolder;
				}
			}));
		}
	}

	private ReactiveCommand<Unit, Unit> browsePreviewFolder;
	public ICommand BrowsePreviewFolder
	{
		get
		{
			return this.browsePreviewFolder ?? (this.browsePreviewFolder = ReactiveCommand.Create(() =>
			{
				string newFolder = FileService.Instance.GetFolderName(null);
				if (newFolder != null)
				{
					this.PreviewOutputFolder = newFolder;
				}
			}));
		}
	}

	private ReactiveCommand<Unit, Unit> openAddProcessDialog;
	public ICommand OpenAddProcessDialog
	{
		get
		{
			return this.openAddProcessDialog ?? (this.openAddProcessDialog = ReactiveCommand.Create(() =>
			{
				var addProcessVM = new AddAutoPauseProcessDialogViewModel();
				StaticResolver.Resolve<IWindowManager>().OpenDialog(addProcessVM, this);

				if (addProcessVM.DialogResult)
				{
					string newProcessName = addProcessVM.ProcessName; ;
					if (!string.IsNullOrWhiteSpace(newProcessName) && !this.AutoPauseProcesses.Contains(newProcessName))
					{
						this.AutoPauseProcesses.Add(addProcessVM.ProcessName);
						this.SaveAutoPauseProcesses();
					}
				}
			}));
		}
	}

	private ReactiveCommand<Unit, Unit> removeProcess;
	public ICommand RemoveProcess
	{
		get
		{
			return this.removeProcess ?? (this.removeProcess = ReactiveCommand.Create(
				() =>
				{
					this.AutoPauseProcesses.Remove(this.SelectedProcess);
					this.SaveAutoPauseProcesses();
				},
				this.WhenAnyValue(x => x.SelectedProcess).Select(selectedProcess => selectedProcess != null)));
		}
	}

	private ReactiveCommand<Unit, Unit> checkUpdate;
	public ICommand CheckUpdate
	{
		get
		{
			return this.checkUpdate ?? (this.checkUpdate = ReactiveCommand.Create(
				() =>
				{
					this.Updater.CheckUpdates(isManualCheck: true);
				},
				this.Updater
					.WhenAnyValue(x => x.State)
					.Select(state =>
					{
						return Utilities.SupportsUpdates &&
								(state == UpdateState.Failed ||
								state == UpdateState.NotStarted ||
								state == UpdateState.UpToDate);
					})
					.ObserveOnDispatcher()));
		}
	}

	private ReactiveCommand<Unit, Unit> applyUpdate;
	public ICommand ApplyUpdate
	{
		get
		{
			return this.applyUpdate ?? (this.applyUpdate = ReactiveCommand.Create(
				() =>
				{
					var updateManager = new UpdateManager(Utilities.VelopackUpdateUrl);
					updateManager.ApplyUpdatesAndRestart();
				}));
		}
	}
}
