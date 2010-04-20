using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.ObjectModel;
using System.Windows;
using System.IO;
using System.Windows.Input;
using System.Windows.Shell;
using HandBrake.Interop;
using HandBrake.SourceData;
using VidCoder.Model;
using VidCoder.Properties;
using VidCoder.Services;
using System.Diagnostics;

namespace VidCoder.ViewModel
{
    public class MainViewModel : ViewModelBase
    {
        private HandBrakeInstance scanInstance;

        private IUpdateService updateService;
        private Logger logger;

        private SourceOption selectedSource;
        private string sourceDescription;
        private Title selectedTitle;
        private Title oldTitle;

        private Chapter selectedStartChapter;
        private Chapter selectedEndChapter;

        private ObservableCollection<AudioChoiceViewModel> audioChoices;

        private Subtitles currentSubtitles;

        private bool useDefaultChapterNames;
        private List<string> customChapterNames;

        private VideoSource sourceData;
        private string sourcePath;
        private string sourceName;

        private IDriveService driveService;
        private IList<DriveInformation> driveCollection;
        private bool scanningSource;
        private bool scanError;
        private double scanProgress;

        private string outputPath;
        private bool manualOutputPath;

        private List<Preset> builtInPresets;
        private ObservableCollection<PresetViewModel> allPresets;
        private PresetViewModel selectedPreset;

        private ObservableCollection<EncodeJobViewModel> encodeQueue;
        private bool encoding;
        private bool paused;
        private bool encodeStopped;
        private int totalTasks;
        private int taskNumber;
        private bool encodeSpeedDetailsAvailable;
        private string estimatedTimeRemaining;
        private double currentFps;
        private double averageFps;
        private TimeSpan completedQueueTime;
        private TimeSpan totalQueueTime;
        private double overallEncodeProgressFraction;
        private TaskbarItemProgressState encodeProgressState;

        private bool encodingWindowOpen;
        private bool previewWindowOpen;
        private bool logWindowOpen;

        private ICommand openSubtitlesDialogCommand;
        private ICommand openChaptersDialogCommand;
        private ICommand openAboutDialogCommand;
        private ICommand addTrackCommand;
        private ICommand openEncodingWindowCommand;
        private ICommand openPreviewWindowCommand;
        private ICommand openLogWindowCommand;
        private ICommand pickOutputFolderCommand;
        private ICommand pickOutputPathCommand;
        private ICommand encodeCommand;
        private ICommand addToQueueCommand;
        private ICommand pauseCommand;
        private ICommand stopEncodeCommand;
        private ICommand openOptionsCommand;

        public event EventHandler<EventArgs<string>> AnimationStarted;

        public MainViewModel()
        {
            // Upgrade from previous user settings.
            if (Settings.Default.ApplicationVersion != Utilities.CurrentVersion)
            {
                Settings.Default.Upgrade();
                Settings.Default.ApplicationVersion = Utilities.CurrentVersion;
            }

            this.logger = new Logger();

            this.updateService = ServiceFactory.UpdateService;
            this.updateService.HandlePendingUpdate();
            this.updateService.CheckUpdates();

            List<Preset> presets = new List<Preset>();

            System.Diagnostics.Stopwatch watch = System.Diagnostics.Stopwatch.StartNew();
            this.builtInPresets = Presets.BuiltInPresets;
            List<Preset> userPresets = Presets.UserPresets;
            watch.Stop();
            System.Diagnostics.Debug.WriteLine("Loading presets took: " + watch.Elapsed);

            this.allPresets = new ObservableCollection<PresetViewModel>();
            var unmodifiedPresets = userPresets.Where(preset => !preset.IsModified);
            Preset modifiedPreset = userPresets.SingleOrDefault(preset => preset.IsModified);

            foreach (Preset userPreset in unmodifiedPresets)
            {
                PresetViewModel presetVM;
                if (modifiedPreset != null && modifiedPreset.Name == userPreset.Name)
                {
                    presetVM = new PresetViewModel(modifiedPreset);
                    presetVM.OriginalProfile = userPreset.EncodingProfile;
                }
                else
                {
                    presetVM = new PresetViewModel(userPreset);
                }

                this.allPresets.Add(presetVM);
            }

            foreach (Preset builtInPreset in this.builtInPresets)
            {
                PresetViewModel presetVM;
                if (modifiedPreset != null && modifiedPreset.Name == builtInPreset.Name)
                {
                    presetVM = new PresetViewModel(modifiedPreset);
                    presetVM.OriginalProfile = builtInPreset.EncodingProfile;
                }
                else
                {
                    presetVM = new PresetViewModel(builtInPreset);
                }

                this.allPresets.Add(presetVM);
            }

            this.useDefaultChapterNames = true;

            this.encodeQueue = new ObservableCollection<EncodeJobViewModel>();
            this.encodeQueue.CollectionChanged += this.OnEncodeQueueChanged;
            List<EncodeJob> encodeJobs = EncodeJobsPersist.EncodeJobs;
            foreach (EncodeJob job in encodeJobs)
            {
                this.encodeQueue.Add(new EncodeJobViewModel(job, this));
            }

            int presetIndex = Properties.Settings.Default.LastPresetIndex;
            if (presetIndex >= this.allPresets.Count)
            {
                presetIndex = 0;
            }

            this.SelectedPreset = this.allPresets[presetIndex];

            this.driveService = ServiceFactory.CreateDriveService(this);

            IList<DriveInformation> driveCollection = this.driveService.GetDriveInformation();
            this.DriveCollection = driveCollection;

            this.audioChoices = new ObservableCollection<AudioChoiceViewModel>();
        }

        public HandBrakeInstance ScanInstance
        {
            get
            {
                return this.scanInstance;
            }
        }

        public Logger Logger
        {
            get
            {
                return this.logger;
            }
        }

        public EncodeJobViewModel CurrentJob
        {
            get
            {
                return this.EncodeQueue[0];
            }
        }

        public void UpdateDriveCollection()
        {
            if (!System.Windows.Threading.Dispatcher.CurrentDispatcher.CheckAccess())
            {
                System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(new Action(this.UpdateDriveCollection));
                return;
            }

            // Get new set of drives
            IList<DriveInformation> newDriveCollection = this.driveService.GetDriveInformation();

            // If our current drive is not present
            if (this.SelectedSource.Type == SourceType.Dvd)
            {
                string currentRootDirectory = this.SelectedSource.DriveInfo.RootDirectory;
                bool currentDrivePresent = newDriveCollection.Any(driveInfo => driveInfo.RootDirectory == currentRootDirectory);

                if (this.SelectedSource.DriveInfo.Empty && currentDrivePresent)
                {
                    this.SelectedSource.DriveInfo.Empty = false;
                    this.SelectedSource.DriveInfo.VolumeLabel = newDriveCollection.Single(driveInfo => driveInfo.RootDirectory == currentRootDirectory).VolumeLabel;
                    this.SetSourceFromDvd(this.SelectedSource.DriveInfo);
                    this.NotifyPropertyChanged("HasVideoSource");
                }
                else if (!this.SelectedSource.DriveInfo.Empty && !currentDrivePresent)
                {
                    DriveInformation emptyDrive = this.SelectedSource.DriveInfo;
                    emptyDrive.Empty = true;

                    newDriveCollection.Add(emptyDrive);

                    this.sourceData = null;
                    this.SelectedTitle = null;
                    this.NotifyPropertyChanged("HasVideoSource");
                }
            }

            this.DriveCollection = newDriveCollection;
        }

        public bool SetSourceFromFile()
        {
            this.ScanError = false;
            string videoFile = FileService.Instance.GetFileNameLoad(null, null, Properties.Settings.Default.LastInputFileFolder);

            if (videoFile != null)
            {
                this.SetSourceFromFile(videoFile);
                return true;
            }

            return false;
        }

