using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Input;
using VidCoder.Services;
using System.IO;
using VidCoder.Properties;

namespace VidCoder.ViewModel
{
    public class OptionsDialogViewModel : OkCancelDialogViewModel
    {
        private bool autoNameOutputFiles;
        private bool updatesEnabled;
        private bool updateDownloading;
        private double updateProgress;
        private string defaultPath;
        private bool customFormat;
        private string customFormatString;
        private string nativeLanguageCode;
        private bool dubAudio;
        private int logVerbosity;

        private int initialLogVerbosity;

        private ICommand saveSettingsCommand;
        private ICommand browsePathCommand;

        private IUpdateService updateService;

        public OptionsDialogViewModel(IUpdateService updateService)
        {
            this.updateService = updateService;
            this.updateDownloading = updateService.UpdateDownloading;
            this.updateService.UpdateDownloadProgress += this.OnUpdateDownloadProgress;
            this.updateService.UpdateDownloadCompleted += this.OnUpdateDownloadCompleted;

            this.UpdatesEnabled = Settings.Default.UpdatesEnabled;
            this.AutoNameOutputFiles = Settings.Default.AutoNameOutputFiles;
            this.DefaultPath = Settings.Default.AutoNameOutputFolder;
            this.CustomFormat = Settings.Default.AutoNameCustomFormat;
            this.CustomFormatString = Settings.Default.AutoNameCustomFormatString;
            this.NativeLanguageCode = Settings.Default.NativeLanguageCode;
            this.DubAudio = Settings.Default.DubAudio;
            this.LogVerbosity = Settings.Default.LogVerbosity;

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
                this.NotifyPropertyChanged("UpdatesEnabled");
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
                this.NotifyPropertyChanged("UpdateDownloading");
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
                this.NotifyPropertyChanged("UpdateProgress");
            }
        }

        public bool AutoNameOutputFiles
        {
            get
            {
                return this.autoNameOutputFiles;
            }

            set
            {
                this.autoNameOutputFiles = value;
                this.NotifyPropertyChanged("AutoNameOutputFiles");
                this.NotifyPropertyChanged("CustomFormatStringEnabled");
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
                this.NotifyPropertyChanged("DefaultPath");
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
                this.NotifyPropertyChanged("CustomFormat");
                this.NotifyPropertyChanged("CustomFormatStringEnabled");
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
                this.NotifyPropertyChanged("CustomFormatString");
            }
        }

        public bool CustomFormatStringEnabled
        {
            get
            {
                return this.AutoNameOutputFiles && this.CustomFormat;
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
                this.NotifyPropertyChanged("NativeLanguageCode");
                this.NotifyPropertyChanged("DubAudioEnabled");
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
                this.NotifyPropertyChanged("DubAudio");
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
                this.NotifyPropertyChanged("LogVerbosity");
                this.NotifyPropertyChanged("LogVerbosityWarningVisible");
            }
        }

        public bool LogVerbosityWarningVisible
        {
            get
            {
                return this.LogVerbosity != this.initialLogVerbosity;
            }
        }

        public ICommand SaveSettingsCommand
        {
            get
            {
                if (this.saveSettingsCommand == null)
                {
                    this.saveSettingsCommand = new RelayCommand(param =>
                    {
                        bool autoName = this.AutoNameOutputFiles;
                        if (autoName && string.IsNullOrEmpty(this.DefaultPath))
                        {
                            autoName = false;
                        }

                        if (Settings.Default.UpdatesEnabled != this.UpdatesEnabled)
                        {
                            Settings.Default.UpdatesEnabled = this.UpdatesEnabled;
                            this.updateService.HandleUpdatedSettings(this.UpdatesEnabled);
                        }

                        Settings.Default.AutoNameOutputFiles = autoName;
                        Settings.Default.AutoNameOutputFolder = this.DefaultPath;
                        Settings.Default.AutoNameCustomFormat = this.CustomFormat;
                        Settings.Default.AutoNameCustomFormatString = this.CustomFormatString;
                        Settings.Default.NativeLanguageCode = this.NativeLanguageCode;
                        Settings.Default.DubAudio = this.DubAudio;
                        Settings.Default.LogVerbosity = this.LogVerbosity;
                        Settings.Default.Save();

                        this.AcceptCommand.Execute(null);
                    });
                }

                return this.saveSettingsCommand;
            }
        }

        public ICommand BrowsePathCommand
        {
            get
            {
                if (this.browsePathCommand == null)
                {
                    this.browsePathCommand = new RelayCommand(param =>
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
                    });
                }

                return this.browsePathCommand;
            }
        }

        private void OnUpdateDownloadCompleted(object sender, EventArgs e)
        {
            this.UpdateDownloading = false;
            this.NotifyPropertyChanged("UpdateStatus");
        }

        private void OnUpdateDownloadProgress(object sender, EventArgs<double> e)
        {
            this.UpdateProgress = e.Value;
        }
    }
}
