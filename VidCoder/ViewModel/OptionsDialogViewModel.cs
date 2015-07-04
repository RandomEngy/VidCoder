using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows.Input;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;
using VidCoder.Messages;
using VidCoder.Model;
using VidCoder.Services;
using System.IO;
using System.Collections.ObjectModel;
using VidCoder.Services.Windows;

namespace VidCoder.ViewModel
{
	using System.ComponentModel;
	using System.Data.SQLite;
	using System.Globalization;
	using System.Resources;
	using Resources;
	using Utilities = VidCoder.Utilities;

	public class OptionsDialogViewModel : OkCancelDialogOldViewModel
	{
		private double updateProgress;
		private string defaultPath;
		private bool customFormat;
		private string customFormatString;
		private WhenFileExists whenFileExists;
		private WhenFileExists whenFileExistsBatch;
		private InterfaceLanguage interfaceLanguage;
		private bool minimizeToTray;
		private bool playSoundOnCompletion;
		private int logVerbosity;
		private ObservableCollection<string> autoPauseProcesses;
		private string selectedProcess;
		private int previewCount;
		private bool showAudioTrackNameField;

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
			this.updater.UpdateDownloadProgress += this.OnUpdateDownloadProgress;
			this.updater.UpdateStateChanged += this.OnUpdateStateChanged;

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
			this.playSoundOnCompletion = Config.PlaySoundOnCompletion;
			this.useCustomCompletionSound = Config.UseCustomCompletionSound;
			this.customCompletionSound = Config.CustomCompletionSound;
			this.workerProcessPriority = Config.WorkerProcessPriority;
			this.logVerbosity = Config.LogVerbosity;
			this.copyLogToOutputFolder = Config.CopyLogToOutputFolder;
			this.previewCount = Config.PreviewCount;
			this.rememberPreviousFiles = Config.RememberPreviousFiles;
			this.showAudioTrackNameField = Config.ShowAudioTrackNameField;
			this.dxvaDecoding = Config.DxvaDecoding;
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
						new InterfaceLanguage { CultureCode = "cs-CZ", Display = "čeština / Czech" },
						new InterfaceLanguage { CultureCode = "de-DE", Display = "Deutsch / German" },
						new InterfaceLanguage { CultureCode = "es-ES", Display = "Español / Spanish" },
						new InterfaceLanguage { CultureCode = "eu-ES", Display = "Euskara / Basque" },
						new InterfaceLanguage { CultureCode = "fr-FR", Display = "Français / French" },
						new InterfaceLanguage { CultureCode = "it-IT", Display = "italiano / Italian" },
						new InterfaceLanguage { CultureCode = "hu-HU", Display = "Magyar / Hungarian" },
						new InterfaceLanguage { CultureCode = "pl-PL", Display = "polski / Polish" },
						new InterfaceLanguage { CultureCode = "pt-PT", Display = "Português / Portuguese" },
						new InterfaceLanguage { CultureCode = "pt-BR", Display = "Português (Brasil) / Portuguese (Brazil)" },
						new InterfaceLanguage { CultureCode = "ru-RU", Display = "русский / Russian" },
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

#if !BETA
			var betaInfoWorker = new BackgroundWorker();
			betaInfoWorker.DoWork += (o, e) =>
				{
					this.betaInfo = Updater.GetUpdateInfo(true);
				};
			betaInfoWorker.RunWorkerCompleted += (o, e) =>
				{
					this.betaInfoAvailable = false;
					if (this.betaInfo != null)
					{
						if (Utilities.CompareVersions(this.betaInfo.LatestVersion, Utilities.CurrentVersion) > 0)
						{
							this.betaInfoAvailable = true;
						}
					}

					this.RaisePropertyChanged(() => this.BetaChangelogUrl);
					this.RaisePropertyChanged(() => this.BetaSectionVisible);
				};
			betaInfoWorker.RunWorkerAsync();
#endif

		    int tabIndex = Config.OptionsDialogLastTab;
		    if (tabIndex >= this.Tabs.Count)
		    {
		        tabIndex = 0;
		    }

            this.SelectedTabIndex = tabIndex;

			this.RefreshUpdateStatus();
		}

		public override void OnClosing()
		{
			this.updater.UpdateDownloadProgress -= this.OnUpdateDownloadProgress;
			this.updater.UpdateStateChanged -= this.OnUpdateStateChanged;

			Config.OptionsDialogLastTab = this.SelectedTabIndex;

			base.OnClosing();
		}

