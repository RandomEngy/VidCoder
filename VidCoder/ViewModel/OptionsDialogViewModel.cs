using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Resources;
using System.Threading.Tasks;
using VidCoder.Model;
using VidCoder.Resources;
using VidCoder.Services;
using VidCoder.Services.Windows;
using ReactiveUI;
using VidCoder.Extensions;
using VidCoderCommon;

namespace VidCoder.ViewModel
{
	public class OptionsDialogViewModel : OkCancelDialogViewModel
	{
		public const int UpdatesTabIndex = 4;

		private ObservableCollection<string> autoPauseProcesses;

		private List<IVideoPlayer> playerChoices;
		private List<InterfaceLanguage> languageChoices;
		private List<ComboChoice> priorityChoices;

#pragma warning disable 169
		private UpdateInfo betaInfo;
		private bool betaInfoAvailable;
#pragma warning restore 169

		private IUpdater updater;

		public OptionsDialogViewModel(IUpdater updateService)
		{
			this.updater = updateService;

			this.Tabs = new List<string>
			{
				OptionsRes.GeneralTab,			// 0
				OptionsRes.FileNamingTab,		// 1
				OptionsRes.ProcessTab,			// 2
				OptionsRes.AdvancedTab,			// 3
			};

			if (Utilities.SupportsUpdates)
			{
				this.Tabs.Add(OptionsRes.UpdatesTab); // 4
			}

			// UpdateStatus
			this.updater
				.WhenAnyValue(x => x.State)
				.Select(state =>
				{
					switch (state)
					{
						case UpdateState.NotStarted:
							return string.Empty;
						case UpdateState.DownloadingInfo:
							return OptionsRes.DownloadingInfoStatus;
						case UpdateState.DownloadingInstaller:
							return string.Format(OptionsRes.DownloadingStatus, this.updater.LatestVersion);
						case UpdateState.UpToDate:
							return OptionsRes.UpToDateStatus;
						case UpdateState.InstallerReady:
							return string.Format(OptionsRes.UpdateReadyStatus, this.updater.LatestVersion);
						case UpdateState.Failed:
							return OptionsRes.UpdateFailedStatus;
						case UpdateState.NotSupported32BitOS:
							return CommonRes.UpdatesDisabled32BitOSMessage;
						default:
							throw new ArgumentOutOfRangeException();
					}
				})
				.ToProperty(this, x => x.UpdateStatus, out this.updateStatus);

			// UpdateDownloading
			this.updater
				.WhenAnyValue(x => x.State)
				.Select(state => state == UpdateState.DownloadingInstaller)
				.ToProperty(this, x => x.UpdateDownloading, out this.updateDownloading);

			// UpdateProgressPercent
			this.updater
				.WhenAnyValue(x => x.UpdateDownloadProgressFraction)
				.Select(downloadFraction => downloadFraction * 100)
				.ToProperty(this, x => x.UpdateProgressPercent, out this.updateProgressPercent);

			// CpuThrottlingDisplay
			this.WhenAnyValue(x => x.CpuThrottlingCores, x => x.CpuThrottlingMaxCores, (cores, maxCores) =>
			{
				double currentFraction = (double)cores / maxCores;
				return currentFraction.ToString("p0");
			}).ToProperty(this, x => x.CpuThrottlingDisplay, out this.cpuThrottlingDisplay);

			this.SaveSettings = ReactiveCommand.Create();
			this.SaveSettings.Subscribe(_ => this.SaveSettingsImpl());

			this.BrowseVideoPlayer = ReactiveCommand.Create(this.WhenAnyValue(x => x.UseCustomVideoPlayer));
			this.BrowseVideoPlayer.Subscribe(_ => this.BrowseVideoPlayerImpl());

			this.BrowseCompletionSound = ReactiveCommand.Create(this.WhenAnyValue(x => x.UseCustomCompletionSound));
			this.BrowseCompletionSound.Subscribe(_ => this.BrowseCompletionSoundImpl());

			this.BrowsePath = ReactiveCommand.Create();
			this.BrowsePath.Subscribe(_ => this.BrowsePathImpl());

			this.BrowsePreviewFolder = ReactiveCommand.Create();
			this.BrowsePreviewFolder.Subscribe(_ => this.BrowsePreviewFolderImpl());

			this.OpenAddProcessDialog = ReactiveCommand.Create();
			this.OpenAddProcessDialog.Subscribe(_ => this.OpenAddProcessDialogImpl());

			this.RemoveProcess = ReactiveCommand.Create(this.WhenAnyValue(x => x.SelectedProcess).Select(selectedProcess => selectedProcess != null));
			this.RemoveProcess.Subscribe(_ => this.RemoveProcessImpl());

			bool logFolderExists = Directory.Exists(Utilities.LogsFolder);
			this.OpenLogFolder = ReactiveCommand.Create(MvvmUtilities.CreateConstantObservable(logFolderExists));
			this.OpenLogFolder.Subscribe(_ => this.OpenLogFolderImpl());

			this.CheckUpdate = ReactiveCommand.Create(this.updater.WhenAnyValue(x => x.State).Select(state =>
			{
				return Config.UpdatesEnabled &&
					(state == UpdateState.Failed ||
					state == UpdateState.NotStarted ||
					state == UpdateState.UpToDate);
			}));
			this.CheckUpdate.Subscribe(_ => this.CheckUpdateImpl());

			this.updatesEnabledConfig = Config.UpdatesEnabled;
			this.defaultPath = Config.AutoNameOutputFolder;
			this.customFormat = Config.AutoNameCustomFormat;
			this.customFormatString = Config.AutoNameCustomFormatString;
			this.outputToSourceDirectory = Config.OutputToSourceDirectory;
			this.preserveFolderStructureInBatch = Config.PreserveFolderStructureInBatch;
			this.useCustomPreviewFolder = Config.UseCustomPreviewFolder;
			this.previewOutputFolder = Config.PreviewOutputFolder;
			this.whenFileExists = CustomConfig.WhenFileExists;
			this.whenFileExistsBatch = CustomConfig.WhenFileExistsBatch;
			this.minimizeToTray = Config.MinimizeToTray;
			this.useCustomVideoPlayer = Config.UseCustomVideoPlayer;
			this.customVideoPlayer = Config.CustomVideoPlayer;
			this.useBuiltInPlayerForPreviews = Config.UseBuiltInPlayerForPreviews;
			this.playSoundOnCompletion = Config.PlaySoundOnCompletion;
			this.useCustomCompletionSound = Config.UseCustomCompletionSound;
			this.customCompletionSound = Config.CustomCompletionSound;
			this.workerProcessPriority = Config.WorkerProcessPriority;
			this.logVerbosity = Config.LogVerbosity;
			this.copyLogToOutputFolder = Config.CopyLogToOutputFolder;
			this.previewCount = Config.PreviewCount;
			this.rememberPreviousFiles = Config.RememberPreviousFiles;
			this.showAudioTrackNameField = Config.ShowAudioTrackNameField;
			this.enableLibDvdNav = Config.EnableLibDvdNav;
			this.deleteSourceFilesOnClearingCompleted = Config.DeleteSourceFilesOnClearingCompleted;
			this.preserveModifyTimeFiles = Config.PreserveModifyTimeFiles;
			this.resumeEncodingOnRestart = Config.ResumeEncodingOnRestart;
			this.useWorkerProcess = Config.UseWorkerProcess;
			this.minimumTitleLengthSeconds = Config.MinimumTitleLengthSeconds;
			this.autoPauseProcesses = new ObservableCollection<string>();
			this.videoFileExtensions = Config.VideoFileExtensions;
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
					this.autoPauseProcesses.Add(process);
				}
			}

