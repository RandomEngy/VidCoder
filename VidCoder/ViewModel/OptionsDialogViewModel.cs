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
using VidCoder.Properties;
using System.Collections.Specialized;
using System.Collections.ObjectModel;

namespace VidCoder.ViewModel
{
	using System.Resources;
	using LocalResources;

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
		private bool minimizeToTray;
		private bool playSoundOnCompletion;
		private int logVerbosity;
		private ObservableCollection<string> autoPauseProcesses;
		private string selectedProcess;
		private int previewCount;
		private bool showAudioTrackNameField;

		private List<IVideoPlayer> playerChoices;

		private IUpdater updateService;

		public OptionsDialogViewModel(IUpdater updateService)
		{
			this.updateService = updateService;
			this.updateDownloading = updateService.UpdateDownloading;
			this.updateService.UpdateDownloadProgress += this.OnUpdateDownloadProgress;
			this.updateService.UpdateDownloadCompleted += this.OnUpdateDownloadCompleted;

			this.updatesEnabled = Settings.Default.UpdatesEnabled;
			this.betaUpdates = Settings.Default.BetaUpdates;
			this.defaultPath = Settings.Default.AutoNameOutputFolder;
			this.customFormat = Settings.Default.AutoNameCustomFormat;
			this.customFormatString = Settings.Default.AutoNameCustomFormatString;
			this.outputToSourceDirectory = Settings.Default.OutputToSourceDirectory;
			this.whenFileExists = Settings.Default.WhenFileExists;
			this.whenFileExistsBatch = Settings.Default.WhenFileExistsBatch;
			this.minimizeToTray = Settings.Default.MinimizeToTray;
			this.playSoundOnCompletion = Settings.Default.PlaySoundOnCompletion;
			this.autoAudio = Settings.Default.AutoAudio;
			this.audioLanguageCode = Settings.Default.AudioLanguageCode;
			this.autoAudioAll = Settings.Default.AutoAudioAll;
			this.autoSubtitle = Settings.Default.AutoSubtitle;
			this.autoSubtitleBurnIn = Settings.Default.AutoSubtitleBurnIn;
			this.subtitleLanguageCode = Settings.Default.SubtitleLanguageCode;
			this.autoSubtitleOnlyIfDifferent = Settings.Default.AutoSubtitleOnlyIfDifferent;
			this.autoSubtitleAll = Settings.Default.AutoSubtitleAll;
			this.logVerbosity = Settings.Default.LogVerbosity;
			this.previewCount = Settings.Default.PreviewCount;
			this.rememberPreviousFiles = Settings.Default.RememberPreviousFiles;
			this.showAudioTrackNameField = Settings.Default.ShowAudioTrackNameField;
			this.keepScansAfterCompletion = Settings.Default.KeepScansAfterCompletion;
			this.enableLibDvdNav = Settings.Default.EnableLibDvdNav;
			this.deleteSourceFilesOnClearingCompleted = Settings.Default.DeleteSourceFilesOnClearingCompleted;
			this.minimumTitleLengthSeconds = Settings.Default.MinimumTitleLengthSeconds;
			this.autoPauseProcesses = new ObservableCollection<string>();
			this.videoFileExtensions = Settings.Default.VideoFileExtensions;
			StringCollection autoPauseStringCollection = Settings.Default.AutoPauseProcesses;
			if (autoPauseStringCollection != null)
			{
				foreach (string process in autoPauseStringCollection)
				{
					this.autoPauseProcesses.Add(process);
				}
			}

			this.playerChoices = Players.All;
			if (this.playerChoices.Count > 0)
			{
				this.selectedPlayer = this.playerChoices[0];

				foreach (IVideoPlayer player in this.playerChoices)
				{
					if (player.Id == Settings.Default.PreferredPlayer)
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
#if BETA
				return true;
#endif

				if (!this.UpdatesEnabled)
				{
					return false;
				}

				return this.betaUpdates;
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
#if BETA
				return false;
#endif

				return this.UpdatesEnabled;
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
						if (Settings.Default.UpdatesEnabled != this.UpdatesEnabled || Settings.Default.BetaUpdates != this.BetaUpdates)
						{
							Settings.Default.UpdatesEnabled = this.UpdatesEnabled;
							Settings.Default.BetaUpdates = this.BetaUpdates;
							this.updateService.HandleUpdatedSettings(this.UpdatesEnabled);
						}

						Settings.Default.AutoNameOutputFolder = this.DefaultPath;
						Settings.Default.AutoNameCustomFormat = this.CustomFormat;
						Settings.Default.AutoNameCustomFormatString = this.CustomFormatString;
						Settings.Default.OutputToSourceDirectory = this.OutputToSourceDirectory;
						Settings.Default.WhenFileExists = this.WhenFileExists;
						Settings.Default.WhenFileExistsBatch = this.WhenFileExistsBatch;
						Settings.Default.MinimizeToTray = this.MinimizeToTray;
						Settings.Default.PlaySoundOnCompletion = this.PlaySoundOnCompletion;
						Settings.Default.AutoAudio = this.AutoAudio;
						Settings.Default.AudioLanguageCode = this.AudioLanguageCode;
						Settings.Default.AutoAudioAll = this.AutoAudioAll;
						Settings.Default.AutoSubtitle = this.AutoSubtitle;
						Settings.Default.AutoSubtitleBurnIn = this.AutoSubtitleBurnIn;
						Settings.Default.SubtitleLanguageCode = this.SubtitleLanguageCode;
						Settings.Default.AutoSubtitleOnlyIfDifferent = this.AutoSubtitleOnlyIfDifferent;
						Settings.Default.AutoSubtitleAll = this.AutoSubtitleAll;
						Settings.Default.LogVerbosity = this.LogVerbosity;
						var autoPauseStringCollection = new StringCollection();
						foreach (string process in this.AutoPauseProcesses)
						{
							autoPauseStringCollection.Add(process);
						}

						Settings.Default.AutoPauseProcesses = autoPauseStringCollection;
						Settings.Default.PreviewCount = this.PreviewCount;
						Settings.Default.RememberPreviousFiles = this.RememberPreviousFiles;
						// Clear out any file/folder history when this is disabled.
						if (!this.RememberPreviousFiles)
						{
							Settings.Default.LastCsvFolder = null;
							Settings.Default.LastInputFileFolder = null;
							Settings.Default.LastOutputFolder = null;
							Settings.Default.LastPresetExportFolder = null;
							Settings.Default.LastSrtFolder = null;
							Settings.Default.LastVideoTSFolder = null;

							Settings.Default.SourceHistory = null;
						}

						Settings.Default.ShowAudioTrackNameField = this.ShowAudioTrackNameField;
						Settings.Default.EnableLibDvdNav = this.EnableLibDvdNav;
						Settings.Default.KeepScansAfterCompletion = this.KeepScansAfterCompletion;
						Settings.Default.DeleteSourceFilesOnClearingCompleted = this.DeleteSourceFilesOnClearingCompleted;
						Settings.Default.MinimumTitleLengthSeconds = this.MinimumTitleLengthSeconds;
						Settings.Default.VideoFileExtensions = this.VideoFileExtensions;

						Settings.Default.PreferredPlayer = this.selectedPlayer.Id;

						Settings.Default.Save();

						Messenger.Default.Send(new OptionsChangedMessage());

						this.AcceptCommand.Execute(null);
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