        public void SetSourceFromFile(string videoFile)
        {
            Properties.Settings.Default.LastInputFileFolder = Path.GetDirectoryName(videoFile);
            Properties.Settings.Default.Save();

            this.sourceName = Path.GetFileNameWithoutExtension(videoFile);
            this.ScanningSource = true;
            this.StartScan(videoFile);

            this.SourceDescription = videoFile;
            this.sourcePath = videoFile;
        }

        public bool SetSourceFromFolder()
        {
            this.ScanError = false;
            string folderPath = FileService.Instance.GetFolderName(Properties.Settings.Default.LastVideoTSFolder);

            if (folderPath != null)
            {
                Properties.Settings.Default.LastVideoTSFolder = folderPath;
                Properties.Settings.Default.Save();
                DirectoryInfo parentDirectory = Directory.GetParent(folderPath);
                if (parentDirectory == null || parentDirectory.Root.FullName == parentDirectory.FullName)
                {
                    this.sourceName = "VIDEO_TS";
                }
                else
                {
                    this.sourceName = parentDirectory.Name;
                }

                this.ScanningSource = true;
                this.StartScan(folderPath);

                this.SourceDescription = folderPath;
                this.sourcePath = folderPath;
                return true;
            }

            return false;
        }

        public bool SetSourceFromDvd(DriveInformation driveInfo)
        {
            this.ScanError = false;
            this.sourceName = driveInfo.VolumeLabel;
            this.ScanningSource = true;
            this.sourcePath = Path.Combine(driveInfo.RootDirectory, "VIDEO_TS");
            this.StartScan(this.sourcePath);

            this.SourceDescription = string.Empty;
            return true;
        }

        public void OnLoaded()
        {
            if (Settings.Default.EncodingWindowOpen)
            {
                this.OpenEncodingWindow();
            }

            if (Settings.Default.PreviewWindowOpen)
            {
                this.OpenPreviewWindow();
            }

            if (Settings.Default.LogWindowOpen)
            {
                this.OpenLogWindow();
            }
        }

        /// <summary>
        /// Returns true if we can close.
        /// </summary>
        /// <returns>True if the form can close.</returns>
        public bool OnClosing()
        {
            if (this.Encoding)
            {
                MessageBoxResult result = ServiceFactory.MessageBoxService.Show(
                    this,
                    "There are encodes in progress. Are you sure you want to quit?",
                    "Encodes in progress",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.No)
                {
                    return false;
                }
            }

            // If we're quitting, see if the encode is still going.
            if (this.Encoding)
            {
                // If so, stop it.
                this.CurrentJob.HandBrakeInstance.StopEncode();
            }

            ViewModelBase encodingWindow = WindowManager.FindWindow(typeof(EncodingViewModel));
            if (encodingWindow != null)
            {
                WindowManager.Close(encodingWindow);
            }

            ViewModelBase previewWindow = WindowManager.FindWindow(typeof(PreviewViewModel));
            if (previewWindow != null)
            {
                WindowManager.Close(previewWindow);
            }

            ViewModelBase logWindow = WindowManager.FindWindow(typeof(LogViewModel));
            if (logWindow != null)
            {
                WindowManager.Close(logWindow);
            }

            Settings.Default.EncodingWindowOpen = encodingWindow != null;
            Settings.Default.PreviewWindowOpen = previewWindow != null;
            Settings.Default.LogWindowOpen = logWindow != null;
            Settings.Default.Save();

            this.driveService.Close();
            HandBrakeInstance.DisposeGlobal();

            this.updateService.PromptToApplyUpdate();

            this.logger.Dispose();

            return true;
        }

        public IList<DriveInformation> DriveCollection
        {
            get
            {
                return this.driveCollection;
            }

            set
            {
                this.driveCollection = value;
                this.NotifyPropertyChanged("DriveCollection");
            }
        }

        public SourceOption SelectedSource
        {
            get
            {
                return this.selectedSource;
            }

            set
            {
                this.selectedSource = value;
            }
        }

        public string SourceDescription
        {
            get
            {
                return this.sourceDescription;
            }

            set
            {
                this.sourceDescription = value;
                this.NotifyPropertyChanged("SourceDescription");
                this.NotifyPropertyChanged("SourceDescriptionVisible");
            }
        }

        public bool SourceDescriptionVisible
        {
            get
            {
                return !string.IsNullOrEmpty(this.SourceDescription);
            }
        }

        public bool ScanningSource
        {
            get
            {
                return this.scanningSource;
            }

            set
            {
                this.scanningSource = value;
                this.NotifyPropertyChanged("HasVideoSource");
                this.NotifyPropertyChanged("ScanningSource");
            }
        }

        public bool ScanError
        {
            get
            {
                return this.scanError;
            }

            set
            {
                this.scanError = value;
                this.NotifyPropertyChanged("ScanError");
            }
        }

        public double ScanProgress
        {
            get
            {
                return this.scanProgress;
            }

            set
            {
                this.scanProgress = value;
                this.NotifyPropertyChanged("ScanProgress");
            }
        }

        public bool HasVideoSource
        {
            get
            {
                if (this.SelectedSource == null)
                {
                    return false;
                }

                if (this.SelectedSource.Type == SourceType.None)
                {
                    return false;
                }

                if (this.SelectedSource.Type == SourceType.Dvd && this.SelectedSource.DriveInfo.Empty)
                {
                    return false;
                }

                if (this.ScanningSource)
                {
                    return false;
                }

                if (this.sourceData == null)
                {
                    return false;
                }

                return true;
            }
        }

        public List<Title> Titles
        {
            get
            {
                if (this.sourceData != null)
                {
                    return this.sourceData.Titles;
                }

                return null;
            }
        }

