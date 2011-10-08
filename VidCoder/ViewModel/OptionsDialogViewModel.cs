using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Input;
using GalaSoft.MvvmLight.Command;
using VidCoder.Model;
using VidCoder.Services;
using System.IO;
using VidCoder.Properties;
using System.Collections.Specialized;
using System.Collections.ObjectModel;

namespace VidCoder.ViewModel
{
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
		private string nativeLanguageCode;
		private bool dubAudio;
		private int logVerbosity;
		private ObservableCollection<string> autoPauseProcesses;
		private string selectedProcess;
		private int previewCount;
		private bool showAudioTrackNameField;

		private int initialLogVerbosity;

		private IUpdater updateService;

		public OptionsDialogViewModel(IUpdater updateService)
		{
			this.updateService = updateService;
			this.updateDownloading = updateService.UpdateDownloading;
			this.updateService.UpdateDownloadProgress += this.OnUpdateDownloadProgress;
			this.updateService.UpdateDownloadCompleted += this.OnUpdateDownloadCompleted;

			this.updatesEnabled = Settings.Default.UpdatesEnabled;
			this.defaultPath = Settings.Default.AutoNameOutputFolder;
			this.customFormat = Settings.Default.AutoNameCustomFormat;
			this.customFormatString = Settings.Default.AutoNameCustomFormatString;
			this.outputToSourceDirectory = Settings.Default.OutputToSourceDirectory;
			this.whenFileExists = Settings.Default.WhenFileExists;
			this.whenFileExistsBatch = Settings.Default.WhenFileExistsBatch;
			this.minimizeToTray = Settings.Default.MinimizeToTray;
			this.nativeLanguageCode = Settings.Default.NativeLanguageCode;
			this.dubAudio = Settings.Default.DubAudio;
			this.logVerbosity = Settings.Default.LogVerbosity;
			this.previewCount = Settings.Default.PreviewCount;
			this.showAudioTrackNameField = Settings.Default.ShowAudioTrackNameField;
			this.autoPauseProcesses = new ObservableCollection<string>();
			StringCollection autoPauseStringCollection = Settings.Default.AutoPauseProcesses;
			if (autoPauseStringCollection != null)
			{
				foreach (string process in autoPauseStringCollection)
				{
					this.autoPauseProcesses.Add(process);
				}
			}

			this.initialLogVerbosity = this.LogVerbosity;
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
				this.RaisePropertyChanged("UpdatesEnabled");
			}
		}

		public string UpdateStatus
		{
			get
			{
				if (this.updateService.UpdateReady)
				{
					return "Update ready. It will install when you exit the program.";
				}

				if (this.UpdateDownloading)
				{
					return "Downloading:";
				}

				if (this.updateService.UpToDate)
				{
					return "VidCoder is up to date.";
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
				this.RaisePropertyChanged("UpdateDownloading");
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
				this.RaisePropertyChanged("UpdateProgress");
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
				this.RaisePropertyChanged("DefaultPath");
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
				this.RaisePropertyChanged("CustomFormat");
				this.RaisePropertyChanged("CustomFormatStringEnabled");
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
				this.RaisePropertyChanged("CustomFormatString");
			}
		}

		public bool CustomFormatStringEnabled
		{
			get
			{
				return this.CustomFormat;
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
				this.RaisePropertyChanged("OutputToSourceDirectory");
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
				this.RaisePropertyChanged("WhenFileExists");
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
				this.RaisePropertyChanged("WhenFileExistsBatch");
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
				this.RaisePropertyChanged("MinimizeToTray");
			}
		}

		public string NativeLanguageCode
		{
			get
			{
				return this.nativeLanguageCode;
			}

			set
			{
				this.nativeLanguageCode = value;
				this.RaisePropertyChanged("NativeLanguageCode");
				this.RaisePropertyChanged("DubAudioEnabled");
			}
		}

		public bool DubAudio
		{
			get
			{
				return this.dubAudio;
			}

			set
			{
				this.dubAudio = value;
				this.RaisePropertyChanged("DubAudio");
			}
		}

		public bool DubAudioEnabled
		{
			get
			{
				return this.NativeLanguageCode != "und";
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
				this.RaisePropertyChanged("LogVerbosity");
				this.RaisePropertyChanged("LogVerbosityWarningVisible");
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
				this.RaisePropertyChanged("SelectedProcess");
				this.RaisePropertyChanged("ProcessSelected");
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
				this.RaisePropertyChanged("PreviewCount");
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
				this.RaisePropertyChanged("ShowAudioTrackNameField");
			}
		}

		public bool LogVerbosityWarningVisible
		{
			get
			{
				return this.LogVerbosity != this.initialLogVerbosity;
			}
		}

		private RelayCommand saveSettingsCommand;
		public RelayCommand SaveSettingsCommand
		{
			get
			{
				return this.saveSettingsCommand ?? (this.saveSettingsCommand = new RelayCommand(() =>
					{
						if (Settings.Default.UpdatesEnabled != this.UpdatesEnabled)
						{
							Settings.Default.UpdatesEnabled = this.UpdatesEnabled;
							this.updateService.HandleUpdatedSettings(this.UpdatesEnabled);
						}

						Settings.Default.AutoNameOutputFolder = this.DefaultPath;
						Settings.Default.AutoNameCustomFormat = this.CustomFormat;
						Settings.Default.AutoNameCustomFormatString = this.CustomFormatString;
						Settings.Default.OutputToSourceDirectory = this.OutputToSourceDirectory;
						Settings.Default.WhenFileExists = this.WhenFileExists;
						Settings.Default.WhenFileExistsBatch = this.WhenFileExistsBatch;
						Settings.Default.MinimizeToTray = this.MinimizeToTray;
						Settings.Default.NativeLanguageCode = this.NativeLanguageCode;
						Settings.Default.DubAudio = this.DubAudio;
						Settings.Default.LogVerbosity = this.LogVerbosity;
						var autoPauseStringCollection = new StringCollection();
						foreach (string process in this.AutoPauseProcesses)
						{
							autoPauseStringCollection.Add(process);
						}

						Settings.Default.AutoPauseProcesses = autoPauseStringCollection;
						Settings.Default.PreviewCount = this.PreviewCount;
						Settings.Default.ShowAudioTrackNameField = this.ShowAudioTrackNameField;
						Settings.Default.Save();

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

						string newFolder = FileService.Instance.GetFolderName(initialDirectory, "Choose the default output directory for encoded video files.");
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
			this.RaisePropertyChanged("UpdateStatus");
		}

		private void OnUpdateDownloadProgress(object sender, EventArgs<double> e)
		{
			this.UpdateProgress = e.Value;
		}
	}
}