		public IList<string> Tabs
		{
			get
			{
				return new List<string>
					{
						OptionsRes.GeneralTab,			// 0
						OptionsRes.FileNamingTab,		// 1
						OptionsRes.ProcessTab,			// 2
						OptionsRes.AdvancedTab,			// 3
						OptionsRes.UpdatesTab			// 4
					};
			}
		} 

		private int selectedTabIndex;
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

		public InterfaceLanguage InterfaceLanguage
		{
			get
			{
				return this.interfaceLanguage;
			}

			set
			{
				this.interfaceLanguage = value;
				this.RaisePropertyChanged(() => this.InterfaceLanguage);
			}
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
			get
			{
				return this.updatesEnabledConfig;
			}

			set
			{
				this.updatesEnabledConfig = value;
				this.RaisePropertyChanged(() => this.UpdatesEnabledConfig);
				this.RaisePropertyChanged(() => this.UpdatesEnabled);
			}
		}

		public bool UpdatesEnabled
		{
			get
			{
				if (Utilities.IsPortable)
				{
					return false;
				}

				return this.updatesEnabledConfig;
			}
		}

		public bool BuildSupportsUpdates
		{
			get
			{
				return !Utilities.IsPortable;
			}
		}

		public string UpdateStatus
		{
			get
			{
				switch (this.updater.State)
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
					default:
						throw new ArgumentOutOfRangeException();
				}
			}
		}

		public bool UpdateDownloading
		{
			get
			{
				return this.updater.State == UpdateState.DownloadingInstaller;
			}
		}

		public double UpdateProgress
		{
			get
			{
				return this.updateProgress;
			}

			set
			{
				this.updateProgress = value;
				this.RaisePropertyChanged(() => this.UpdateProgress);
			}
		}

		public string BetaUpdatesText
		{
			get
			{
#if BETA
				return OptionsRes.BetaUpdatesInBeta;
#else
				return OptionsRes.BetaUpdatesNonBeta;
#endif
			}
		}

		public string BetaChangelogUrl
		{
			get
			{
#if BETA
				return string.Empty;
#else
				return this.betaInfo.ChangelogLocation;
#endif
			}
		}

		public bool BetaSectionVisible
		{
			get
			{
#if BETA
				return true;
#else
				return this.betaInfoAvailable;
#endif
			}
		}

		public bool InBeta
		{
			get
			{
#if BETA
				return true;
#else
				return false;
#endif
			}
		}

		public List<IVideoPlayer> PlayerChoices
		{
			get
			{
				return this.playerChoices;
			}
		}

		private IVideoPlayer selectedPlayer;
		public IVideoPlayer SelectedPlayer
		{
			get
			{
				return this.selectedPlayer;
			}

			set
			{
				this.selectedPlayer = value;
				this.RaisePropertyChanged(() => this.SelectedPlayer);
			}
		}

		private bool useCustomVideoPlayer;
		public bool UseCustomVideoPlayer
		{
			get
			{
				return this.useCustomVideoPlayer;
			}

			set
			{
				this.useCustomVideoPlayer = value;
				this.RaisePropertyChanged(() => this.UseCustomVideoPlayer);
				this.BrowseVideoPlayerCommand.RaiseCanExecuteChanged();
			}
		}

		private string customVideoPlayer;
		public string CustomVideoPlayer
		{
			get
			{
				return this.customVideoPlayer;
			}

			set
			{
				this.customVideoPlayer = value;
				this.RaisePropertyChanged(() => this.CustomVideoPlayer);
			}
		}

		public string DefaultPath
		{
			get
			{
				return this.defaultPath;
			}

			set
			{
				this.defaultPath = value;
				this.RaisePropertyChanged(() => this.DefaultPath);
			}
		}

		public bool CustomFormat
		{
			get
			{
				return this.customFormat;
			}

			set
			{
				this.customFormat = value;
				this.RaisePropertyChanged(() => this.CustomFormat);
				this.RaisePropertyChanged(() => this.CustomFormatStringEnabled);
			}
		}

		public string CustomFormatString
		{
			get
			{
				return this.customFormatString;
			}

			set
			{
				this.customFormatString = value;
				this.RaisePropertyChanged(() => this.CustomFormatString);
			}
		}

		public bool CustomFormatStringEnabled
		{
			get
			{
				return this.CustomFormat;
			}
		}

		public string AvailableOptionsText
		{
			get
			{
				var manager = new ResourceManager(typeof (OptionsRes));
				return string.Format(manager.GetString("FileNameFormatOptions"), "{source} {title} {range} {preset} {date} {time} {quality} {parent} {titleduration}");
			}
		}

		private bool outputToSourceDirectory;
		public bool OutputToSourceDirectory
		{
			get
			{
				return this.outputToSourceDirectory;
			}

			set
			{
				this.outputToSourceDirectory = value;
				this.RaisePropertyChanged(() => this.OutputToSourceDirectory);
			}
		}

		private bool preserveFolderStructureInBatch;
		public bool PreserveFolderStructureInBatch
		{
			get
			{
				return this.preserveFolderStructureInBatch;
			}

			set
			{
				this.preserveFolderStructureInBatch = value;
				this.RaisePropertyChanged(() => this.PreserveFolderStructureInBatch);
			}
		}

		private bool useCustomPreviewFolder;
		public bool UseCustomPreviewFolder
		{
			get
			{
				return this.useCustomPreviewFolder;
			}

			set
			{
				this.useCustomPreviewFolder = value;
				this.RaisePropertyChanged(() => this.UseCustomPreviewFolder);
			}
		}

		private string previewOutputFolder;
		public string PreviewOutputFolder
		{
			get
			{
				return this.previewOutputFolder;
			}

			set
			{
				this.previewOutputFolder = value;
				this.RaisePropertyChanged(() => this.PreviewOutputFolder);
			}
		}

		public WhenFileExists WhenFileExists
		{
			get
			{
				return this.whenFileExists;
			}

			set
			{
				this.whenFileExists = value;
				this.RaisePropertyChanged(() => this.WhenFileExists);
			}
		}

		public WhenFileExists WhenFileExistsBatch
		{
			get
			{
				return this.whenFileExistsBatch;
			}

			set
			{
				this.whenFileExistsBatch = value;
				this.RaisePropertyChanged(() => this.WhenFileExistsBatch);
			}
		}

		public bool MinimizeToTray
		{
			get
			{
				return this.minimizeToTray;
			}

			set
			{
				this.minimizeToTray = value;
				this.RaisePropertyChanged(() => this.MinimizeToTray);
			}
		}

		public bool PlaySoundOnCompletion
		{
			get
			{
				return this.playSoundOnCompletion;
			}

			set
			{
				this.playSoundOnCompletion = value;
				this.RaisePropertyChanged(() => this.PlaySoundOnCompletion);
			}
		}

		private bool useCustomCompletionSound;
		public bool UseCustomCompletionSound
		{
			get
			{
				return this.useCustomCompletionSound;
			}

			set
			{
				this.useCustomCompletionSound = value;
				this.RaisePropertyChanged(() => this.UseCustomCompletionSound);
				this.BrowseCompletionSoundCommand.RaiseCanExecuteChanged();
			}
		}

		private string customCompletionSound;
		public string CustomCompletionSound
		{
			get
			{
				return this.customCompletionSound;
			}

			set
			{
				this.customCompletionSound = value;
				this.RaisePropertyChanged(() => this.CustomCompletionSound);
			}
		}

		public int LogVerbosity
		{
			get
			{
				return this.logVerbosity;
			}

			set
			{
				this.logVerbosity = value;
				this.RaisePropertyChanged(() => this.LogVerbosity);
			}
		}

		private bool copyLogToOutputFolder;
		public bool CopyLogToOutputFolder
		{
			get
			{
				return this.copyLogToOutputFolder;
			}

			set
			{
				this.copyLogToOutputFolder = value;
				this.RaisePropertyChanged(() => this.CopyLogToOutputFolder);
			}
		}

		public ObservableCollection<string> AutoPauseProcesses
		{
			get
			{
				return this.autoPauseProcesses;
			}
		}

		public string SelectedProcess
		{
			get
			{
				return this.selectedProcess;
			}

			set
			{
				this.selectedProcess = value;
				this.RemoveProcessCommand.RaiseCanExecuteChanged();
				this.RaisePropertyChanged(() => this.SelectedProcess);
			}
		}

		public int PreviewCount
		{
			get
			{
				return this.previewCount;
			}

			set
			{
				this.previewCount = value;
				this.RaisePropertyChanged(() => this.PreviewCount);
			}
		}

		private string workerProcessPriority;
		public string WorkerProcessPriority
		{
			get
			{
				return this.workerProcessPriority;
			}

			set
			{
				this.workerProcessPriority = value;
				this.RaisePropertyChanged(() => this.WorkerProcessPriority);
			}
		}

		private bool rememberPreviousFiles;
		public bool RememberPreviousFiles
		{
			get
			{
				return this.rememberPreviousFiles;
			}

			set
			{
				this.rememberPreviousFiles = value;
				this.RaisePropertyChanged(() => this.RememberPreviousFiles);
			}
		}

		public bool ShowAudioTrackNameField
		{
			get
			{
				return this.showAudioTrackNameField;
			}

			set
			{
				this.showAudioTrackNameField = value;
				this.RaisePropertyChanged(() => this.ShowAudioTrackNameField);
			}
		}

		private bool dxvaDecoding;
		public bool DxvaDecoding
		{
			get
			{
				return this.dxvaDecoding;
			}

			set
			{
				this.dxvaDecoding = value;
				this.RaisePropertyChanged(() => this.DxvaDecoding);
			}
		}

		private bool enableLibDvdNav;
		public bool EnableLibDvdNav
		{
			get
			{
				return this.enableLibDvdNav;
			}

			set
			{
				this.enableLibDvdNav = value;
				this.RaisePropertyChanged(() => this.EnableLibDvdNav);
			}
		}

		private bool deleteSourceFilesOnClearingCompleted;
		public bool DeleteSourceFilesOnClearingCompleted
		{
			get
			{
				return this.deleteSourceFilesOnClearingCompleted;
			}

			set
			{
				this.deleteSourceFilesOnClearingCompleted = value;
				this.RaisePropertyChanged(() => this.DeleteSourceFilesOnClearingCompleted);
			}
		}

		private bool preserveModifyTimeFiles;
		public bool PreserveModifyTimeFiles
		{
			get
			{
				return this.preserveModifyTimeFiles;
			}

			set
			{
				this.preserveModifyTimeFiles = value;
				this.RaisePropertyChanged(() => this.PreserveModifyTimeFiles);
			}
		}

		private bool resumeEncodingOnRestart;
		public bool ResumeEncodingOnRestart
		{
			get
			{
				return this.resumeEncodingOnRestart;
			}

			set
			{
				this.resumeEncodingOnRestart = value;
				this.RaisePropertyChanged(() => this.ResumeEncodingOnRestart);
			}
		}

		private bool useWorkerProcess;
		public bool UseWorkerProcess
		{
			get
			{
				return this.useWorkerProcess;
			}

			set
			{
				this.useWorkerProcess = value;
				this.RaisePropertyChanged(() => this.UseWorkerProcess);
			}
		}

		private int minimumTitleLengthSeconds;
		public int MinimumTitleLengthSeconds
		{
			get
			{
				return this.minimumTitleLengthSeconds;
			}

			set
			{
				this.minimumTitleLengthSeconds = value;
				this.RaisePropertyChanged(() => this.MinimumTitleLengthSeconds);
			}
		}

		private string videoFileExtensions;
		public string VideoFileExtensions
		{
			get
			{
				return this.videoFileExtensions;
			}

			set
			{
				this.videoFileExtensions = value;
				this.RaisePropertyChanged(() => this.VideoFileExtensions);
			}
		}

		private int cpuThrottlingCores;
		public int CpuThrottlingCores
		{
			get
			{
				return this.cpuThrottlingCores;
			}

			set
			{
				this.cpuThrottlingCores = value;
				this.RaisePropertyChanged(() => this.CpuThrottlingCores);
				this.RaisePropertyChanged(() => this.CpuThrottlingDisplay);
			}
		}

		public int CpuThrottlingMaxCores
		{
			get
			{
				return Environment.ProcessorCount;
			}
		}

		public string CpuThrottlingDisplay
		{
			get
			{
				double currentFraction = (double)this.CpuThrottlingCores / this.CpuThrottlingMaxCores;
				return currentFraction.ToString("p0");
			}
		}

		private RelayCommand saveSettingsCommand;
		public RelayCommand SaveSettingsCommand
		{
			get
			{
				return this.saveSettingsCommand ?? (this.saveSettingsCommand = new RelayCommand(() =>
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
							Config.DxvaDecoding = this.DxvaDecoding;
							Config.DeleteSourceFilesOnClearingCompleted = this.DeleteSourceFilesOnClearingCompleted;
							Config.PreserveModifyTimeFiles = this.PreserveModifyTimeFiles;
							Config.ResumeEncodingOnRestart = this.ResumeEncodingOnRestart;
							Config.UseWorkerProcess = this.UseWorkerProcess;
							Config.MinimumTitleLengthSeconds = this.MinimumTitleLengthSeconds;
							Config.VideoFileExtensions = this.VideoFileExtensions;
							Config.CpuThrottlingFraction = (double) this.CpuThrottlingCores / this.CpuThrottlingMaxCores;

							Config.PreferredPlayer = this.selectedPlayer.Id;

							transaction.Commit();
						}

						Messenger.Default.Send(new OptionsChangedMessage());

						this.AcceptCommand.Execute(null);
					}));
			}
		}

		private RelayCommand browseVideoPlayerCommand;
		public RelayCommand BrowseVideoPlayerCommand
		{
			get
			{
				return this.browseVideoPlayerCommand ?? (this.browseVideoPlayerCommand = new RelayCommand(() =>
					{
						string fileName = FileService.Instance.GetFileNameLoad(
							title: OptionsRes.VideoPlayerFilePickTitle,
							filter: Utilities.GetFilePickerFilter("exe"));

						if (fileName != null)
						{
							this.CustomVideoPlayer = fileName;
						}
					},
					() =>
					{
						return this.UseCustomVideoPlayer;
					}));
			}
		}

		private RelayCommand browseCompletionSoundCommand;
		public RelayCommand BrowseCompletionSoundCommand
		{
			get
			{
				return this.browseCompletionSoundCommand ?? (this.browseCompletionSoundCommand = new RelayCommand(() =>
					{
						string fileName = FileService.Instance.GetFileNameLoad(
							title: OptionsRes.CompletionWavFilePickTitle, 
							filter: Utilities.GetFilePickerFilter("wav"));

						if (fileName != null)
						{
							this.CustomCompletionSound = fileName;
						}
					},
					() =>
					{
						return this.UseCustomCompletionSound;
					}));
			}
		}

		private RelayCommand browsePathCommand;
		public RelayCommand BrowsePathCommand
		{
			get
			{
				return this.browsePathCommand ?? (this.browsePathCommand = new RelayCommand(() =>
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
					}));
			}
		}

		private RelayCommand browsePreviewFolderCommand;
		public RelayCommand BrowsePreviewFolderCommand
		{
			get
			{
				return this.browsePreviewFolderCommand ?? (this.browsePreviewFolderCommand = new RelayCommand(() =>
				{
					string newFolder = FileService.Instance.GetFolderName(null);
					if (newFolder != null)
					{
						this.PreviewOutputFolder = newFolder;
					}
				}));
			}
		}

		private RelayCommand openAddProcessDialogCommand;
		public RelayCommand OpenAddProcessDialogCommand
		{
			get
			{
				return this.openAddProcessDialogCommand ?? (this.openAddProcessDialogCommand = new RelayCommand(() =>
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
					}));
			}
		}

		private RelayCommand removeProcessCommand;
		public RelayCommand RemoveProcessCommand
		{
			get
			{
				return this.removeProcessCommand ?? (this.removeProcessCommand = new RelayCommand(() =>
					{
						this.AutoPauseProcesses.Remove(this.SelectedProcess);
					}, () =>
					{
						return this.SelectedProcess != null;
					}));
			}
		}

		private RelayCommand openLogFolderCommand;
		public RelayCommand OpenLogFolderCommand
		{
			get
			{
				return this.openLogFolderCommand ?? (this.openLogFolderCommand = new RelayCommand(() =>
					{
						string logFolder = Utilities.LogsFolder;

						if (Directory.Exists(logFolder))
						{
							FileService.Instance.LaunchFile(logFolder);
						}
					},
					() =>
					{
						return Directory.Exists(Utilities.LogsFolder);
					}));
			}
		}

		private RelayCommand checkUpdateCommand;
		public RelayCommand CheckUpdateCommand
		{
			get
			{
				return this.checkUpdateCommand ?? (this.checkUpdateCommand = new RelayCommand(() =>
					{
						this.updater.CheckUpdates();
					},
					() =>
					{
						return Config.UpdatesEnabled &&
							(this.updater.State == UpdateState.Failed || 
							this.updater.State == UpdateState.NotStarted || 
							this.updater.State == UpdateState.UpToDate);
					}));
			}
		}

		private void RefreshUpdateStatus()
		{
			this.RaisePropertyChanged(() => this.UpdateDownloading);
			this.RaisePropertyChanged(() => this.UpdateStatus);
			this.CheckUpdateCommand.RaiseCanExecuteChanged();
		}

		private void OnUpdateStateChanged(object sender, EventArgs e)
		{
			DispatchUtilities.BeginInvoke(() =>
				{
					this.RefreshUpdateStatus();
				});
		}

		private void OnUpdateDownloadProgress(object sender, EventArgs<double> e)
		{
			this.UpdateProgress = e.Value;
		}
	}
}