        public Title SelectedTitle
        {
            get
            {
                return this.selectedTitle;
            }

            set
            {
                PreviewViewModel previewWindow = WindowManager.FindWindow(typeof(PreviewViewModel)) as PreviewViewModel;

                if (value == null)
                {
                    this.selectedTitle = null;

                    if (previewWindow != null)
                    {
                        previewWindow.RefreshPreviews();
                        //previewWindow.Job = null;
                    }
                }
                else
                {
                    // Save old title, transfer from it when disc returns.
                    this.selectedTitle = value;

                    // Re-enable auto-naming when switching titles.
                    if (this.oldTitle != null)
                    {
                        this.manualOutputPath = false;
                    }

                    // Keep audio/subtitle choices if they match up in index and language.
                    Subtitles oldSubtitles = this.CurrentSubtitles;
                    this.CurrentSubtitles = new Subtitles { SourceSubtitles = new List<SourceSubtitle>(), SrtSubtitles = new List<SrtSubtitle>() };

                    if (this.oldTitle != null)
                    {
                        var keptAudioChoices = new List<AudioChoiceViewModel>();
                        foreach (AudioChoiceViewModel audioChoiceVM in this.AudioChoices)
                        {
                            if (audioChoiceVM.SelectedIndex < this.selectedTitle.AudioTracks.Count &&
                                this.oldTitle.AudioTracks[audioChoiceVM.SelectedIndex].Language == this.selectedTitle.AudioTracks[audioChoiceVM.SelectedIndex].Language)
                            {
                                keptAudioChoices.Add(audioChoiceVM);
                            }
                        }

                        this.AudioChoices.Clear();
                        foreach (AudioChoiceViewModel audioChoiceVM in keptAudioChoices)
                        {
                            this.AudioChoices.Add(audioChoiceVM);
                        }

                        if (oldSubtitles != null)
                        {
                            if (this.selectedTitle.Subtitles.Count > 0)
                            {
                                // Keep source subtitles when changing title, but not specific SRT files.
                                var keptSourceSubtitles = new List<SourceSubtitle>();
                                foreach (SourceSubtitle sourceSubtitle in oldSubtitles.SourceSubtitles)
                                {
                                    if (sourceSubtitle.TrackNumber == 0)
                                    {
                                        keptSourceSubtitles.Add(sourceSubtitle);
                                    }
                                    else if (sourceSubtitle.TrackNumber - 1 < this.selectedTitle.Subtitles.Count &&
                                        this.oldTitle.Subtitles[sourceSubtitle.TrackNumber - 1].LanguageCode == this.selectedTitle.Subtitles[sourceSubtitle.TrackNumber - 1].LanguageCode)
                                    {
                                        keptSourceSubtitles.Add(sourceSubtitle);
                                    }
                                }

                                foreach (SourceSubtitle sourceSubtitle in keptSourceSubtitles)
                                {
                                    this.CurrentSubtitles.SourceSubtitles.Add(sourceSubtitle);
                                }
                            }
                        }
                    }
                    else
                    {
                        this.AudioChoices.Clear();
                    }

                    // If we have a preferred language specified, try to apply that language
                    if (Settings.Default.NativeLanguageCode != "und")
                    {
                        // Only mess with the audio tracks if we're in dub mode and we're not carrying over previous selections
                        if (Settings.Default.DubAudio && (this.AudioChoices.Count == 0 || this.AudioChoices.Count == 1 && this.AudioChoices[0].SelectedIndex == 0))
                        {
                            List<AudioTrack> nativeTracks = this.selectedTitle.AudioTracks.Where(track => track.LanguageCode == Settings.Default.NativeLanguageCode).ToList();
                            if (nativeTracks.Count > 0)
                            {
                                this.AudioChoices.Clear();
                                AudioChoiceViewModel newVM = new AudioChoiceViewModel(this);
                                newVM.SelectedIndex = nativeTracks[0].TrackNumber - 1;
                                this.AudioChoices.Add(newVM);
                            }
                        }

                        // Try to apply the preferred language subtitle if no subtitles already exist
                        if (this.CurrentSubtitles.SourceSubtitles.Count == 0 && this.CurrentSubtitles.SrtSubtitles.Count == 0)
                        {
                            List<Subtitle> nativeSubtitles = this.selectedTitle.Subtitles.Where(subtitle => subtitle.LanguageCode == "iso639-2: " + Settings.Default.NativeLanguageCode).ToList();
                            if (nativeSubtitles.Count > 0)
                            {
                                this.CurrentSubtitles.SourceSubtitles.Add(new SourceSubtitle
                                {
                                    BurnedIn = false,
                                    Default = true,
                                    Forced = false,
                                    TrackNumber = nativeSubtitles[0].TrackNumber
                                });
                            }
                        }
                    }

                    if (this.selectedTitle.AudioTracks.Count > 0 && this.AudioChoices.Count == 0)
                    {
                        AudioChoiceViewModel newVM = new AudioChoiceViewModel(this);
                        newVM.SelectedIndex = 0;
                        this.AudioChoices.Add(newVM);
                    }

                    this.UseDefaultChapterNames = true;
                    this.SelectedStartChapter = this.selectedTitle.Chapters[0];
                    this.SelectedEndChapter = this.selectedTitle.Chapters[this.selectedTitle.Chapters.Count - 1];

                    if (previewWindow != null)
                    {
                        previewWindow.RefreshPreviews();
                    }

                    this.oldTitle = value;
                }

                this.NotifyPropertyChanged("SubtitlesSummary");
                CommandManager.InvalidateRequerySuggested();
                this.GenerateOutputFileName();

                this.NotifyPropertyChanged("Chapters");
                this.NotifyPropertyChanged("SelectedTitle");
            }
        }

        public Subtitles CurrentSubtitles
        {
            get
            {
                return this.currentSubtitles;
            }

            set
            {
                this.currentSubtitles = value;
                this.NotifyPropertyChanged("CurrentSubtitles");
                this.NotifyPropertyChanged("SubtitlesSummary");
            }
        }

        public string SubtitlesSummary
        {
            get
            {
                if (!this.HasVideoSource || this.CurrentSubtitles == null || (this.CurrentSubtitles.SourceSubtitles.Count == 0 && this.CurrentSubtitles.SrtSubtitles.Count == 0))
                {
                    return "(None)";
                }

                List<string> trackSummaries = new List<string>();

                foreach (SourceSubtitle sourceSubtitle in this.CurrentSubtitles.SourceSubtitles)
                {
                    if (sourceSubtitle.TrackNumber == 0)
                    {
                        trackSummaries.Add("Foriegn Audio Search");
                    }
                    else
                    {
                        trackSummaries.Add(this.SelectedTitle.Subtitles[sourceSubtitle.TrackNumber - 1].Language);
                    }
                }

                foreach (SrtSubtitle srtSubtitle in this.currentSubtitles.SrtSubtitles)
                {
                    trackSummaries.Add(Language.Decode(srtSubtitle.LanguageCode));
                }

                if (trackSummaries.Count > 3)
                {
                    return trackSummaries.Count + " tracks";
                }

                StringBuilder summaryBuilder = new StringBuilder();
                for (int i = 0; i < trackSummaries.Count; i++)
                {
                    summaryBuilder.Append(trackSummaries[i]);

                    if (i != trackSummaries.Count - 1)
                    {
                        summaryBuilder.Append(", ");
                    }
                }

                return summaryBuilder.ToString();
            }
        }

        public bool UseDefaultChapterNames
        {
            get
            {
                return this.useDefaultChapterNames;
            }

            set
            {
                this.useDefaultChapterNames = value;
                this.NotifyPropertyChanged("UseDefaultChapterNames");
                this.NotifyPropertyChanged("ChapterMarkersSummary");
            }
        }

        public List<string> CustomChapterNames
        {
            get
            {
                return this.customChapterNames;
            }

            set
            {
                this.customChapterNames = value;
                this.NotifyPropertyChanged("CustomChapterNames");
            }
        }

        public string ChapterMarkersSummary
        {
            get
            {
                if (this.UseDefaultChapterNames)
                {
                    return "Default";
                }

                return "Custom";
            }
        }

        public bool ShowChapterMarkerUI
        {
            get
            {
                return this.SelectedPreset != null && this.SelectedPreset.Preset.EncodingProfile.IncludeChapterMarkers;
            }
        }

        public bool VideoVisible
        {
            get
            {
                return this.SelectedSource.Type != SourceType.None;
            }
        }

        public bool TitleVisible
        {
            get
            {
                return this.SelectedSource.Type == SourceType.Dvd || this.SelectedSource.Type == SourceType.VideoFolder;
            }
        }

        public bool ChaptersVisible
        {
            get
            {
                return this.SelectedSource.Type != SourceType.None;
            }
        }

        public List<Chapter> Chapters
        {
            get
            {
                if (this.SourceData != null)
                {
                    return this.selectedTitle.Chapters;
                }

                return null;
            }
        }

        public Chapter SelectedStartChapter
        {
            get
            {
                return this.selectedStartChapter;
            }

            set
            {
                this.selectedStartChapter = value;

                if (value == null)
                {
                    return;
                }

                if (this.SelectedEndChapter != null && this.SelectedEndChapter.ChapterNumber < this.selectedStartChapter.ChapterNumber)
                {
                    this.SelectedEndChapter = this.selectedStartChapter;
                }

                this.GenerateOutputFileName();

                this.NotifyPropertyChanged("SelectedStartChapter");
                this.NotifyPropertyChanged("ChaptersSummary");
            }
        }

