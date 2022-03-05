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
using Squirrel;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;

namespace VidCoder.ViewModel
{
	public class OptionsDialogViewModel : OkCancelDialogViewModel
	{
		public const int UpdatesTabIndex = 3;

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

			// UpdateDownloading
			this.Updater
				.WhenAnyValue(x => x.State)
				.Select(state => state == UpdateState.Checking)
				.ToProperty(this, x => x.CheckingForUpdate, out this.checkingForUpdate);

			// UpdateProgressPercent
			this.Updater
				.WhenAnyValue(x => x.UpdateCheckProgressPercent)
				.ToProperty(this, x => x.UpdateProgressPercent, out this.updateProgressPercent);

			// CpuThrottlingDisplay
			this.WhenAnyValue(x => x.CpuThrottlingCores, x => x.CpuThrottlingMaxCores, (cores, maxCores) =>
			{
				double currentFraction = (double)cores / maxCores;
				return currentFraction.ToString("p0");
			}).ToProperty(this, x => x.CpuThrottlingDisplay, out this.cpuThrottlingDisplay, deferSubscription:true, scheduler: Scheduler.Immediate);

			this.updatesEnabledConfig = Config.UpdatesEnabled;
			this.updateMode = CustomConfig.UpdateMode;
			this.useCustomPreviewFolder = Config.UseCustomPreviewFolder;
			this.previewOutputFolder = Config.PreviewOutputFolder;
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
			this.copyLogToCustomFolder = Config.CopyLogToCustomFolder;
			this.logCustomFolder = Config.LogCustomFolder;
			this.dragDropOrder = CustomConfig.DragDropOrder;
			this.previewCount = Config.PreviewCount;
			this.rememberPreviousFiles = Config.RememberPreviousFiles;
			this.enableLibDvdNav = Config.EnableLibDvdNav;
			this.preserveModifyTimeFiles = Config.PreserveModifyTimeFiles;
			this.resumeEncodingOnRestart = Config.ResumeEncodingOnRestart;
			this.keepFailedFiles = Config.KeepFailedFiles;
			this.triggerEncodeCompleteActionWithErrors = Config.TriggerEncodeCompleteActionWithErrors;
			this.useWorkerProcess = Config.UseWorkerProcess;
			this.minimumTitleLengthSeconds = Config.MinimumTitleLengthSeconds;
			this.autoPauseLowBattery = Config.AutoPauseLowBattery;
			this.autoPauseLowDiskSpace = Config.AutoPauseLowDiskSpace;
			this.autoPauseLowDiskSpaceGb = Config.AutoPauseLowDiskSpaceGb;
			this.AutoPauseProcesses = new ObservableCollection<string>();
			this.videoFileExtensions = Config.VideoFileExtensions;
			this.encodeRetries = Config.EncodeRetries;
			this.cpuThrottlingCores = (int)Math.Round(this.CpuThrottlingMaxCores * Config.CpuThrottlingFraction);
			if (this.cpuThrottlingCores < 1)
			{
				this.cpuThrottlingCores = 1;
			}

			if (this.cpuThrottlingCores > this.CpuThrottlingMaxCores)
			{
				this.cpuThrottlingCores = this.CpuThrottlingMaxCores;
			}

			this.maxSimultaneousEncodes = Config.MaxSimultaneousEncodes;

			List<string> autoPauseList = CustomConfig.AutoPauseProcesses;
			if (autoPauseList != null)
			{
				foreach (string process in autoPauseList)
				{
					this.AutoPauseProcesses.Add(process);
				}
			}