			// List of language codes and names: http://msdn.microsoft.com/en-us/goglobal/bb896001.aspx

			this.languageChoices =
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
						new InterfaceLanguage { CultureCode = "hu-HU", Display = "Magyar / Hungarian" },
						new InterfaceLanguage { CultureCode = "nl-BE", Display = "Nederlands / Dutch" },
						new InterfaceLanguage { CultureCode = "pl-PL", Display = "polski / Polish" },
						new InterfaceLanguage { CultureCode = "pt-PT", Display = "Português / Portuguese" },
						new InterfaceLanguage { CultureCode = "pt-BR", Display = "Português (Brasil) / Portuguese (Brazil)" },
						new InterfaceLanguage { CultureCode = "tr-TR", Display = "Türkçe / Turkish" },
						new InterfaceLanguage { CultureCode = "ka-GE", Display = "ქართული / Georgian" },
						new InterfaceLanguage { CultureCode = "ru-RU", Display = "русский / Russian" },
						new InterfaceLanguage { CultureCode = "ko-KO", Display = "한국어 / Korean" },
						new InterfaceLanguage { CultureCode = "zh-Hans", Display = "中文(简体) / Chinese (Simplified)" },
						new InterfaceLanguage { CultureCode = "zh-Hant", Display = "中文(繁體) / Chinese (Traditional)" },
						new InterfaceLanguage { CultureCode =  "ja-JP", Display = "日本語 / Japanese" },
					};

			this.priorityChoices = new List<ComboChoice>
				{
					new ComboChoice("High", OptionsRes.Priority_High),
					new ComboChoice("AboveNormal", OptionsRes.Priority_AboveNormal),
					new ComboChoice("Normal", OptionsRes.Priority_Normal),
					new ComboChoice("BelowNormal", OptionsRes.Priority_BelowNormal),
					new ComboChoice("Idle", OptionsRes.Priority_Idle),
				};

			this.interfaceLanguage = this.languageChoices.FirstOrDefault(l => l.CultureCode == Config.InterfaceLanguageCode);
			if (this.interfaceLanguage == null)
			{
				this.interfaceLanguage = this.languageChoices[0];
			}

			this.playerChoices = Players.All;
			if (this.playerChoices.Count > 0)
			{
				this.selectedPlayer = this.playerChoices[0];

				foreach (IVideoPlayer player in this.playerChoices)
				{
					if (player.Id == Config.PreferredPlayer)
					{
						this.selectedPlayer = player;
						break;
					}
				}
			}

			if (!CommonUtilities.Beta)
			{
				Task.Run(async () =>
				{
					this.betaInfo = await Updater.GetUpdateInfoAsync(beta: true);

					this.betaInfoAvailable = false;
					if (this.betaInfo != null)
					{
						if (this.betaInfo.LatestVersion.FillInWithZeroes() > Utilities.CurrentVersion)
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

			int tabIndex = Config.OptionsDialogLastTab;
			if (tabIndex >= this.Tabs.Count)
			{
				tabIndex = 0;
			}

			this.SelectedTabIndex = tabIndex;
		}

		public override bool OnClosing()
		{
			Config.OptionsDialogLastTab = this.SelectedTabIndex;

			if (this.DialogResult)
			{
				Ioc.Get<OutputPathService>().NotifyDefaultOutputFolderChanged();
			}

			return base.OnClosing();
		}

		public IList<string> Tabs { get; }

		private int selectedTabIndex;
		public int SelectedTabIndex
		{
			get { return this.selectedTabIndex; }
			set { this.RaiseAndSetIfChanged(ref this.selectedTabIndex, value); }
		}

		public List<InterfaceLanguage> LanguageChoices
		{
			get
			{
				return this.languageChoices;
			}
		}

		public List<ComboChoice> PriorityChoices
		{
			get
			{
				return this.priorityChoices;
			}
		}

		private InterfaceLanguage interfaceLanguage;
		public InterfaceLanguage InterfaceLanguage
		{
			get { return this.interfaceLanguage; }
			set { this.RaiseAndSetIfChanged(ref this.interfaceLanguage, value); }
		}

		public string CurrentVersion
		{
			get
			{
				return string.Format(OptionsRes.CurrentVersionLabel, Utilities.VersionString);
			}
		}

		private bool updatesEnabledConfig;
		public bool UpdatesEnabledConfig
		{
			get { return this.updatesEnabledConfig; }
			set { this.RaiseAndSetIfChanged(ref this.updatesEnabledConfig, value); }
		}

		private ObservableAsPropertyHelper<string> updateStatus;
		public string UpdateStatus => this.updateStatus.Value;

		private ObservableAsPropertyHelper<bool> updateDownloading;
		public bool UpdateDownloading => this.updateDownloading.Value;

		private ObservableAsPropertyHelper<double> updateProgressPercent;
		public double UpdateProgressPercent => this.updateProgressPercent.Value;

		public string BetaUpdatesText => CommonUtilities.Beta ? OptionsRes.BetaUpdatesInBeta : OptionsRes.BetaUpdatesNonBeta;

		public string BetaChangelogUrl => CommonUtilities.Beta ? string.Empty : (this.betaInfo?.ChangelogUrl ?? string.Empty);

		public bool BetaSectionVisible => CommonUtilities.Beta || this.betaInfoAvailable;

		public List<IVideoPlayer> PlayerChoices => this.playerChoices;

		private IVideoPlayer selectedPlayer;
		public IVideoPlayer SelectedPlayer
		{
			get { return this.selectedPlayer; }
			set { this.RaiseAndSetIfChanged(ref this.selectedPlayer, value); }
		}

		private bool useCustomVideoPlayer;
		public bool UseCustomVideoPlayer
		{
			get { return this.useCustomVideoPlayer; }
			set { this.RaiseAndSetIfChanged(ref this.useCustomVideoPlayer, value); }
		}

		private string customVideoPlayer;
		public string CustomVideoPlayer
		{
			get { return this.customVideoPlayer; }
			set { this.RaiseAndSetIfChanged(ref this.customVideoPlayer, value); }
		}

		private bool useBuiltInPlayerForPreviews;
		public bool UseBuiltInPlayerForPreviews
		{
			get { return this.useBuiltInPlayerForPreviews; }
			set { this.RaiseAndSetIfChanged(ref this.useBuiltInPlayerForPreviews, value); }
		}

		private string defaultPath;
		public string DefaultPath
		{
			get { return this.defaultPath; }
			set { this.RaiseAndSetIfChanged(ref this.defaultPath, value); }
		}

		private bool customFormat;
		public bool CustomFormat
		{
			get { return this.customFormat; }
			set { this.RaiseAndSetIfChanged(ref this.customFormat, value); }
		}

		private string customFormatString;
		public string CustomFormatString
		{
			get { return this.customFormatString; }
			set { this.RaiseAndSetIfChanged(ref this.customFormatString, value); }
		}

		public string AvailableOptionsText
		{
			get
			{
				var manager = new ResourceManager(typeof(OptionsRes));
				return string.Format(manager.GetString("FileNameFormatOptions"), "{source} {title} {range} {preset} {date} {time} {quality} {parent} {titleduration}");
			}
		}

		private bool outputToSourceDirectory;
		public bool OutputToSourceDirectory
		{
			get { return this.outputToSourceDirectory; }
			set { this.RaiseAndSetIfChanged(ref this.outputToSourceDirectory, value); }
		}

		private bool preserveFolderStructureInBatch;
		public bool PreserveFolderStructureInBatch
		{
			get { return this.preserveFolderStructureInBatch; }
			set { this.RaiseAndSetIfChanged(ref this.preserveFolderStructureInBatch, value); }
		}

		private bool useCustomPreviewFolder;
		public bool UseCustomPreviewFolder
		{
			get { return this.useCustomPreviewFolder; }
			set { this.RaiseAndSetIfChanged(ref this.useCustomPreviewFolder, value); }
		}

		private string previewOutputFolder;
		public string PreviewOutputFolder
		{
			get { return this.previewOutputFolder; }
			set { this.RaiseAndSetIfChanged(ref this.previewOutputFolder, value); }
		}

		private WhenFileExists whenFileExists;
		public WhenFileExists WhenFileExists
		{
			get { return this.whenFileExists; }
			set { this.RaiseAndSetIfChanged(ref this.whenFileExists, value); }
		}

		private WhenFileExists whenFileExistsBatch;
		public WhenFileExists WhenFileExistsBatch
		{
			get { return this.whenFileExistsBatch; }
			set { this.RaiseAndSetIfChanged(ref this.whenFileExistsBatch, value); }
		}

		private bool minimizeToTray;
		public bool MinimizeToTray
		{
			get { return this.minimizeToTray; }
			set { this.RaiseAndSetIfChanged(ref this.minimizeToTray, value); }
		}

		private bool playSoundOnCompletion;
		public bool PlaySoundOnCompletion
		{
			get { return this.playSoundOnCompletion; }
			set { this.RaiseAndSetIfChanged(ref this.playSoundOnCompletion, value); }
		}

		private bool useCustomCompletionSound;
		public bool UseCustomCompletionSound
		{
			get { return this.useCustomCompletionSound; }
			set { this.RaiseAndSetIfChanged(ref this.useCustomCompletionSound, value); }
		}

		private string customCompletionSound;
		public string CustomCompletionSound
		{
			get { return this.customCompletionSound; }
			set { this.RaiseAndSetIfChanged(ref this.customCompletionSound, value); }
		}

		private int logVerbosity;
		public int LogVerbosity
		{
			get { return this.logVerbosity; }
			set { this.RaiseAndSetIfChanged(ref this.logVerbosity, value); }
		}

		private bool copyLogToOutputFolder;
		public bool CopyLogToOutputFolder
		{
			get { return this.copyLogToOutputFolder; }
			set { this.RaiseAndSetIfChanged(ref this.copyLogToOutputFolder, value); }
		}

		public ObservableCollection<string> AutoPauseProcesses
		{
			get
			{
				return this.autoPauseProcesses;
			}
		}

		private string selectedProcess;
		public string SelectedProcess
		{
			get { return this.selectedProcess; }
			set { this.RaiseAndSetIfChanged(ref this.selectedProcess, value); }
		}

		private int previewCount;
		public int PreviewCount
		{
			get { return this.previewCount; }
			set { this.RaiseAndSetIfChanged(ref this.previewCount, value); }
		}

		private string workerProcessPriority;
		public string WorkerProcessPriority
		{
			get { return this.workerProcessPriority; }
			set { this.RaiseAndSetIfChanged(ref this.workerProcessPriority, value); }
		}

		private bool rememberPreviousFiles;
		public bool RememberPreviousFiles
		{
			get { return this.rememberPreviousFiles; }
			set { this.RaiseAndSetIfChanged(ref this.rememberPreviousFiles, value); }
		}

		private bool showAudioTrackNameField;
		public bool ShowAudioTrackNameField
		{
			get { return this.showAudioTrackNameField; }
			set { this.RaiseAndSetIfChanged(ref this.showAudioTrackNameField, value); }
		}

		private bool enableLibDvdNav;
		public bool EnableLibDvdNav
		{
			get { return this.enableLibDvdNav; }
			set { this.RaiseAndSetIfChanged(ref this.enableLibDvdNav, value); }
		}

		private bool deleteSourceFilesOnClearingCompleted;
		public bool DeleteSourceFilesOnClearingCompleted
		{
			get { return this.deleteSourceFilesOnClearingCompleted; }
			set { this.RaiseAndSetIfChanged(ref this.deleteSourceFilesOnClearingCompleted, value); }
		}

		private bool preserveModifyTimeFiles;
		public bool PreserveModifyTimeFiles
		{
			get { return this.preserveModifyTimeFiles; }
			set { this.RaiseAndSetIfChanged(ref this.preserveModifyTimeFiles, value); }
		}

		private bool resumeEncodingOnRestart;
		public bool ResumeEncodingOnRestart
		{
			get { return this.resumeEncodingOnRestart; }
			set { this.RaiseAndSetIfChanged(ref this.resumeEncodingOnRestart, value); }
		}

		private bool useWorkerProcess;
		public bool UseWorkerProcess
		{
			get { return this.useWorkerProcess; }
			set { this.RaiseAndSetIfChanged(ref this.useWorkerProcess, value); }
		}

		private int minimumTitleLengthSeconds;
		public int MinimumTitleLengthSeconds
		{
			get { return this.minimumTitleLengthSeconds; }
			set { this.RaiseAndSetIfChanged(ref this.minimumTitleLengthSeconds, value); }
		}

		private string videoFileExtensions;
		public string VideoFileExtensions
		{
			get { return this.videoFileExtensions; }
			set { this.RaiseAndSetIfChanged(ref this.videoFileExtensions, value); }
		}

		private int cpuThrottlingCores;
		public int CpuThrottlingCores
		{
			get { return this.cpuThrottlingCores; }
			set { this.RaiseAndSetIfChanged(ref this.cpuThrottlingCores, value); }
		}

		public int CpuThrottlingMaxCores
		{
			get
			{
				return Environment.ProcessorCount;
			}
		}

		private ObservableAsPropertyHelper<string> cpuThrottlingDisplay;
		public string CpuThrottlingDisplay => this.cpuThrottlingDisplay.Value;

		public ReactiveCommand<object> SaveSettings { get; }
		private void SaveSettingsImpl()
		{
			using (SQLiteTransaction transaction = Database.Connection.BeginTransaction())
			{
				if (Config.UpdatesEnabled != this.UpdatesEnabledConfig)
				{
					Config.UpdatesEnabled = this.UpdatesEnabledConfig;
					this.updater.HandleUpdatedSettings(this.UpdatesEnabledConfig);
				}

				if (Config.InterfaceLanguageCode != this.InterfaceLanguage.CultureCode)
				{
					Config.InterfaceLanguageCode = this.InterfaceLanguage.CultureCode;
					Ioc.Get<IMessageBoxService>().Show(this, OptionsRes.NewLanguageRestartDialogMessage);
				}

				Config.AutoNameOutputFolder = this.DefaultPath;
				Config.AutoNameCustomFormat = this.CustomFormat;
				Config.AutoNameCustomFormatString = this.CustomFormatString;
				Config.OutputToSourceDirectory = this.OutputToSourceDirectory;
				Config.PreserveFolderStructureInBatch = this.PreserveFolderStructureInBatch;
				Config.UseCustomPreviewFolder = this.UseCustomPreviewFolder;
				Config.PreviewOutputFolder = this.PreviewOutputFolder;
				CustomConfig.WhenFileExists = this.WhenFileExists;
				CustomConfig.WhenFileExistsBatch = this.WhenFileExistsBatch;
				Config.MinimizeToTray = this.MinimizeToTray;
				Config.UseCustomVideoPlayer = this.UseCustomVideoPlayer;
				Config.CustomVideoPlayer = this.CustomVideoPlayer;
				Config.UseBuiltInPlayerForPreviews = this.UseBuiltInPlayerForPreviews;
				Config.PlaySoundOnCompletion = this.PlaySoundOnCompletion;
				Config.UseCustomCompletionSound = this.UseCustomCompletionSound;
				Config.CustomCompletionSound = this.CustomCompletionSound;
				Config.WorkerProcessPriority = this.WorkerProcessPriority;
				Config.LogVerbosity = this.LogVerbosity;
				Config.CopyLogToOutputFolder = this.CopyLogToOutputFolder;
				var autoPauseList = new List<string>();
				foreach (string process in this.AutoPauseProcesses)
				{
					autoPauseList.Add(process);
				}

				CustomConfig.AutoPauseProcesses = autoPauseList;
				Config.PreviewCount = this.PreviewCount;
				Config.RememberPreviousFiles = this.RememberPreviousFiles;
				// Clear out any file/folder history when this is disabled.
				if (!this.RememberPreviousFiles)
				{
					Config.LastCsvFolder = null;
					Config.LastInputFileFolder = null;
					Config.LastOutputFolder = null;
					Config.LastPresetExportFolder = null;
					Config.LastSrtFolder = null;
					Config.LastVideoTSFolder = null;

					Config.SourceHistory = null;
				}

				Config.ShowAudioTrackNameField = this.ShowAudioTrackNameField;
				Config.EnableLibDvdNav = this.EnableLibDvdNav;
				Config.DeleteSourceFilesOnClearingCompleted = this.DeleteSourceFilesOnClearingCompleted;
				Config.PreserveModifyTimeFiles = this.PreserveModifyTimeFiles;
				Config.ResumeEncodingOnRestart = this.ResumeEncodingOnRestart;
				Config.UseWorkerProcess = this.UseWorkerProcess;
				Config.MinimumTitleLengthSeconds = this.MinimumTitleLengthSeconds;
				Config.VideoFileExtensions = this.VideoFileExtensions;
				Config.CpuThrottlingFraction = (double)this.CpuThrottlingCores / this.CpuThrottlingMaxCores;

				Config.PreferredPlayer = this.selectedPlayer.Id;

				transaction.Commit();
			}

			this.Accept.Execute(null);
		}

		public ReactiveCommand<object> BrowseVideoPlayer { get; }
		private void BrowseVideoPlayerImpl()
		{
			string fileName = FileService.Instance.GetFileNameLoad(
				title: OptionsRes.VideoPlayerFilePickTitle,
				filter: Utilities.GetFilePickerFilter("exe"));

			if (fileName != null)
			{
				this.CustomVideoPlayer = fileName;
			}
		}

		public ReactiveCommand<object> BrowseCompletionSound { get; }
		private void BrowseCompletionSoundImpl()
		{
			string fileName = FileService.Instance.GetFileNameLoad(
				title: OptionsRes.CompletionWavFilePickTitle,
				filter: Utilities.GetFilePickerFilter("wav"));

			if (fileName != null)
			{
				this.CustomCompletionSound = fileName;
			}
		}

		public ReactiveCommand<object> BrowsePath { get; }
		private void BrowsePathImpl()
		{
			string initialDirectory = null;
			if (Directory.Exists(this.DefaultPath))
			{
				initialDirectory = this.DefaultPath;
			}

			string newFolder = FileService.Instance.GetFolderName(initialDirectory, OptionsRes.ChooseDefaultOutputFolder);
			if (newFolder != null)
			{
				this.DefaultPath = newFolder;
			}
		}

		public ReactiveCommand<object> BrowsePreviewFolder { get; }
		private void BrowsePreviewFolderImpl()
		{
			string newFolder = FileService.Instance.GetFolderName(null);
			if (newFolder != null)
			{
				this.PreviewOutputFolder = newFolder;
			}
		}

		public ReactiveCommand<object> OpenAddProcessDialog { get; }
		private void OpenAddProcessDialogImpl()
		{
			var addProcessVM = new AddAutoPauseProcessDialogViewModel();
			Ioc.Get<IWindowManager>().OpenDialog(addProcessVM, this);

			if (addProcessVM.DialogResult)
			{
				string newProcessName = addProcessVM.ProcessName; ;
				if (!string.IsNullOrWhiteSpace(newProcessName) && !this.AutoPauseProcesses.Contains(newProcessName))
				{
					this.AutoPauseProcesses.Add(addProcessVM.ProcessName);
				}
			}
		}

		public ReactiveCommand<object> RemoveProcess { get; }
		private void RemoveProcessImpl()
		{
			this.AutoPauseProcesses.Remove(this.SelectedProcess);
		}

		public ReactiveCommand<object> OpenLogFolder { get; }
		private void OpenLogFolderImpl()
		{
			string logFolder = Utilities.LogsFolder;

			if (Directory.Exists(logFolder))
			{
				FileService.Instance.LaunchFile(logFolder);
			}
		}

		public ReactiveCommand<object> CheckUpdate { get; }
		private void CheckUpdateImpl()
		{
			this.updater.CheckUpdates();
		}
	}
}
