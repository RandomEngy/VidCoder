using System;
using System.Collections.Generic;
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

namespace VidCoder.ViewModel
{
	using System.Data.SQLite;
	using System.Resources;
	using LocalResources;
	using Microsoft.Practices.Unity;

	public class OptionsDialogViewModel : OkCancelDialogViewModel
	{
		private bool updatesEnabled;
		private bool updateDownloading;
		private double updateProgress;
		private string defaultPath;
		private bool customFormat;
		private string customFormatString;
		private bool outputToSourceDirectory;
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

		private IUpdater updateService;

		public OptionsDialogViewModel(IUpdater updateService)
		{
			this.updateService = updateService;
			this.updateDownloading = updateService.UpdateDownloading;
			this.updateService.UpdateDownloadProgress += this.OnUpdateDownloadProgress;
			this.updateService.UpdateDownloadCompleted += this.OnUpdateDownloadCompleted;

			this.updatesEnabled = Config.UpdatesEnabled;
			this.betaUpdates = Config.BetaUpdates;
			this.defaultPath = Config.AutoNameOutputFolder;
			this.customFormat = Config.AutoNameCustomFormat;
			this.customFormatString = Config.AutoNameCustomFormatString;
			this.outputToSourceDirectory = Config.OutputToSourceDirectory;
			this.whenFileExists = CustomConfig.WhenFileExists;
			this.whenFileExistsBatch = CustomConfig.WhenFileExistsBatch;
			this.minimizeToTray = Config.MinimizeToTray;
			this.playSoundOnCompletion = Config.PlaySoundOnCompletion;
			this.useCustomCompletionSound = Config.UseCustomCompletionSound;
			this.customCompletionSound = Config.CustomCompletionSound;
			this.autoAudio = CustomConfig.AutoAudio;
			this.audioLanguageCode = Config.AudioLanguageCode;
			this.autoAudioAll = Config.AutoAudioAll;
			this.autoSubtitle = CustomConfig.AutoSubtitle;
			this.autoSubtitleBurnIn = Config.AutoSubtitleBurnIn;
			this.subtitleLanguageCode = Config.SubtitleLanguageCode;
			this.autoSubtitleOnlyIfDifferent = Config.AutoSubtitleOnlyIfDifferent;
			this.autoSubtitleAll = Config.AutoSubtitleAll;
			this.logVerbosity = Config.LogVerbosity;
			this.previewCount = Config.PreviewCount;
			this.rememberPreviousFiles = Config.RememberPreviousFiles;
			this.showAudioTrackNameField = Config.ShowAudioTrackNameField;
			this.keepScansAfterCompletion = Config.KeepScansAfterCompletion;
			this.enableLibDvdNav = Config.EnableLibDvdNav;
			this.deleteSourceFilesOnClearingCompleted = Config.DeleteSourceFilesOnClearingCompleted;
			this.minimumTitleLengthSeconds = Config.MinimumTitleLengthSeconds;
			this.autoPauseProcesses = new ObservableCollection<string>();
			this.videoFileExtensions = Config.VideoFileExtensions;
			List<string> autoPauseList = CustomConfig.AutoPauseProcesses;
			if (autoPauseList != null)
			{
				foreach (string process in autoPauseList)
				{
					this.autoPauseProcesses.Add(process);
				}
			}

			this.languageChoices =
				new List<InterfaceLanguage>
					{
						new InterfaceLanguage { CultureCode = string.Empty, Display = OptionsRes.UseOSLanguage },
						new InterfaceLanguage { CultureCode = "en-US", Display = "English" },
						new InterfaceLanguage { CultureCode = "hu-HU", Display = "Magyar"},
						//new InterfaceLanguage { CultureCode = "de-DE", Display = "Deutsch" },
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
		}

		public override void OnClosing()
		{
			this.updateService.UpdateDownloadProgress -= this.OnUpdateDownloadProgress;
			this.updateService.UpdateDownloadCompleted -= this.OnUpdateDownloadCompleted;

			base.OnClosing();
		}

		public List<InterfaceLanguage> LanguageChoices
		{
			get
			{
				return this.languageChoices;
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

		public bool UpdatesEnabled
		{
			get
			{
				return this.updatesEnabled;
			}

			set
			{
				this.updatesEnabled = value;
				this.RaisePropertyChanged(() => this.UpdatesEnabled);
				this.RaisePropertyChanged(() => this.BetaUpdates);
				this.RaisePropertyChanged(() => this.BetaUpdatesCheckBoxEnabled);
			}
		}

		private bool betaUpdates;
		public bool BetaUpdates
		{
			get
			{
#pragma warning disable 162
#if BETA
				return true;
#endif

				if (!this.UpdatesEnabled)
				{
					return false;
				}

				return this.betaUpdates;
#pragma warning restore 162
			}

			set
			{
				this.betaUpdates = value;
				this.RaisePropertyChanged(() => this.BetaUpdates);
			}
		}

		public bool BetaUpdatesCheckBoxEnabled
		{
			get
			{
#pragma warning disable 162
#if BETA
				return false;
#endif

				return this.UpdatesEnabled;
#pragma warning restore 162
			}
		}

		public string BetaUpdatesToolTip
		{
			get
			{
#if BETA
				return OptionsRes.BetaUpdatesInBetaToolTip;
#else
				return OptionsRes.BetaUpdatesNonBetaToolTip;
#endif
			}
		}

		public string UpdateStatus
		{
			get
			{
				if (this.updateService.UpdateReady)
				{
					return OptionsRes.UpdateReadyStatus;
				}

				if (this.UpdateDownloading)
				{
					return OptionsRes.DownloadingStatus;
				}

				if (this.updateService.UpToDate)
				{
					return OptionsRes.UpToDateStatus;
				}

				return string.Empty;
			}
		}

		public bool UpdateDownloading
		{
			get
			{
				return this.updateDownloading;
			}

			set
			{
				this.updateDownloading = value;
				this.RaisePropertyChanged(() => this.UpdateDownloading);
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

		private AutoAudioType autoAudio;
		public AutoAudioType AutoAudio
		{
			get
			{
				return this.autoAudio;
			}

			set
			{
				this.autoAudio = value;
				this.RaisePropertyChanged(() => this.AutoAudio);
				this.RaisePropertyChanged(() => this.AutoAudioLanguageSelected);
			}
		}

		public bool AutoAudioLanguageSelected
		{
			get
			{
				return this.AutoAudio == AutoAudioType.Language;
			}
		}

		private string audioLanguageCode;
		public string AudioLanguageCode
		{
			get
			{
				return this.audioLanguageCode;
			}

			set
			{
				this.audioLanguageCode = value;
				this.RaisePropertyChanged(() => this.AudioLanguageCode);
			}
		}

		private bool autoAudioAll;
		public bool AutoAudioAll
		{
			get
			{
				return this.autoAudioAll;
			}

			set
			{
				this.autoAudioAll = value;
				this.RaisePropertyChanged(() => this.AutoAudioAll);
			}
		}

		private AutoSubtitleType autoSubtitle;
		public AutoSubtitleType AutoSubtitle
		{
			get
			{
				return this.autoSubtitle;
			}

			set
			{
				this.autoSubtitle = value;
				this.RaisePropertyChanged(() => this.AutoSubtitle);
				this.RaisePropertyChanged(() => this.AutoSubtitleFasSelected);
				this.RaisePropertyChanged(() => this.AutoSubtitleLanguageSelected);
			}
		}

		public bool AutoSubtitleFasSelected
		{
			get
			{
				return this.AutoSubtitle == AutoSubtitleType.ForeignAudioSearch;
			}
		}

		public bool AutoSubtitleLanguageSelected
		{
			get
			{
				return this.AutoSubtitle == AutoSubtitleType.Language;
			}
		}

		private bool autoSubtitleBurnIn;
		public bool AutoSubtitleBurnIn
		{
			get
			{
				return this.autoSubtitleBurnIn;
			}

			set
			{
				this.autoSubtitleBurnIn = value;
				this.RaisePropertyChanged(() => this.AutoSubtitleBurnIn);
			}
		}

		private string subtitleLanguageCode;
		public string SubtitleLanguageCode
		{
			get
			{
				return this.subtitleLanguageCode;
			}

			set
			{
				this.subtitleLanguageCode = value;
				this.RaisePropertyChanged(() => this.SubtitleLanguageCode);
			}
		}

		private bool autoSubtitleOnlyIfDifferent;
		public bool AutoSubtitleOnlyIfDifferent
		{
			get
			{
				return this.autoSubtitleOnlyIfDifferent;
			}

			set
			{
				this.autoSubtitleOnlyIfDifferent = value;
				this.RaisePropertyChanged(() => this.AutoSubtitleOnlyIfDifferent);
			}
		}

		private bool autoSubtitleAll;
		public bool AutoSubtitleAll
		{
			get
			{
				return this.autoSubtitleAll;
			}

			set
			{
				this.autoSubtitleAll = value;
				this.RaisePropertyChanged(() => this.AutoSubtitleAll);
			}
		}

		public IList<Language> Languages
		{
			get
			{
				return Language.Languages;
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

		private bool keepScansAfterCompletion;
		public bool KeepScansAfterCompletion
		{
			get
			{
				return this.keepScansAfterCompletion;
			}

			set
			{
				this.keepScansAfterCompletion = value;
				this.RaisePropertyChanged(() => this.KeepScansAfterCompletion);
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

		private RelayCommand saveSettingsCommand;
		public RelayCommand SaveSettingsCommand
		{
			get
			{
				return this.saveSettingsCommand ?? (this.saveSettingsCommand = new RelayCommand(() =>
					{
						using (SQLiteTransaction transaction = Database.Connection.BeginTransaction())
						{
							if (Config.UpdatesEnabled != this.UpdatesEnabled || Config.BetaUpdates != this.BetaUpdates)
							{
								Config.UpdatesEnabled = this.UpdatesEnabled;
								Config.BetaUpdates = this.BetaUpdates;
								this.updateService.HandleUpdatedSettings(this.UpdatesEnabled);
							}

							if (Config.InterfaceLanguageCode != this.InterfaceLanguage.CultureCode)
							{
								Config.InterfaceLanguageCode = this.InterfaceLanguage.CultureCode;
								Unity.Container.Resolve<IMessageBoxService>().Show(this, OptionsRes.NewLanguageRestartDialogMessage);
							}

							Config.AutoNameOutputFolder = this.DefaultPath;
							Config.AutoNameCustomFormat = this.CustomFormat;
							Config.AutoNameCustomFormatString = this.CustomFormatString;
							Config.OutputToSourceDirectory = this.OutputToSourceDirectory;
							CustomConfig.WhenFileExists = this.WhenFileExists;
							CustomConfig.WhenFileExistsBatch = this.WhenFileExistsBatch;
							Config.MinimizeToTray = this.MinimizeToTray;
							//Config.MinimizeToTray = this.MinimizeToTray;
							Config.PlaySoundOnCompletion = this.PlaySoundOnCompletion;
							Config.UseCustomCompletionSound = this.UseCustomCompletionSound;
							Config.CustomCompletionSound = this.CustomCompletionSound;
							CustomConfig.AutoAudio = this.AutoAudio;
							Config.AudioLanguageCode = this.AudioLanguageCode;
							Config.AutoAudioAll = this.AutoAudioAll;
							CustomConfig.AutoSubtitle = this.AutoSubtitle;
							Config.AutoSubtitleBurnIn = this.AutoSubtitleBurnIn;
							Config.SubtitleLanguageCode = this.SubtitleLanguageCode;
							Config.AutoSubtitleOnlyIfDifferent = this.AutoSubtitleOnlyIfDifferent;
							Config.AutoSubtitleAll = this.AutoSubtitleAll;
							Config.LogVerbosity = this.LogVerbosity;
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
							Config.KeepScansAfterCompletion = this.KeepScansAfterCompletion;
							Config.DeleteSourceFilesOnClearingCompleted = this.DeleteSourceFilesOnClearingCompleted;
							Config.MinimumTitleLengthSeconds = this.MinimumTitleLengthSeconds;
							Config.VideoFileExtensions = this.VideoFileExtensions;

							Config.PreferredPlayer = this.selectedPlayer.Id;

							transaction.Commit();
						}

						Messenger.Default.Send(new OptionsChangedMessage());

						this.AcceptCommand.Execute(null);
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
							title: "Pick the .wav file you want to use for the completion sound.", 
							defaultExt: "wav", 
							filter: "WAV Files|*.wav");

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

		private RelayCommand openAddProcessDialogCommand;
		public RelayCommand OpenAddProcessDialogCommand
		{
			get
			{
				return this.openAddProcessDialogCommand ?? (this.openAddProcessDialogCommand = new RelayCommand(() =>
					{
						var addProcessVM = new AddAutoPauseProcessDialogViewModel();
						WindowManager.OpenDialog(addProcessVM, this);

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

		private void OnUpdateDownloadCompleted(object sender, EventArgs e)
		{
			this.UpdateDownloading = false;
			this.RaisePropertyChanged(() => this.UpdateStatus);
		}

		private void OnUpdateDownloadProgress(object sender, EventArgs<double> e)
		{
			this.UpdateProgress = e.Value;
		}
	}
}