			// List of language codes and names: http://msdn.microsoft.com/en-us/goglobal/bb896001.aspx

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
						new InterfaceLanguage { CultureCode = "hu-HU", Display = "Magyar / Hungarian" },
						new InterfaceLanguage { CultureCode = "nl-BE", Display = "Nederlands / Dutch" },
						new InterfaceLanguage { CultureCode = "pl-PL", Display = "polski / Polish" },
						new InterfaceLanguage { CultureCode = "pt-PT", Display = "Português / Portuguese" },
						new InterfaceLanguage { CultureCode = "pt-BR", Display = "Português (Brasil) / Portuguese (Brazil)" },
						new InterfaceLanguage { CultureCode = "tr-TR", Display = "Türkçe / Turkish" },
						new InterfaceLanguage { CultureCode = "vi-VN", Display = "Tiếng Việt / Vietnamese" },
						new InterfaceLanguage { CultureCode = "ar-EG", Display = "العربية‏ / Arabic" },
						new InterfaceLanguage { CultureCode = "ka-GE", Display = "ქართული / Georgian" },
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
			this.appTheme = this.AppThemeChoices.FirstOrDefault(t => t.Value == CustomConfig.AppTheme);
			if (this.appTheme == null)
			{
				this.appTheme = this.AppThemeChoices[0];
			}

			this.PriorityChoices = new List<ComboChoice>
				{
					new ComboChoice("High", OptionsRes.Priority_High),
					new ComboChoice("AboveNormal", OptionsRes.Priority_AboveNormal),
					new ComboChoice("Normal", OptionsRes.Priority_Normal),
					new ComboChoice("BelowNormal", OptionsRes.Priority_BelowNormal),
					new ComboChoice("Idle", OptionsRes.Priority_Idle),
				};

			this.interfaceLanguage = this.LanguageChoices.FirstOrDefault(l => l.CultureCode == Config.InterfaceLanguageCode);
			if (this.interfaceLanguage == null)
			{
				this.interfaceLanguage = this.LanguageChoices[0];
			}

			this.PlayerChoices = Players.All;
			if (this.PlayerChoices.Count > 0)
			{
				this.selectedPlayer = this.PlayerChoices[0];

				foreach (IVideoPlayer player in this.PlayerChoices)
				{
					if (player.Id == Config.PreferredPlayer)
					{
						this.selectedPlayer = player;
						break;
					}
				}
			}

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


		public override bool OnClosing()
		{
			Config.OptionsDialogLastTab = this.SelectedTabIndex;

			return base.OnClosing();
		}

		public IList<string> Tabs { get; }

		private int selectedTabIndex;
		public int SelectedTabIndex
		{
			get => this.selectedTabIndex;
			set
			{
				this.RaiseAndSetIfChanged(ref this.selectedTabIndex, value);
				this.StartBetaInfoFetchIfNeeded();
			}
		}

		public LogCoordinator LogCoordinator { get; } = StaticResolver.Resolve<LogCoordinator>();

		public List<InterfaceLanguage> LanguageChoices { get; }

		public List<ComboChoice<AppThemeChoice>> AppThemeChoices { get; }

		private ComboChoice<AppThemeChoice> appTheme;

		public ComboChoice<AppThemeChoice> AppTheme
		{
			get => this.appTheme;
			set => this.RaiseAndSetIfChanged(ref this.appTheme, value);
		}

		public List<ComboChoice> PriorityChoices { get; }