        public Chapter SelectedEndChapter
        {
            get
            {
                return this.selectedEndChapter;
            }

            set
            {
                this.selectedEndChapter = value;

                if (value == null)
                {
                    return;
                }

                if (this.SelectedStartChapter != null && this.selectedEndChapter.ChapterNumber < this.SelectedStartChapter.ChapterNumber)
                {
                    this.SelectedStartChapter = this.selectedEndChapter;
                }

                this.GenerateOutputFileName();

                this.NotifyPropertyChanged("SelectedEndChapter");
                this.NotifyPropertyChanged("ChaptersSummary");
            }
        }

        public ObservableCollection<AudioChoiceViewModel> AudioChoices
        {
            get
            {
                return this.audioChoices;
            }
        }

        public List<AudioTrack> InputTracks
        {
            get
            {
                if (this.SourceData != null)
                {
                    return this.SelectedTitle.AudioTracks;
                }

                return null;
            }
        }

        public ICommand AddTrackCommand
        {
            get
            {
                if (this.addTrackCommand == null)
                {
                    this.addTrackCommand = new RelayCommand(
                        param =>
                        {
                            AudioChoiceViewModel newAudioChoice = new AudioChoiceViewModel(this);
                            newAudioChoice.SelectedIndex = this.GetFirstUnusedAudioTrack();
                            this.AudioChoices.Add(newAudioChoice);
                        },
                        param =>
                        {
                            if (this.SelectedTitle == null)
                            {
                                return true;
                            }

                            return this.SelectedTitle.AudioTracks.Count > 0;
                        });
                }

                return this.addTrackCommand;
            }
        }

        public string ChaptersSummary
        {
            get
            {
                if (this.SelectedStartChapter != null && this.SelectedEndChapter != null)
                {
                    string timeString = string.Format("{0:00}:{1:00}:{2:00}", this.SelectedTime.Hours, this.SelectedTime.Minutes, this.SelectedTime.Seconds);
                    return "of " + this.SelectedTitle.Chapters.Count + " (" + timeString + ")";
                }

                return string.Empty;
            }
        }

        public bool OutputFolderChosen
        {
            get
            {
                return !Properties.Settings.Default.AutoNameOutputFiles || !string.IsNullOrEmpty(Properties.Settings.Default.AutoNameOutputFolder);
            }
        }

        public string OutputPath
        {
            get
            {
                return this.outputPath;
            }

            set
            {
                this.outputPath = value;
                this.NotifyPropertyChanged("OutputPath");
            }
        }

        public VideoSource SourceData
        {
            get
            {
                return this.sourceData;
            }

            set
            {
                this.sourceData = value;
                this.UpdateFromNewVideoSource();
            }
        }

        public ObservableCollection<PresetViewModel> AllPresets
        {
            get
            {
                return this.allPresets;
            }
        }

        public PresetViewModel SelectedPreset
        {
            get
            {
                return this.selectedPreset;
            }

            set
            {
                if (value == null)
                {
                    return;
                }

                PresetViewModel previouslySelectedPreset = this.selectedPreset;
                bool changeSelectedPreset = true;

                if (this.selectedPreset != null && this.selectedPreset.Preset.IsModified)
                {
                    MessageBoxResult dialogResult = ServiceFactory.MessageBoxService.Show(this, "Do you want to save changes to your current preset?", "Save current preset?", MessageBoxButton.YesNoCancel);
                    if (dialogResult == MessageBoxResult.Yes)
                    {
                        this.SavePreset();
                    }
                    else if (dialogResult == MessageBoxResult.No)
                    {
                        this.RevertPreset(userInitiated: false);
                    }
                    else if (dialogResult == MessageBoxResult.Cancel)
                    {
                        // Queue up an action to switch back to this preset.
                        int currentPresetIndex = this.AllPresets.IndexOf(this.selectedPreset);

                        DispatchService.BeginInvoke(() =>
                        {
                            this.SelectedPreset = this.AllPresets[currentPresetIndex];
                        });

                        changeSelectedPreset = false;
                    }
                }

                this.selectedPreset = value;

                if (changeSelectedPreset)
                {
                    this.GenerateOutputFileName();

                    EncodingViewModel encodingWindow = WindowManager.FindWindow(typeof(EncodingViewModel)) as EncodingViewModel;
                    if (encodingWindow != null)
                    {
                        encodingWindow.EditingPreset = this.selectedPreset.Preset;
                    }

                    PreviewViewModel previewWindow = WindowManager.FindWindow(typeof(PreviewViewModel)) as PreviewViewModel;
                    if (previewWindow != null)
                    {
                        previewWindow.RefreshPreviews();
                    }

                    this.NotifyPropertyChanged("SelectedPreset");
                }

                this.NotifyPropertyChanged("ShowChapterMarkerUI");
                Properties.Settings.Default.LastPresetIndex = this.AllPresets.IndexOf(this.selectedPreset);
            }
        }

        public ObservableCollection<EncodeJobViewModel> EncodeQueue
        {
            get
            {
                return this.encodeQueue;
            }
        }

        public bool CanEnqueue
        {
            get
            {
                return this.HasVideoSource && !string.IsNullOrEmpty(this.OutputPath);
            }
        }

        public bool CanEncode
        {
            get
            {
                return this.EncodeQueue.Count > 0 || this.CanEnqueue;
            }
        }

        public bool Encoding
        {
            get
            {
                return this.encoding;
            }

            set
            {
                this.encoding = value;
                if (!value)
                {
                    this.EncodeSpeedDetailsAvailable = false;
                }

                this.NotifyPropertyChanged("PauseVisible");
                this.NotifyPropertyChanged("Encoding");
                this.NotifyPropertyChanged("EncodeButtonText");
            }
        }

        public bool Paused
        {
            get
            {
                return this.paused;
            }

            set
            {
                this.paused = value;
                this.NotifyPropertyChanged("PauseVisible");
                this.NotifyPropertyChanged("ProgressBarColor");
                this.NotifyPropertyChanged("Paused");
            }
        }

        public string EncodeButtonText
        {
            get
            {
                if (this.Encoding)
                {
                    return "Resume";
                }
                else
                {
                    return "Encode";
                }
            }
        }

        public bool PauseVisible
        {
            get
            {
                return this.Encoding && !this.Paused;
            }
        }

        public string TaskDetails
        {
            get
            {
                return this.taskNumber + "/" + this.totalTasks;
            }
        }

        public bool EncodeSpeedDetailsAvailable
        {
            get
            {
                return this.encodeSpeedDetailsAvailable;
            }

            set
            {
                this.encodeSpeedDetailsAvailable = value;
                this.NotifyPropertyChanged("EncodeSpeedDetailsAvailable");
            }
        }

        public string EstimatedTimeRemaining
        {
            get
            {
                return this.estimatedTimeRemaining;
            }

            set
            {
                this.estimatedTimeRemaining = value;
                this.NotifyPropertyChanged("EstimatedTimeRemaining");
            }
        }

        public double CurrentFps
        {
            get
            {
                return this.currentFps;
            }

            set
            {
                this.currentFps = value;
                this.NotifyPropertyChanged("CurrentFps");
            }
        }

        public double AverageFps
        {
            get
            {
                return this.averageFps;
            }

            set
            {
                this.averageFps = value;
                this.NotifyPropertyChanged("AverageFps");
            }
        }

        public double OverallEncodeProgressFraction
        {
            get
            {
                return this.overallEncodeProgressFraction;
            }

            set
            {
                this.overallEncodeProgressFraction = value;
                this.NotifyPropertyChanged("OverallEncodeProgressPercent");
                this.NotifyPropertyChanged("OverallEncodeProgressFraction");
            }
        }

        public double OverallEncodeProgressPercent
        {
            get
            {
                return this.overallEncodeProgressFraction * 100;
            }
        }

        public TaskbarItemProgressState EncodeProgressState
        {
            get
            {
                return this.encodeProgressState;
            }

            set
            {
                this.encodeProgressState = value;
                this.NotifyPropertyChanged("EncodeProgressState");
            }
        }

        public System.Windows.Media.Brush ProgressBarColor
        {
            get
            {
                if (this.Paused)
                {
                    return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xCC, 0xCC, 0));
                }
                else
                {
                    return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 200, 0));
                }
            }
        }

        public bool EncodingWindowOpen
        {
            get
            {
                return this.encodingWindowOpen;
            }

            set
            {
                this.encodingWindowOpen = value;
                this.NotifyPropertyChanged("EncodingWindowOpen");
            }
        }

        public bool PreviewWindowOpen
        {
            get
            {
                return this.previewWindowOpen;
            }

            set
            {
                this.previewWindowOpen = value;
                this.NotifyPropertyChanged("PreviewWindowOpen");
            }
        }

        public bool LogWindowOpen
        {
            get
            {
                return this.logWindowOpen;
            }

            set
            {
                this.logWindowOpen = value;
                this.NotifyPropertyChanged("LogWindowOpen");
            }
        }

        public ICommand OpenSubtitlesDialogCommand
        {
            get
            {
                if (this.openSubtitlesDialogCommand == null)
                {
                    this.openSubtitlesDialogCommand = new RelayCommand(param =>
                    {
                        SubtitleDialogViewModel subtitleViewModel = new SubtitleDialogViewModel(this, this.CurrentSubtitles);
                        subtitleViewModel.Closing = () =>
                        {
                            if (subtitleViewModel.DialogResult)
                            {
                                this.CurrentSubtitles = subtitleViewModel.ChosenSubtitles;
                            }
                        };

                        WindowManager.OpenDialog(subtitleViewModel, this);
                    });
                }

                return this.openSubtitlesDialogCommand;
            }
        }

        public ICommand OpenChaptersDialogCommand
        {
            get
            {
                if (this.openChaptersDialogCommand == null)
                {
                    this.openChaptersDialogCommand = new RelayCommand(param =>
                    {
                        var chaptersVM = new ChapterMarkersDialogViewModel(this.SelectedTitle.Chapters.Count, this.CustomChapterNames, this.UseDefaultChapterNames);
                        chaptersVM.Closing = () =>
                        {
                            if (chaptersVM.DialogResult)
                            {
                                this.UseDefaultChapterNames = chaptersVM.UseDefaultNames;
                                if (!chaptersVM.UseDefaultNames)
                                {
                                    this.CustomChapterNames = chaptersVM.ChapterNamesList;
                                }
                            }
                        };

                        WindowManager.OpenDialog(chaptersVM, this);
                    });
                }

                return this.openChaptersDialogCommand;
            }
        }

        public ICommand OpenEncodingWindowCommand
        {
            get
            {
                if (this.openEncodingWindowCommand == null)
                {
                    this.openEncodingWindowCommand = new RelayCommand(param =>
                    {
                        this.OpenEncodingWindow();
                    });
                }

                return this.openEncodingWindowCommand;
            }
        }

        public ICommand OpenPreviewWindowCommand
        {
            get
            {
                if (this.openPreviewWindowCommand == null)
                {
                    this.openPreviewWindowCommand = new RelayCommand(param =>
                    {
                        this.OpenPreviewWindow();
                    });
                }

                return this.openPreviewWindowCommand;
            }
        }

        public ICommand OpenLogWindowCommand
        {
            get
            {
                if (this.openLogWindowCommand == null)
                {
                    this.openLogWindowCommand = new RelayCommand(param =>
                    {
                        this.OpenLogWindow();
                    });
                }

                return this.openLogWindowCommand;
            }
        }

        public ICommand OpenAboutDialogCommand
        {
            get
            {
                if (this.openAboutDialogCommand == null)
                {
                    this.openAboutDialogCommand = new RelayCommand(param =>
                    {
                        WindowManager.OpenDialog(new AboutDialogViewModel(), this);
                    });
                }

                return this.openAboutDialogCommand;
            }
        }

        public ICommand PickOutputFolderCommand
        {
            get
            {
                if (this.pickOutputFolderCommand == null)
                {
                    this.pickOutputFolderCommand = new RelayCommand(param =>
                    {
                        string newOutputFolder = FileService.Instance.GetFolderName(null);

                        if (newOutputFolder != null)
                        {
                            Properties.Settings.Default.AutoNameOutputFolder = newOutputFolder;
                            Properties.Settings.Default.Save();
                            this.NotifyPropertyChanged("OutputFolderChosen");

                            if (this.HasVideoSource)
                            {
                                this.GenerateOutputFileName();
                            }
                        }
                    });
                }

                return this.pickOutputFolderCommand;
            }
        }

        public ICommand PickOutputPathCommand
        {
            get
            {
                if (this.pickOutputPathCommand == null)
                {
                    this.pickOutputPathCommand = new RelayCommand(param =>
                    {
                        string newOutputPath = FileService.Instance.GetFileNameSave(Properties.Settings.Default.LastOutputFolder);

                        if (newOutputPath != null)
                        {
                            Properties.Settings.Default.LastOutputFolder = Path.GetDirectoryName(newOutputPath);
                            Properties.Settings.Default.Save();
                            this.manualOutputPath = true;
                            this.OutputPath = newOutputPath;
                        }
                    });
                }

                return this.pickOutputPathCommand;
            }
        }

        public ICommand EncodeCommand
        {
            get
            {
                if (this.encodeCommand == null)
                {
                    this.encodeCommand = new RelayCommand(param =>
                    {
                        if (this.Encoding)
                        {
                            this.CurrentJob.HandBrakeInstance.ResumeEncode();
                            this.EncodeProgressState = TaskbarItemProgressState.Normal;

                            this.Paused = false;
                        }
                        else
                        {
                            if (this.EncodeQueue.Count == 0)
                            {
                                this.EncodeQueue.Add(new EncodeJobViewModel(this.EncodeJob, this));
                                this.EncodeQueue[0].HandBrakeInstance = this.scanInstance;
                            }

                            this.StartEncodeQueue();
                        }
                    },
                    param =>
                    {
                        return this.CanEncode;
                    });
                }

                return this.encodeCommand;
            }
        }

        public ICommand AddToQueueCommand
        {
            get
            {
                if (this.addToQueueCommand == null)
                {
                    this.addToQueueCommand = new RelayCommand(param =>
                    {
                        // Check for previous items in queue with this name.
                        bool foundExisting = false;
                        foreach (EncodeJobViewModel job in this.EncodeQueue)
                        {
                            if (job.Job.OutputPath.ToLowerInvariant() == this.OutputPath.ToLowerInvariant())
                            {
                                foundExisting = true;
                                break;
                            }
                        }

                        if (foundExisting)
                        {
                            MessageBoxResult result = ServiceFactory.MessageBoxService.Show(this, "There is already a queue item for this destination path." + Environment.NewLine + Environment.NewLine +
                                "If you continue, the encode will be overwritten. Do you wish to continue?", "Warning", MessageBoxButton.YesNo);

                            if (result == MessageBoxResult.No)
                            {
                                return;
                            }
                        }

                        var newEncodeJobVM = new EncodeJobViewModel(this.EncodeJob, this);
                        newEncodeJobVM.HandBrakeInstance = this.scanInstance;

                        if (this.Encoding)
                        {
                            if (this.totalTasks == 1)
                            {
                                this.EncodeQueue[0].IsOnlyItem = false;
                            }

                            this.totalTasks++;
                            this.totalQueueTime += newEncodeJobVM.Job.Length;
                            this.NotifyPropertyChanged("TaskDetails");
                        }

                        this.EncodeQueue.Add(newEncodeJobVM);

                    },
                    param =>
                    {
                        return this.CanEnqueue;
                    });
                }

                return this.addToQueueCommand;
            }
        }

        public ICommand PauseCommand
        {
            get
            {
                if (this.pauseCommand == null)
                {
                    this.pauseCommand = new RelayCommand(param =>
                    {
                        this.CurrentJob.HandBrakeInstance.PauseEncode();
                        this.EncodeProgressState = TaskbarItemProgressState.Paused;

                        this.Paused = true;
                    });
                }

                return this.pauseCommand;
            }
        }

        public ICommand StopEncodeCommand
        {
            get
            {
                if (this.stopEncodeCommand == null)
                {
                    this.stopEncodeCommand = new RelayCommand(param =>
                    {
                        // Signify that we stopped the encode manually rather than it completing.
                        this.encodeStopped = true;
                        this.CurrentJob.HandBrakeInstance.StopEncode();

                    });
                }

                return this.stopEncodeCommand;
            }
        }

        public ICommand OpenOptionsCommand
        {
            get
            {
                if (this.openOptionsCommand == null)
                {
                    this.openOptionsCommand = new RelayCommand(param =>
                    {
                        var optionsVM = new OptionsDialogViewModel(this.updateService);
                        WindowManager.OpenDialog(optionsVM, this);
                        if (optionsVM.DialogResult)
                        {
                            this.GenerateOutputFileName();
                        }
                    });
                }

                return this.openOptionsCommand;
            }
        }

        public void StartAnimation(string animationKey)
        {
            if (this.AnimationStarted != null)
            {
                this.AnimationStarted(this, new EventArgs<string>(animationKey));
            }
        }

        public EncodeJob EncodeJob
        {
            get
            {
                return new EncodeJob
                {
                    SourcePath = this.sourcePath,
                    OutputPath = this.OutputPath,
                    EncodingProfile = this.SelectedPreset.Preset.EncodingProfile,
                    Title = this.SelectedTitle.TitleNumber,
                    ChapterStart = this.SelectedStartChapter.ChapterNumber,
                    ChapterEnd = this.SelectedEndChapter.ChapterNumber,
                    ChosenAudioTracks = this.GetChosenAudioTracks(),
                    Subtitles = this.CurrentSubtitles,
                    UseDefaultChapterNames = this.UseDefaultChapterNames,
                    CustomChapterNames = this.CustomChapterNames,
                    Length = this.SelectedTime
                };
            }
        }

        public void StartEncodeQueue()
        {
            this.EncodeProgressState = TaskbarItemProgressState.Normal;
            this.logger.Log("## Starting queue");

            this.totalTasks = this.EncodeQueue.Count;
            this.taskNumber = 0;

            this.completedQueueTime = TimeSpan.Zero;
            this.totalQueueTime = TimeSpan.Zero;
            foreach (EncodeJobViewModel jobVM in this.EncodeQueue)
            {
                this.totalQueueTime += jobVM.Job.Length;

                // Add the job length twice for two-pass encoding.
                if (jobVM.Job.EncodingProfile.TwoPass)
                {
                    this.totalQueueTime += jobVM.Job.Length;
                }
            }

            this.OverallEncodeProgressFraction = 0;
            this.Encoding = true;
            this.Paused = false;
            this.encodeStopped = false;
            this.EncodeNextJob();
        }

        private void EncodeNextJob()
        {
            this.taskNumber++;
            this.NotifyPropertyChanged("TaskDetails");

            if (this.CurrentJob.HandBrakeInstance == null)
            {
                HandBrakeInstance onDemandInstance = new HandBrakeInstance();
                onDemandInstance.Initialize(2);
                onDemandInstance.ScanCompleted += (o, e) =>
                {
                    this.CurrentJob.HandBrakeInstance = onDemandInstance;
                    this.StartEncode();
                };

                onDemandInstance.StartScan(this.CurrentJob.Job.SourcePath, 10, this.CurrentJob.Job.Title);
            }
            else
            {
                this.StartEncode();
            }
        }

        private void StartEncode()
        {
            EncodeJob job = this.CurrentJob.Job;

            this.logger.Log("## Starting job " + this.taskNumber + "/" + this.totalTasks);
            this.logger.Log("##   Path: " + job.SourcePath);
            this.logger.Log("##   Title: " + job.Title);
            this.logger.Log("##   Chapters: " + job.ChapterStart + "-" + job.ChapterEnd);
            this.CurrentJob.HandBrakeInstance.EncodeProgress += this.OnEncodeProgress;
            this.CurrentJob.HandBrakeInstance.EncodeCompleted += this.OnEncodeCompleted;

            this.EncodeQueue[0].ReportEncodeStart(this.totalTasks == 1);
            this.CurrentJob.HandBrakeInstance.StartEncode(this.CurrentJob.Job);
        }

        private void OnEncodeProgress(object sender, EncodeProgressEventArgs e)
        {
            if (this.EncodeQueue.Count == 0)
            {
                return;
            }

            double currentJobLengthSeconds = this.EncodeQueue[0].Job.Length.TotalSeconds;

            double completedSeconds = this.completedQueueTime.TotalSeconds + currentJobLengthSeconds * e.FractionComplete;
            if (e.Pass == 2)
            {
                completedSeconds += currentJobLengthSeconds;
            }

            this.OverallEncodeProgressFraction = completedSeconds / this.totalQueueTime.TotalSeconds;

            this.EncodeQueue[0].PercentComplete = (int)(e.FractionComplete * 100);

            if (e.EstimatedTimeLeft >= TimeSpan.Zero)
            {
                this.CurrentFps = Math.Round(e.CurrentFrameRate, 1);
                this.AverageFps = Math.Round(e.AverageFrameRate, 1);
                this.EstimatedTimeRemaining = e.EstimatedTimeLeft.ToString();
                this.EncodeSpeedDetailsAvailable = true;
            }
        }

        private void OnEncodeCompleted(object sender, EventArgs e)
        {
            DispatchService.Invoke(() =>
            {
                // Unregister from events. This instance may be used again.
                this.EncodeQueue[0].HandBrakeInstance.EncodeProgress -= this.OnEncodeProgress;
                this.EncodeQueue[0].HandBrakeInstance.EncodeCompleted -= this.OnEncodeCompleted;

                if (this.encodeStopped)
                {
                    // If the encode was stopped manually
                    this.Encoding = false;
                    this.EncodeQueue[0].ReportEncodeEnd();

                    this.EncodeProgressState = TaskbarItemProgressState.None;

                    if (this.totalTasks == 1)
                    {
                        this.EncodeQueue.Clear();
                    }

                    this.logger.Log("## Encoding stopped");
                }
                else
                {
                    // If the encode completed successfully
                    this.completedQueueTime += this.EncodeQueue[0].Job.Length;
                    if (this.EncodeQueue[0].Job.EncodingProfile.TwoPass)
                    {
                        this.completedQueueTime += this.EncodeQueue[0].Job.Length;
                    }
                    
                    HandBrakeInstance finishedInstance = this.EncodeQueue[0].HandBrakeInstance;
                    this.EncodeQueue.RemoveAt(0);

                    this.CleanupHandBrakeInstance(finishedInstance);

                    this.logger.Log("## Job completed");

                    if (this.EncodeQueue.Count == 0)
                    {
                        this.EncodeProgressState = TaskbarItemProgressState.None;

                        this.logger.Log("## Queue completed");
                        this.logger.Log("");
                        this.Encoding = false;
                    }
                    else
                    {
                        this.EncodeNextJob();
                    }
                }
            });
        }

        /// <summary>
        /// Cleans up the given HandBrake instance if it's not being used anymore.
        /// </summary>
        /// <param name="instance">The instance to clean up.</param>
        private void CleanupHandBrakeInstance(HandBrakeInstance instance)
        {
            foreach (EncodeJobViewModel encodeJobVM in this.EncodeQueue)
            {
                if (instance == encodeJobVM.HandBrakeInstance)
                {
                    return;
                }
            }

            if (instance == this.scanInstance)
            {
                return;
            }

            instance.Dispose();
        }

        private void OnEncodeQueueChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            this.SaveEncodeQueue();
        }

        public void RemoveAudioChoice(AudioChoiceViewModel choice)
        {
            this.AudioChoices.Remove(choice);
        }

        public void RemoveQueueJob(EncodeJobViewModel job)
        {
            this.EncodeQueue.Remove(job);

            if (this.Encoding)
            {
                this.totalTasks--;
                this.totalQueueTime -= job.Job.Length;
                this.NotifyPropertyChanged("TaskDetails");

                if (this.totalTasks == 1)
                {
                    this.EncodeQueue[0].IsOnlyItem = true;
                }
            }
        }

        public void SavePreset()
        {
            if (this.SelectedPreset.IsModified)
            {
                this.SelectedPreset.OriginalProfile = null;
                this.SelectedPreset.Preset.IsModified = false;
            }

            // Refresh view and save in case of rename.
            this.SelectedPreset.RefreshView();
            this.SaveUserPresets();

            this.StartAnimation("PresetGlowHighlight");
        }

        public void SavePresetAs(string newPresetName)
        {
            var newPreset = new Preset
            {
                IsBuiltIn = false,
                IsModified = false,
                Name = newPresetName,
                EncodingProfile = this.SelectedPreset.Preset.EncodingProfile.Clone()
            };

            this.AllPresets.Insert(0, new PresetViewModel(newPreset));

            if (this.SelectedPreset.IsModified)
            {
                this.RevertPreset(userInitiated: false);
                this.SelectedPreset.RefreshView();
            }

            this.selectedPreset = null;
            this.SelectedPreset = this.AllPresets[0];

            this.StartAnimation("PresetGlowHighlight");
            this.SaveUserPresets();
        }

        /// <summary>
        /// Reverts changes to the current preset back to last saved.
        /// </summary>
        public void RevertPreset(bool userInitiated)
        {
            Debug.Assert(this.SelectedPreset.OriginalProfile != null, "Error reverting preset: Original profile cannot be null.");
            Debug.Assert(this.SelectedPreset.OriginalProfile != this.SelectedPreset.Preset.EncodingProfile, "Error reverting preset: Original profile must be different from current profile.");

            this.SelectedPreset.Preset.EncodingProfile = this.SelectedPreset.OriginalProfile;
            this.SelectedPreset.OriginalProfile = null;
            this.SelectedPreset.Preset.IsModified = false;
            this.SelectedPreset.RefreshView();

            if (userInitiated)
            {
                this.StartAnimation("PresetGlowHighlight");
            }

            this.SaveUserPresets();
        }

        /// <summary>
        /// Deletes the current preset.
        /// </summary>
        public void DeletePreset()
        {
            this.AllPresets.Remove(this.SelectedPreset);
            this.selectedPreset = null;
            this.SelectedPreset = this.AllPresets[0];

            this.StartAnimation("PresetGlowHighlight");
            this.SaveUserPresets();
        }

        /// <summary>
        /// Modify the current preset. Called when first making a change to a preset.
        /// </summary>
        /// <param name="newProfile">The new encoding profile to use.</param>
        public void ModifyPreset(EncodingProfile newProfile)
        {
            Debug.Assert(this.SelectedPreset.IsModified == false, "Cannot start modification on already modified preset.");
            Debug.Assert(this.SelectedPreset.OriginalProfile == null, "Preset already has OriginalProfile.");

            this.SelectedPreset.OriginalProfile = this.SelectedPreset.Preset.EncodingProfile;
            this.SelectedPreset.Preset.EncodingProfile = newProfile;
            this.SelectedPreset.Preset.IsModified = true;
            this.SelectedPreset.RefreshView();
        }

        public void RefreshChapterMarkerUI()
        {
            this.NotifyPropertyChanged("ShowChapterMarkerUI");
        }

        public void RefreshDestination()
        {
            this.GenerateOutputFileName();
        }

        public void SaveUserPresets()
        {
            List<Preset> userPresets = new List<Preset>();

            foreach (PresetViewModel presetVM in this.AllPresets)
            {
                if (!presetVM.Preset.IsBuiltIn || presetVM.Preset.IsModified)
                {
                    userPresets.Add(presetVM.Preset);
                }

                // Add the original version of the preset, if we're working with a more recent version.
                if (!presetVM.Preset.IsBuiltIn && presetVM.Preset.IsModified)
                {
                    Debug.Assert(presetVM.OriginalProfile != null, "Error saving user presets: Preset marked as modified but no OriginalProfile could be found.");

                    var originalPreset = new Preset
                    {
                        Name = presetVM.Preset.Name,
                        IsBuiltIn = presetVM.Preset.IsBuiltIn,
                        IsModified = false,
                        EncodingProfile = presetVM.OriginalProfile
                    };

                    userPresets.Add(originalPreset);
                }
            }

            //List<PresetViewModel> userPresetVMs = this.AllPresets.Where(preset => !preset.Preset.IsBuiltIn).ToList();
            //foreach (PresetViewModel userPresetVM in userPresetVMs)
            //{
            //    userPresets.Add(userPresetVM.Preset);
            //}

            Presets.UserPresets = userPresets;
        }

        public List<int> GetChosenAudioTracks()
        {
            var tracks = new List<int>();

            foreach (AudioChoiceViewModel audioChoiceVM in this.AudioChoices)
            {
                tracks.Add(audioChoiceVM.SelectedIndex + 1);
            }

            return tracks;
        }

        private TimeSpan SelectedTime
        {
            get
            {
                TimeSpan selectedTime = TimeSpan.Zero;

                int selectionStart = this.SelectedTitle.Chapters.IndexOf(this.SelectedStartChapter);
                int selectionEnd = this.SelectedTitle.Chapters.IndexOf(this.SelectedEndChapter);

                for (int i = selectionStart; i <= selectionEnd; i++)
                {
                    selectedTime += this.SelectedTitle.Chapters[i].Duration;
                }

                return selectedTime;
            }
        }

        private void StartScan(string path)
        {
            this.ScanProgress = 0;
            HandBrakeInstance oldInstance = this.scanInstance;
            if (oldInstance != null)
            {
                this.CleanupHandBrakeInstance(oldInstance);
            }

            this.logger.Log("## Starting scan: " + path);

            this.scanInstance = new HandBrakeInstance();
            this.scanInstance.Initialize(2);
            this.scanInstance.ScanProgress += (o, e) =>
            {
                this.ScanProgress = (e.CurrentTitle * 100) / e.Titles;
            };
            this.scanInstance.ScanCompleted += (o, e) =>
            {
                DispatchService.Invoke(() =>
                {
                    this.ScanningSource = false;
                    this.SourceData = new VideoSource { Titles = this.scanInstance.Titles };

                    this.logger.Log("## Scan completed");
                    this.logger.Log("");
                });
            };

            this.scanInstance.StartScan(path, 10);
        }

        private void UpdateFromNewVideoSource()
        {
            if (this.sourceData != null && this.sourceData.Titles.Count > 0)
            {
                this.ScanError = false;

                // Select the first title within 80% of the duration of the longest title.
                double maxSeconds = this.sourceData.Titles.Max(title => title.Duration.TotalSeconds);
                Title selectTitle = null;
                foreach (Title title in this.sourceData.Titles)
                {
                    if (title.Duration.TotalSeconds >= maxSeconds * .8)
                    {
                        selectTitle = title;
                        break;
                    }
                }

                if (selectTitle == null)
                {
                    selectTitle = this.sourceData.Titles[0];
                }

                this.SelectedTitle = selectTitle;
                this.NotifyPropertyChanged("Titles");
                this.NotifyPropertyChanged("VideoVisible");
                this.NotifyPropertyChanged("TitleVisible");
                this.NotifyPropertyChanged("ChaptersVisible");
                this.NotifyPropertyChanged("HasVideoSource");
            }
            else
            {
                this.ScanError = true;
            }
        }

        private void SaveEncodeQueue()
        {
            List<EncodeJob> jobs = new List<EncodeJob>();
            foreach (EncodeJobViewModel jobVM in this.EncodeQueue)
            {
                jobs.Add(jobVM.Job);
            }

            EncodeJobsPersist.EncodeJobs = jobs;
        }

        private void OpenEncodingWindow()
        {
            var encodingWindow = WindowManager.FindWindow(typeof(EncodingViewModel)) as EncodingViewModel;
            this.EncodingWindowOpen = true;

            if (encodingWindow == null)
            {
                encodingWindow = new EncodingViewModel(this.SelectedPreset.Preset, this);
                encodingWindow.Closing = () =>
                {
                    this.EncodingWindowOpen = false;
                };
                WindowManager.OpenWindow(encodingWindow, this);
            }
            else
            {
                WindowManager.FocusWindow(encodingWindow);
            }
        }

        private void OpenPreviewWindow()
        {
            var previewWindow = WindowManager.FindWindow(typeof(PreviewViewModel)) as PreviewViewModel;
            this.PreviewWindowOpen = true;

            if (previewWindow == null)
            {
                previewWindow = new PreviewViewModel(this);
                previewWindow.Closing = () =>
                {
                    this.PreviewWindowOpen = false;
                };
                WindowManager.OpenWindow(previewWindow, this);
            }
            else
            {
                WindowManager.FocusWindow(previewWindow);
            }
        }

        private void OpenLogWindow()
        {
            var logWindow = WindowManager.FindWindow(typeof(LogViewModel)) as LogViewModel;
            this.LogWindowOpen = true;

            if (logWindow == null)
            {
                logWindow = new LogViewModel(this.logger);
                logWindow.Closing = () =>
                {
                    this.LogWindowOpen = false;
                };
                WindowManager.OpenWindow(logWindow, this);
            }
            else
            {
                WindowManager.FocusWindow(logWindow);
            }
        }

        private void GenerateOutputFileName()
        {
            if (!this.HasVideoSource)
            {
                return;
            }

            if (this.sourceName == null || this.SelectedStartChapter == null || this.SelectedEndChapter == null)
            {
                return;
            }

            if (!Properties.Settings.Default.AutoNameOutputFiles)
            {
                return;
            }

            string fileName;

            if (this.manualOutputPath)
            {
                fileName = Path.GetFileNameWithoutExtension(this.OutputPath);
            }
            else
            {
                // Change casing on DVD titles to be a little more friendly
                string translatedSourceName = this.sourceName;
                if ((this.SelectedSource.Type == SourceType.Dvd || this.SelectedSource.Type == SourceType.VideoFolder) && !string.IsNullOrWhiteSpace(this.sourceName))
                {
                    string[] titleWords = this.sourceName.Split('_');
                    var translatedTitleWords = new List<string>();
                    bool reachedModifiers = false;

                    foreach (string titleWord in titleWords)
                    {
                        // After the disc designator, stop changing capitalization.
                        if (!reachedModifiers && titleWord.Length == 2 && titleWord[0] == 'D' && char.IsDigit(titleWord[1]))
                        {
                            reachedModifiers = true;
                        }

                        if (reachedModifiers)
                        {
                            translatedTitleWords.Add(titleWord);
                        }
                        else
                        {
                            if (titleWord.Length > 0)
                            {
                                translatedTitleWords.Add(titleWord[0] + titleWord.Substring(1).ToLower());
                            }
                        }
                    }

                    translatedSourceName = string.Join(" ", translatedTitleWords);
                }

                if (Properties.Settings.Default.AutoNameCustomFormat)
                {
                    string chapterString;
                    int startChapter = this.SelectedStartChapter.ChapterNumber;
                    int endChapter = this.SelectedEndChapter.ChapterNumber;
                    if (startChapter == endChapter)
                    {
                        chapterString = startChapter.ToString();
                    }
                    else
                    {
                        chapterString = startChapter + "-" + endChapter;
                    }

                    fileName = Properties.Settings.Default.AutoNameCustomFormatString;

                    fileName = fileName.Replace("{source}", translatedSourceName);
                    fileName = fileName.Replace("{title}", this.SelectedTitle.TitleNumber.ToString());
                    fileName = fileName.Replace("{chapters}", chapterString);
                }
                else
                {
                    //this.OutputPath = OutputSaveFolder;
                    string titleSection = string.Empty;
                    if (this.SelectedSource.Type != SourceType.File)
                    {
                        titleSection = " - Title " + this.SelectedTitle.TitleNumber;
                    }

                    string chaptersSection = string.Empty;
                    int startChapter = this.SelectedStartChapter.ChapterNumber;
                    int endChapter = this.SelectedEndChapter.ChapterNumber;
                    if (startChapter > 1 || endChapter < this.SelectedTitle.Chapters.Count)
                    {
                        if (startChapter == endChapter)
                        {
                            chaptersSection = " - Chapter " + startChapter;
                        }
                        else
                        {
                            chaptersSection = " - Chapters " + startChapter + "-" + endChapter;
                        }
                    }

                    fileName = translatedSourceName + titleSection + chaptersSection;
                }
            }

            string extension;

            // If we have a text source subtitle, force .m4v extension.
            bool allowMp4Extension = true;
            if (this.CurrentSubtitles != null)
            {
                foreach (SourceSubtitle sourceSubtitle in this.CurrentSubtitles.SourceSubtitles)
                {
                    if (sourceSubtitle.TrackNumber > 0)
                    {
                        if (this.SelectedTitle.Subtitles[sourceSubtitle.TrackNumber - 1].SubtitleType == SubtitleType.Text)
                        {
                            allowMp4Extension = false;
                        }
                    }
                }
            }

            EncodingProfile profile = this.SelectedPreset.Preset.EncodingProfile;
            if (profile.OutputFormat == OutputFormat.Mkv)
            {
                extension = ".mkv";
            }
            else if (profile.PreferredExtension == OutputExtension.Mp4 && allowMp4Extension)
            {
                extension = ".mp4";
            }
            else
            {
                extension = ".m4v";
            }

            string outputFolder = Properties.Settings.Default.AutoNameOutputFolder;
            if (!string.IsNullOrEmpty(outputFolder))
            {
                this.OutputPath = Path.Combine(outputFolder, fileName + extension);
            }
        }

        private int GetFirstUnusedAudioTrack()
        {
            List<int> unusedTracks = new List<int>();

            for (int i = 0; i < this.SelectedTitle.AudioTracks.Count; i++)
            {
                unusedTracks.Add(i);
            }

            foreach (AudioChoiceViewModel audioChoiceVM in this.AudioChoices)
            {
                if (unusedTracks.Contains(audioChoiceVM.SelectedIndex))
                {
                    unusedTracks.Remove(audioChoiceVM.SelectedIndex);
                }
            }

            if (unusedTracks.Count > 0)
            {
                return unusedTracks[0];
            }

            return 0;
        }
    }
}