		private InterfaceLanguage interfaceLanguage;
		public InterfaceLanguage InterfaceLanguage
		{
			get => this.interfaceLanguage;
			set => this.RaiseAndSetIfChanged(ref this.interfaceLanguage, value);
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

		private DragDropOrder dragDropOrder;
		public DragDropOrder DragDropOrder
		{
			get => this.dragDropOrder;
			set => this.RaiseAndSetIfChanged(ref this.dragDropOrder, value);
		}

		private bool updatesEnabledConfig;
		public bool UpdatesEnabledConfig
		{
			get => this.updatesEnabledConfig;
			set => this.RaiseAndSetIfChanged(ref this.updatesEnabledConfig, value);
		}

		public List<ComboChoice<UpdateMode>> UpdateModeChoices { get; } = new List<ComboChoice<UpdateMode>>
		{
			new ComboChoice<UpdateMode>(UpdateMode.SilentNextLaunch, EnumsRes.UpdateMode_SilentNextLaunch),
			new ComboChoice<UpdateMode>(UpdateMode.PromptApplyImmediately, EnumsRes.UpdateMode_PromptApplyImmediately),
		};

		private UpdateMode updateMode;
		public UpdateMode UpdateMode
		{
			get => this.updateMode;
			set => this.RaiseAndSetIfChanged(ref this.updateMode, value);
		}

		private ObservableAsPropertyHelper<string> updateStatus;
		public string UpdateStatus => this.updateStatus.Value;

		private ObservableAsPropertyHelper<bool> checkingForUpdate;
		public bool CheckingForUpdate => this.checkingForUpdate.Value;

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

		private IVideoPlayer selectedPlayer;
		public IVideoPlayer SelectedPlayer
		{
			get => this.selectedPlayer;
			set => this.RaiseAndSetIfChanged(ref this.selectedPlayer, value);
		}

		private bool useCustomVideoPlayer;
		public bool UseCustomVideoPlayer
		{
			get => this.useCustomVideoPlayer;
			set => this.RaiseAndSetIfChanged(ref this.useCustomVideoPlayer, value);
		}

		private string customVideoPlayer;
		public string CustomVideoPlayer
		{
			get => this.customVideoPlayer;
			set => this.RaiseAndSetIfChanged(ref this.customVideoPlayer, value);
		}

		private bool useBuiltInPlayerForPreviews;
		public bool UseBuiltInPlayerForPreviews
		{
			get => this.useBuiltInPlayerForPreviews;
			set => this.RaiseAndSetIfChanged(ref this.useBuiltInPlayerForPreviews, value);
		}

		private bool useCustomPreviewFolder;
		public bool UseCustomPreviewFolder
		{
			get => this.useCustomPreviewFolder;
			set => this.RaiseAndSetIfChanged(ref this.useCustomPreviewFolder, value);
		}

		private string previewOutputFolder;
		public string PreviewOutputFolder
		{
			get => this.previewOutputFolder;
			set => this.RaiseAndSetIfChanged(ref this.previewOutputFolder, value);
		}

		private bool minimizeToTray;
		public bool MinimizeToTray
		{
			get => this.minimizeToTray;
			set => this.RaiseAndSetIfChanged(ref this.minimizeToTray, value);
		}

		private bool playSoundOnCompletion;
		public bool PlaySoundOnCompletion
		{
			get => this.playSoundOnCompletion;
			set => this.RaiseAndSetIfChanged(ref this.playSoundOnCompletion, value);
		}

		private bool useCustomCompletionSound;
		public bool UseCustomCompletionSound
		{
			get => this.useCustomCompletionSound;
			set => this.RaiseAndSetIfChanged(ref this.useCustomCompletionSound, value);
		}

		private string customCompletionSound;
		public string CustomCompletionSound
		{
			get => this.customCompletionSound;
			set => this.RaiseAndSetIfChanged(ref this.customCompletionSound, value);
		}

		public List<ComboChoice<int>> LogVerbosityChoices { get; } = new List<ComboChoice<int>>
		{
			new ComboChoice<int>(0, LogRes.LogVerbosity_Minimized),
			new ComboChoice<int>(1, LogRes.LogVerbosity_Standard),
			new ComboChoice<int>(2, LogRes.LogVerbosity_Extended),
		};

		private int logVerbosity;
		public int LogVerbosity
		{
			get => this.logVerbosity;
			set => this.RaiseAndSetIfChanged(ref this.logVerbosity, value);
		}

		private bool copyLogToOutputFolder;
		public bool CopyLogToOutputFolder
		{
			get => this.copyLogToOutputFolder;
			set => this.RaiseAndSetIfChanged(ref this.copyLogToOutputFolder, value);
		}

		private bool copyLogToCustomFolder;

		public bool CopyLogToCustomFolder
		{
			get => this.copyLogToCustomFolder;
			set => this.RaiseAndSetIfChanged(ref this.copyLogToCustomFolder, value);
		}

		private string logCustomFolder;

		public string LogCustomFolder
		{
			get => this.logCustomFolder;
			set => this.RaiseAndSetIfChanged(ref this.logCustomFolder, value);
		}

		private bool autoPauseLowBattery;
		public bool AutoPauseLowBattery
		{
			get => this.autoPauseLowBattery;
			set => this.RaiseAndSetIfChanged(ref this.autoPauseLowBattery, value);
		}

		private bool autoPauseLowDiskSpace;
		public bool AutoPauseLowDiskSpace
		{
			get => this.autoPauseLowDiskSpace;
			set => this.RaiseAndSetIfChanged(ref this.autoPauseLowDiskSpace, value);
		}

		private int autoPauseLowDiskSpaceGb;
		public int AutoPauseLowDiskSpaceGb
		{
			get => this.autoPauseLowDiskSpaceGb;
			set => this.RaiseAndSetIfChanged(ref this.autoPauseLowDiskSpaceGb, value);
		}

		public ObservableCollection<string> AutoPauseProcesses { get; }

		private string selectedProcess;
		public string SelectedProcess
		{
			get => this.selectedProcess;
			set => this.RaiseAndSetIfChanged(ref this.selectedProcess, value);
		}

		private int previewCount;
		public int PreviewCount
		{
			get => this.previewCount;
			set => this.RaiseAndSetIfChanged(ref this.previewCount, value);
		}

		private string workerProcessPriority;
		public string WorkerProcessPriority
		{
			get => this.workerProcessPriority;
			set => this.RaiseAndSetIfChanged(ref this.workerProcessPriority, value);
		}

		private bool rememberPreviousFiles;
		public bool RememberPreviousFiles
		{
			get => this.rememberPreviousFiles;
			set => this.RaiseAndSetIfChanged(ref this.rememberPreviousFiles, value);
		}

		private bool enableLibDvdNav;
		public bool EnableLibDvdNav
		{
			get => this.enableLibDvdNav;
			set => this.RaiseAndSetIfChanged(ref this.enableLibDvdNav, value);
		}

		private bool preserveModifyTimeFiles;
		public bool PreserveModifyTimeFiles
		{
			get => this.preserveModifyTimeFiles;
			set => this.RaiseAndSetIfChanged(ref this.preserveModifyTimeFiles, value);
		}

		private bool resumeEncodingOnRestart;
		public bool ResumeEncodingOnRestart
		{
			get => this.resumeEncodingOnRestart;
			set => this.RaiseAndSetIfChanged(ref this.resumeEncodingOnRestart, value);
		}

		private bool keepFailedFiles;

		public bool KeepFailedFiles
		{
			get => this.keepFailedFiles;
			set => this.RaiseAndSetIfChanged(ref this.keepFailedFiles, value);
		}

		private bool triggerEncodeCompleteActionWithErrors;
		public bool TriggerEncodeCompleteActionWithErrors
		{
			get => this.triggerEncodeCompleteActionWithErrors;
			set => this.RaiseAndSetIfChanged(ref this.triggerEncodeCompleteActionWithErrors, value);
		}

		private bool useWorkerProcess;
		public bool UseWorkerProcess
		{
			get => this.useWorkerProcess;
			set => this.RaiseAndSetIfChanged(ref this.useWorkerProcess, value);
		}

		private int minimumTitleLengthSeconds;
		public int MinimumTitleLengthSeconds
		{
			get => this.minimumTitleLengthSeconds;
			set => this.RaiseAndSetIfChanged(ref this.minimumTitleLengthSeconds, value);
		}

		private string videoFileExtensions;
		public string VideoFileExtensions
		{
			get => this.videoFileExtensions;
			set => this.RaiseAndSetIfChanged(ref this.videoFileExtensions, value);
		}

		private int encodeRetries;
		public int EncodeRetries
		{
			get => this.encodeRetries;
			set => this.RaiseAndSetIfChanged(ref this.encodeRetries, value);
		}

		private int cpuThrottlingCores;
		public int CpuThrottlingCores
		{
			get => this.cpuThrottlingCores;
			set => this.RaiseAndSetIfChanged(ref this.cpuThrottlingCores, value);
		}

		public int CpuThrottlingMaxCores
		{
			get
			{
				return Environment.ProcessorCount;
			}
		}

		private int maxSimultaneousEncodes;
		public int MaxSimultaneousEncodes
		{
			get => this.maxSimultaneousEncodes;
			set => this.RaiseAndSetIfChanged(ref this.maxSimultaneousEncodes, value);
		}

		private ObservableAsPropertyHelper<string> cpuThrottlingDisplay;
		public string CpuThrottlingDisplay => this.cpuThrottlingDisplay.Value;

		private ReactiveCommand<Unit, Unit> saveSettings;
		public ICommand SaveSettings
		{
			get
			{
				return this.saveSettings ?? (this.saveSettings = ReactiveCommand.Create(() =>
				{
					this.SaveSettingsImpl();
				}));
			}
		}

		private void SaveSettingsImpl()
		{
			using (SQLiteTransaction transaction = Database.Connection.BeginTransaction())
			{
				if (Config.UpdatesEnabled != this.UpdatesEnabledConfig)
				{
					Config.UpdatesEnabled = this.UpdatesEnabledConfig;
					this.Updater.HandleUpdatedSettings(this.UpdatesEnabledConfig);
				}

				CustomConfig.UpdateMode = this.UpdateMode;

				if (Config.InterfaceLanguageCode != this.InterfaceLanguage.CultureCode)
				{
					Config.InterfaceLanguageCode = this.InterfaceLanguage.CultureCode;
					StaticResolver.Resolve<IMessageBoxService>().Show(this, OptionsRes.NewLanguageRestartDialogMessage);
				}

				if (Config.UseWorkerProcess != this.UseWorkerProcess)
				{
					// Override the value that the app uses with the old choice
					CustomConfig.SetWorkerProcessOverride(Config.UseWorkerProcess);

					// Set the stored value to the new choice
					Config.UseWorkerProcess = this.UseWorkerProcess;

					StaticResolver.Resolve<IMessageBoxService>().Show(this, OptionsRes.WorkerProcessRestartDialogMessage);
				}

				CustomConfig.AppTheme = this.AppTheme.Value;
				Config.UseCustomPreviewFolder = this.UseCustomPreviewFolder;
				Config.PreviewOutputFolder = this.PreviewOutputFolder;
				Config.MinimizeToTray = this.MinimizeToTray;
				Config.UseCustomVideoPlayer = this.UseCustomVideoPlayer;
				Config.CustomVideoPlayer = this.CustomVideoPlayer;
				Config.UseBuiltInPlayerForPreviews = this.UseBuiltInPlayerForPreviews;
				Config.PlaySoundOnCompletion = this.PlaySoundOnCompletion;
				Config.UseCustomCompletionSound = this.UseCustomCompletionSound;
				Config.CustomCompletionSound = this.CustomCompletionSound;
				Config.WorkerProcessPriority = this.WorkerProcessPriority;
				CustomConfig.DragDropOrder = this.DragDropOrder;
				Config.LogVerbosity = this.LogVerbosity;
				Config.CopyLogToOutputFolder = this.CopyLogToOutputFolder;
				Config.CopyLogToCustomFolder = this.CopyLogToCustomFolder;
				Config.LogCustomFolder = this.LogCustomFolder;
				Config.AutoPauseLowBattery = this.AutoPauseLowBattery;
				Config.AutoPauseLowDiskSpace = this.AutoPauseLowDiskSpace;
				Config.AutoPauseLowDiskSpaceGb = this.AutoPauseLowDiskSpaceGb;
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
					Config.LastSubtitleFolder = null;
					Config.LastVideoTSFolder = null;

					Config.SourceHistory = null;
				}

				Config.EnableLibDvdNav = this.EnableLibDvdNav;
				Config.PreserveModifyTimeFiles = this.PreserveModifyTimeFiles;
				Config.ResumeEncodingOnRestart = this.ResumeEncodingOnRestart;
				Config.KeepFailedFiles = this.KeepFailedFiles;
				Config.TriggerEncodeCompleteActionWithErrors = this.TriggerEncodeCompleteActionWithErrors;
				Config.MinimumTitleLengthSeconds = this.MinimumTitleLengthSeconds;
				Config.VideoFileExtensions = this.VideoFileExtensions;
				Config.EncodeRetries = this.EncodeRetries;
				Config.CpuThrottlingFraction = (double)this.CpuThrottlingCores / this.CpuThrottlingMaxCores;
				Config.MaxSimultaneousEncodes = this.MaxSimultaneousEncodes;

				Config.PreferredPlayer = this.selectedPlayer.Id;

				transaction.Commit();
			}

			this.Accept.Execute(null);
		}

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
							return Config.UpdatesEnabled &&
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
						this.SaveSettingsImpl();
						UpdateManager.RestartApp();
					}));
			}
		}
	}
}
