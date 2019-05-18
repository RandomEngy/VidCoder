using System;
using System.Collections.Generic;
using System.Data.SQLite;
using VidCoder.Model;

namespace VidCoder
{
	public static class Config
	{
		private static bool initialized;
		private static Dictionary<string, object> cache;
		private static Dictionary<string, object> observableCache;

		public static void EnsureInitialized(SQLiteConnection connection)
		{
			if (!initialized)
			{
				Initialize(connection);
				initialized = true;
			}
		}

		public static void Refresh(SQLiteConnection connection)
		{
			Initialize(connection);
		}

		private static void Initialize(SQLiteConnection connection)
		{
			observableCache = new Dictionary<string, object>();
			cache = new Dictionary<string, object>();
			cache.Add("MigratedConfigs", DatabaseConfig.Get("MigratedConfigs", false, connection));
			cache.Add("EncodeJobs2", DatabaseConfig.Get("EncodeJobs2", "", connection));
			cache.Add("UpdateInProgress", DatabaseConfig.Get("UpdateInProgress", false, connection));
			cache.Add("UpdateVersion", DatabaseConfig.Get("UpdateVersion", "", connection));
			cache.Add("UpdateInstallerLocation", DatabaseConfig.Get("UpdateInstallerLocation", "", connection));
			cache.Add("UpdateChangelogLocation", DatabaseConfig.Get("UpdateChangelogLocation", "", connection));
			cache.Add("LastOutputFolder", DatabaseConfig.Get("LastOutputFolder", "", connection));
			cache.Add("LastInputFileFolder", DatabaseConfig.Get("LastInputFileFolder", "", connection));
			cache.Add("LastVideoTSFolder", DatabaseConfig.Get("LastVideoTSFolder", "", connection));
			cache.Add("LastSubtitleFolder", DatabaseConfig.Get("LastSubtitleFolder", "", connection));
			cache.Add("LastCsvFolder", DatabaseConfig.Get("LastCsvFolder", "", connection));
			cache.Add("LastPresetExportFolder", DatabaseConfig.Get("LastPresetExportFolder", "", connection));
			cache.Add("AutoNameOutputFolder", DatabaseConfig.Get("AutoNameOutputFolder", "", connection));
			cache.Add("AutoNameCustomFormat", DatabaseConfig.Get("AutoNameCustomFormat", false, connection));
			cache.Add("AutoNameCustomFormatString", DatabaseConfig.Get("AutoNameCustomFormatString", "{source}-{title}", connection));
			cache.Add("NativeLanguageCode", DatabaseConfig.Get("NativeLanguageCode", "", connection));
			cache.Add("DubAudio", DatabaseConfig.Get("DubAudio", false, connection));
			cache.Add("LastPresetIndex", DatabaseConfig.Get("LastPresetIndex", -1, connection));
			cache.Add("LastPickerIndex", DatabaseConfig.Get("LastPickerIndex", 0, connection));
			cache.Add("EncodingDialogLastTab", DatabaseConfig.Get("EncodingDialogLastTab", 0, connection));
			cache.Add("OptionsDialogLastTab", DatabaseConfig.Get("OptionsDialogLastTab", 0, connection));
			cache.Add("CollapsedBuiltInFolders", DatabaseConfig.Get("CollapsedBuiltInFolders", "", connection));
			cache.Add("MainWindowPlacement", DatabaseConfig.Get("MainWindowPlacement", "", connection));
			cache.Add("EncodingDialogPlacement", DatabaseConfig.Get("EncodingDialogPlacement", "", connection));
			cache.Add("ChapterMarkersDialogPlacement", DatabaseConfig.Get("ChapterMarkersDialogPlacement", "", connection));
			cache.Add("PreviewWindowPlacement", DatabaseConfig.Get("PreviewWindowPlacement", "", connection));
			cache.Add("QueueTitlesDialogPlacement2", DatabaseConfig.Get("QueueTitlesDialogPlacement2", "", connection));
			cache.Add("AddAutoPauseProcessDialogPlacement", DatabaseConfig.Get("AddAutoPauseProcessDialogPlacement", "", connection));
			cache.Add("OptionsDialogPlacement", DatabaseConfig.Get("OptionsDialogPlacement", "", connection));
			cache.Add("EncodeDetailsWindowPlacement", DatabaseConfig.Get("EncodeDetailsWindowPlacement", "", connection));
			cache.Add("PickerWindowPlacement", DatabaseConfig.Get("PickerWindowPlacement", "", connection));
			cache.Add("LogWindowPlacement", DatabaseConfig.Get("LogWindowPlacement", "", connection));
			cache.Add("EncodingWindowOpen", DatabaseConfig.Get("EncodingWindowOpen", false, connection));
			cache.Add("PreviewWindowOpen", DatabaseConfig.Get("PreviewWindowOpen", false, connection));
			cache.Add("LogWindowOpen", DatabaseConfig.Get("LogWindowOpen", false, connection));
			cache.Add("EncodeDetailsWindowOpen", DatabaseConfig.Get("EncodeDetailsWindowOpen", false, connection));
			cache.Add("PickerWindowOpen", DatabaseConfig.Get("PickerWindowOpen", false, connection));
			cache.Add("VideoExpanded", DatabaseConfig.Get("VideoExpanded", true, connection));
			cache.Add("AudioExpanded", DatabaseConfig.Get("AudioExpanded", true, connection));
			cache.Add("SubtitlesExpanded", DatabaseConfig.Get("SubtitlesExpanded", true, connection));
			cache.Add("UpdatesEnabled", DatabaseConfig.Get("UpdatesEnabled", true, connection));
			cache.Add("UpdatePromptTiming", DatabaseConfig.Get("UpdatePromptTiming", "OnExit", connection));
			cache.Add("UpdatesDisabled32BitOSWarningDisplayed", DatabaseConfig.Get("UpdatesDisabled32BitOSWarningDisplayed", false, connection));
			cache.Add("PreviewSeconds", DatabaseConfig.Get("PreviewSeconds", 10, connection));
			cache.Add("ApplicationVersion", DatabaseConfig.Get("ApplicationVersion", "", connection));
			cache.Add("QueueColumns", DatabaseConfig.Get("QueueColumns", "Source:200|Title:35|Range:106|Destination:200", connection));
			cache.Add("QueueLastColumnWidth", DatabaseConfig.Get("QueueLastColumnWidth", 75.0, connection));
			cache.Add("CompletedColumnWidths", DatabaseConfig.Get("CompletedColumnWidths", "", connection));
			cache.Add("SourcePaneHeightStar", DatabaseConfig.Get("SourcePaneHeightStar", 2.0, connection));
			cache.Add("QueuePaneHeightStar", DatabaseConfig.Get("QueuePaneHeightStar", 1.0, connection));
			cache.Add("PickerListPaneWidth", DatabaseConfig.Get("PickerListPaneWidth", 135.0, connection));
			cache.Add("EncodingListPaneOpen", DatabaseConfig.Get("EncodingListPaneOpen", true, connection));
			cache.Add("EncodingListPaneWidth", DatabaseConfig.Get("EncodingListPaneWidth", 150.0, connection));
			cache.Add("LogListPaneWidth", DatabaseConfig.Get("LogListPaneWidth", 150.0, connection));
			cache.Add("ShowPickerWindowMessage", DatabaseConfig.Get("ShowPickerWindowMessage", true, connection));
			cache.Add("WorkerProcessPriority", DatabaseConfig.Get("WorkerProcessPriority", "BelowNormal", connection));
			cache.Add("LogVerbosity", DatabaseConfig.Get("LogVerbosity", 1, connection));
			cache.Add("CopyLogToOutputFolder", DatabaseConfig.Get("CopyLogToOutputFolder", false, connection));
			cache.Add("CopyLogToCustomFolder", DatabaseConfig.Get("CopyLogToCustomFolder", false, connection));
			cache.Add("LogCustomFolder", DatabaseConfig.Get("LogCustomFolder", "", connection));
			cache.Add("AutoPauseProcesses", DatabaseConfig.Get("AutoPauseProcesses", "", connection));
			cache.Add("MaxSimultaneousEncodes", DatabaseConfig.Get("MaxSimultaneousEncodes", 1, connection));
			cache.Add("PreviewCount", DatabaseConfig.Get("PreviewCount", 10, connection));
			cache.Add("PreviewDisplay", DatabaseConfig.Get("PreviewDisplay", "FitToWindow", connection));
			cache.Add("UseCustomPreviewFolder", DatabaseConfig.Get("UseCustomPreviewFolder", false, connection));
			cache.Add("PreviewOutputFolder", DatabaseConfig.Get("PreviewOutputFolder", "", connection));
			cache.Add("QueueTitlesUseTitleOverride", DatabaseConfig.Get("QueueTitlesUseTitleOverride", false, connection));
			cache.Add("QueueTitlesTitleOverride", DatabaseConfig.Get("QueueTitlesTitleOverride", 1, connection));
			cache.Add("ShowAudioTrackNameField", DatabaseConfig.Get("ShowAudioTrackNameField", false, connection));
			cache.Add("SourceHistory", DatabaseConfig.Get("SourceHistory", "", connection));
			cache.Add("MinimizeToTray", DatabaseConfig.Get("MinimizeToTray", false, connection));
			cache.Add("OutputToSourceDirectory", DatabaseConfig.Get("OutputToSourceDirectory", false, connection));
			cache.Add("PreserveFolderStructureInBatch", DatabaseConfig.Get("PreserveFolderStructureInBatch", false, connection));
			cache.Add("WhenFileExists", DatabaseConfig.Get("WhenFileExists", "Prompt", connection));
			cache.Add("WhenFileExistsBatch", DatabaseConfig.Get("WhenFileExistsBatch", "AutoRename", connection));
			cache.Add("UseCustomVideoPlayer", DatabaseConfig.Get("UseCustomVideoPlayer", false, connection));
			cache.Add("CustomVideoPlayer", DatabaseConfig.Get("CustomVideoPlayer", "", connection));
			cache.Add("PlaySoundOnCompletion", DatabaseConfig.Get("PlaySoundOnCompletion", false, connection));
			cache.Add("UseCustomCompletionSound", DatabaseConfig.Get("UseCustomCompletionSound", false, connection));
			cache.Add("CustomCompletionSound", DatabaseConfig.Get("CustomCompletionSound", "", connection));
			cache.Add("LastPlayer", DatabaseConfig.Get("LastPlayer", "vlc", connection));
			cache.Add("QueueTitlesUseDirectoryOverride", DatabaseConfig.Get("QueueTitlesUseDirectoryOverride", false, connection));
			cache.Add("QueueTitlesDirectoryOverride", DatabaseConfig.Get("QueueTitlesDirectoryOverride", "", connection));
			cache.Add("QueueTitlesUseNameOverride", DatabaseConfig.Get("QueueTitlesUseNameOverride", false, connection));
			cache.Add("QueueTitlesNameOverride", DatabaseConfig.Get("QueueTitlesNameOverride", "", connection));
			cache.Add("DxvaDecoding", DatabaseConfig.Get("DxvaDecoding", false, connection));
			cache.Add("EnableLibDvdNav", DatabaseConfig.Get("EnableLibDvdNav", true, connection));
			cache.Add("MinimumTitleLengthSeconds", DatabaseConfig.Get("MinimumTitleLengthSeconds", 10, connection));
			cache.Add("DeleteSourceFilesOnClearingCompleted", DatabaseConfig.Get("DeleteSourceFilesOnClearingCompleted", false, connection));
			cache.Add("PreserveModifyTimeFiles", DatabaseConfig.Get("PreserveModifyTimeFiles", false, connection));
			cache.Add("ResumeEncodingOnRestart", DatabaseConfig.Get("ResumeEncodingOnRestart", false, connection));
			cache.Add("KeepFailedFiles", DatabaseConfig.Get("KeepFailedFiles", false, connection));
			cache.Add("UseWorkerProcess", DatabaseConfig.Get("UseWorkerProcess", true, connection));
			cache.Add("RememberPreviousFiles", DatabaseConfig.Get("RememberPreviousFiles", true, connection));
			cache.Add("VideoFileExtensions", DatabaseConfig.Get("VideoFileExtensions", "avi, mkv, mp4, m4v, mpg, mpeg, mov, wmv", connection));
			cache.Add("PreferredPlayer", DatabaseConfig.Get("PreferredPlayer", "vlc", connection));
			cache.Add("BetaUpdates", DatabaseConfig.Get("BetaUpdates", false, connection));
			cache.Add("InterfaceLanguageCode", DatabaseConfig.Get("InterfaceLanguageCode", "", connection));
			cache.Add("AppTheme", DatabaseConfig.Get("AppTheme", "Auto", connection));
			cache.Add("CpuThrottlingFraction", DatabaseConfig.Get("CpuThrottlingFraction", 1.0, connection));
			cache.Add("UseBuiltInPlayerForPreviews", DatabaseConfig.Get("UseBuiltInPlayerForPreviews", true, connection));
			cache.Add("PreviewVolume", DatabaseConfig.Get("PreviewVolume", 0.5, connection));
			cache.Add("TriggerEncodeCompleteActionWithErrors", DatabaseConfig.Get("TriggerEncodeCompleteActionWithErrors", true, connection));
			cache.Add("AudioLanguageCode", DatabaseConfig.Get("AudioLanguageCode", "und", connection));
			cache.Add("SubtitleLanguageCode", DatabaseConfig.Get("SubtitleLanguageCode", "und", connection));
			cache.Add("AutoAudio", DatabaseConfig.Get("AutoAudio", "Disabled", connection));
			cache.Add("AutoSubtitle", DatabaseConfig.Get("AutoSubtitle", "Disabled", connection));
			cache.Add("AutoAudioAll", DatabaseConfig.Get("AutoAudioAll", false, connection));
			cache.Add("AutoSubtitleAll", DatabaseConfig.Get("AutoSubtitleAll", false, connection));
			cache.Add("AutoSubtitleOnlyIfDifferent", DatabaseConfig.Get("AutoSubtitleOnlyIfDifferent", true, connection));
			cache.Add("AutoSubtitleBurnIn", DatabaseConfig.Get("AutoSubtitleBurnIn", true, connection));
			cache.Add("AutoSubtitleLanguageDefault", DatabaseConfig.Get("AutoSubtitleLanguageDefault", false, connection));
			cache.Add("AutoSubtitleLanguageBurnIn", DatabaseConfig.Get("AutoSubtitleLanguageBurnIn", false, connection));
			cache.Add("QueueTitlesStartTime", DatabaseConfig.Get("QueueTitlesStartTime", 40, connection));
			cache.Add("QueueTitlesEndTime", DatabaseConfig.Get("QueueTitlesEndTime", 45, connection));
			cache.Add("QueueTitlesUseRange", DatabaseConfig.Get("QueueTitlesUseRange", false, connection));
		}

		public static T Get<T>(string key)
		{
			return (T)cache[key];
		}

		public static void Set<T>(string key, T value)
		{
			cache[key] = value;
			DatabaseConfig.Set(key, value);

			NotifyObservable(key, value);
		}

		public static void SetLegacy<T>(string key, T value)
		{
			cache[key] = value;
			DatabaseConfig.SetLegacy(key, value);

			NotifyObservable(key, value);
		}

		private static void NotifyObservable<T>(string key, T value)
		{
			object observableObject;
			if (observableCache.TryGetValue(key, out observableObject))
			{
				var observable = (ConfigObservable<T>)observableObject;
				observable.OnNext(value);
			}
		}

		public static bool MigratedConfigs
		{
			get { return (bool)cache["MigratedConfigs"]; }
			set { Set("MigratedConfigs", value); }
		}
		public static string EncodeJobs2
		{
			get { return (string)cache["EncodeJobs2"]; }
			set { Set("EncodeJobs2", value); }
		}
		public static bool UpdateInProgress
		{
			get { return (bool)cache["UpdateInProgress"]; }
			set { Set("UpdateInProgress", value); }
		}
		public static string UpdateVersion
		{
			get { return (string)cache["UpdateVersion"]; }
			set { Set("UpdateVersion", value); }
		}
		public static string UpdateInstallerLocation
		{
			get { return (string)cache["UpdateInstallerLocation"]; }
			set { Set("UpdateInstallerLocation", value); }
		}
		public static string UpdateChangelogLocation
		{
			get { return (string)cache["UpdateChangelogLocation"]; }
			set { Set("UpdateChangelogLocation", value); }
		}
		public static string LastOutputFolder
		{
			get { return (string)cache["LastOutputFolder"]; }
			set { Set("LastOutputFolder", value); }
		}
		public static string LastInputFileFolder
		{
			get { return (string)cache["LastInputFileFolder"]; }
			set { Set("LastInputFileFolder", value); }
		}
		public static string LastVideoTSFolder
		{
			get { return (string)cache["LastVideoTSFolder"]; }
			set { Set("LastVideoTSFolder", value); }
		}
		public static string LastSubtitleFolder
		{
			get { return (string)cache["LastSubtitleFolder"]; }
			set { Set("LastSubtitleFolder", value); }
		}
		public static string LastCsvFolder
		{
			get { return (string)cache["LastCsvFolder"]; }
			set { Set("LastCsvFolder", value); }
		}
		public static string LastPresetExportFolder
		{
			get { return (string)cache["LastPresetExportFolder"]; }
			set { Set("LastPresetExportFolder", value); }
		}
		public static string AutoNameOutputFolder
		{
			get { return (string)cache["AutoNameOutputFolder"]; }
			set { Set("AutoNameOutputFolder", value); }
		}
		public static bool AutoNameCustomFormat
		{
			get { return (bool)cache["AutoNameCustomFormat"]; }
			set { Set("AutoNameCustomFormat", value); }
		}
		public static string AutoNameCustomFormatString
		{
			get { return (string)cache["AutoNameCustomFormatString"]; }
			set { Set("AutoNameCustomFormatString", value); }
		}
		public static string NativeLanguageCode
		{
			get { return (string)cache["NativeLanguageCode"]; }
			set { Set("NativeLanguageCode", value); }
		}
		public static bool DubAudio
		{
			get { return (bool)cache["DubAudio"]; }
			set { Set("DubAudio", value); }
		}
		public static int LastPresetIndex
		{
			get { return (int)cache["LastPresetIndex"]; }
			set { Set("LastPresetIndex", value); }
		}
		public static int LastPickerIndex
		{
			get { return (int)cache["LastPickerIndex"]; }
			set { Set("LastPickerIndex", value); }
		}
		public static int EncodingDialogLastTab
		{
			get { return (int)cache["EncodingDialogLastTab"]; }
			set { Set("EncodingDialogLastTab", value); }
		}
		public static int OptionsDialogLastTab
		{
			get { return (int)cache["OptionsDialogLastTab"]; }
			set { Set("OptionsDialogLastTab", value); }
		}
		public static string CollapsedBuiltInFolders
		{
			get { return (string)cache["CollapsedBuiltInFolders"]; }
			set { Set("CollapsedBuiltInFolders", value); }
		}
		public static string MainWindowPlacement
		{
			get { return (string)cache["MainWindowPlacement"]; }
			set { Set("MainWindowPlacement", value); }
		}
		public static string EncodingDialogPlacement
		{
			get { return (string)cache["EncodingDialogPlacement"]; }
			set { Set("EncodingDialogPlacement", value); }
		}
		public static string ChapterMarkersDialogPlacement
		{
			get { return (string)cache["ChapterMarkersDialogPlacement"]; }
			set { Set("ChapterMarkersDialogPlacement", value); }
		}
		public static string PreviewWindowPlacement
		{
			get { return (string)cache["PreviewWindowPlacement"]; }
			set { Set("PreviewWindowPlacement", value); }
		}
		public static string QueueTitlesDialogPlacement2
		{
			get { return (string)cache["QueueTitlesDialogPlacement2"]; }
			set { Set("QueueTitlesDialogPlacement2", value); }
		}
		public static string AddAutoPauseProcessDialogPlacement
		{
			get { return (string)cache["AddAutoPauseProcessDialogPlacement"]; }
			set { Set("AddAutoPauseProcessDialogPlacement", value); }
		}
		public static string OptionsDialogPlacement
		{
			get { return (string)cache["OptionsDialogPlacement"]; }
			set { Set("OptionsDialogPlacement", value); }
		}
		public static string EncodeDetailsWindowPlacement
		{
			get { return (string)cache["EncodeDetailsWindowPlacement"]; }
			set { Set("EncodeDetailsWindowPlacement", value); }
		}
		public static string PickerWindowPlacement
		{
			get { return (string)cache["PickerWindowPlacement"]; }
			set { Set("PickerWindowPlacement", value); }
		}
		public static string LogWindowPlacement
		{
			get { return (string)cache["LogWindowPlacement"]; }
			set { Set("LogWindowPlacement", value); }
		}
		public static bool EncodingWindowOpen
		{
			get { return (bool)cache["EncodingWindowOpen"]; }
			set { Set("EncodingWindowOpen", value); }
		}
		public static bool PreviewWindowOpen
		{
			get { return (bool)cache["PreviewWindowOpen"]; }
			set { Set("PreviewWindowOpen", value); }
		}
		public static bool LogWindowOpen
		{
			get { return (bool)cache["LogWindowOpen"]; }
			set { Set("LogWindowOpen", value); }
		}
		public static bool EncodeDetailsWindowOpen
		{
			get { return (bool)cache["EncodeDetailsWindowOpen"]; }
			set { Set("EncodeDetailsWindowOpen", value); }
		}
		public static bool PickerWindowOpen
		{
			get { return (bool)cache["PickerWindowOpen"]; }
			set { Set("PickerWindowOpen", value); }
		}
		public static bool VideoExpanded
		{
			get { return (bool)cache["VideoExpanded"]; }
			set { Set("VideoExpanded", value); }
		}
		public static bool AudioExpanded
		{
			get { return (bool)cache["AudioExpanded"]; }
			set { Set("AudioExpanded", value); }
		}
		public static bool SubtitlesExpanded
		{
			get { return (bool)cache["SubtitlesExpanded"]; }
			set { Set("SubtitlesExpanded", value); }
		}
		public static bool UpdatesEnabled
		{
			get { return (bool)cache["UpdatesEnabled"]; }
			set { Set("UpdatesEnabled", value); }
		}
		public static string UpdatePromptTiming
		{
			get { return (string)cache["UpdatePromptTiming"]; }
			set { Set("UpdatePromptTiming", value); }
		}
		public static bool UpdatesDisabled32BitOSWarningDisplayed
		{
			get { return (bool)cache["UpdatesDisabled32BitOSWarningDisplayed"]; }
			set { Set("UpdatesDisabled32BitOSWarningDisplayed", value); }
		}
		public static int PreviewSeconds
		{
			get { return (int)cache["PreviewSeconds"]; }
			set { Set("PreviewSeconds", value); }
		}
		public static string ApplicationVersion
		{
			get { return (string)cache["ApplicationVersion"]; }
			set { Set("ApplicationVersion", value); }
		}
		public static string QueueColumns
		{
			get { return (string)cache["QueueColumns"]; }
			set { Set("QueueColumns", value); }
		}
		public static double QueueLastColumnWidth
		{
			get { return (double)cache["QueueLastColumnWidth"]; }
			set { Set("QueueLastColumnWidth", value); }
		}
		public static string CompletedColumnWidths
		{
			get { return (string)cache["CompletedColumnWidths"]; }
			set { Set("CompletedColumnWidths", value); }
		}
		public static double SourcePaneHeightStar
		{
			get { return (double)cache["SourcePaneHeightStar"]; }
			set { Set("SourcePaneHeightStar", value); }
		}
		public static double QueuePaneHeightStar
		{
			get { return (double)cache["QueuePaneHeightStar"]; }
			set { Set("QueuePaneHeightStar", value); }
		}
		public static double PickerListPaneWidth
		{
			get { return (double)cache["PickerListPaneWidth"]; }
			set { Set("PickerListPaneWidth", value); }
		}
		public static bool EncodingListPaneOpen
		{
			get { return (bool)cache["EncodingListPaneOpen"]; }
			set { Set("EncodingListPaneOpen", value); }
		}
		public static double EncodingListPaneWidth
		{
			get { return (double)cache["EncodingListPaneWidth"]; }
			set { Set("EncodingListPaneWidth", value); }
		}
		public static double LogListPaneWidth
		{
			get { return (double)cache["LogListPaneWidth"]; }
			set { Set("LogListPaneWidth", value); }
		}
		public static bool ShowPickerWindowMessage
		{
			get { return (bool)cache["ShowPickerWindowMessage"]; }
			set { Set("ShowPickerWindowMessage", value); }
		}
		public static string WorkerProcessPriority
		{
			get { return (string)cache["WorkerProcessPriority"]; }
			set { Set("WorkerProcessPriority", value); }
		}
		public static int LogVerbosity
		{
			get { return (int)cache["LogVerbosity"]; }
			set { Set("LogVerbosity", value); }
		}
		public static bool CopyLogToOutputFolder
		{
			get { return (bool)cache["CopyLogToOutputFolder"]; }
			set { Set("CopyLogToOutputFolder", value); }
		}
		public static bool CopyLogToCustomFolder
		{
			get { return (bool)cache["CopyLogToCustomFolder"]; }
			set { Set("CopyLogToCustomFolder", value); }
		}
		public static string LogCustomFolder
		{
			get { return (string)cache["LogCustomFolder"]; }
			set { Set("LogCustomFolder", value); }
		}
		public static string AutoPauseProcesses
		{
			get { return (string)cache["AutoPauseProcesses"]; }
			set { Set("AutoPauseProcesses", value); }
		}
		public static int MaxSimultaneousEncodes
		{
			get { return (int)cache["MaxSimultaneousEncodes"]; }
			set { Set("MaxSimultaneousEncodes", value); }
		}
		public static int PreviewCount
		{
			get { return (int)cache["PreviewCount"]; }
			set { Set("PreviewCount", value); }
		}
		public static string PreviewDisplay
		{
			get { return (string)cache["PreviewDisplay"]; }
			set { Set("PreviewDisplay", value); }
		}
		public static bool UseCustomPreviewFolder
		{
			get { return (bool)cache["UseCustomPreviewFolder"]; }
			set { Set("UseCustomPreviewFolder", value); }
		}
		public static string PreviewOutputFolder
		{
			get { return (string)cache["PreviewOutputFolder"]; }
			set { Set("PreviewOutputFolder", value); }
		}
		public static bool QueueTitlesUseTitleOverride
		{
			get { return (bool)cache["QueueTitlesUseTitleOverride"]; }
			set { Set("QueueTitlesUseTitleOverride", value); }
		}
		public static int QueueTitlesTitleOverride
		{
			get { return (int)cache["QueueTitlesTitleOverride"]; }
			set { Set("QueueTitlesTitleOverride", value); }
		}
		public static bool ShowAudioTrackNameField
		{
			get { return (bool)cache["ShowAudioTrackNameField"]; }
			set { Set("ShowAudioTrackNameField", value); }
		}
		public static string SourceHistory
		{
			get { return (string)cache["SourceHistory"]; }
			set { Set("SourceHistory", value); }
		}
		public static bool MinimizeToTray
		{
			get { return (bool)cache["MinimizeToTray"]; }
			set { Set("MinimizeToTray", value); }
		}
		public static bool OutputToSourceDirectory
		{
			get { return (bool)cache["OutputToSourceDirectory"]; }
			set { Set("OutputToSourceDirectory", value); }
		}
		public static bool PreserveFolderStructureInBatch
		{
			get { return (bool)cache["PreserveFolderStructureInBatch"]; }
			set { Set("PreserveFolderStructureInBatch", value); }
		}
		public static string WhenFileExists
		{
			get { return (string)cache["WhenFileExists"]; }
			set { Set("WhenFileExists", value); }
		}
		public static string WhenFileExistsBatch
		{
			get { return (string)cache["WhenFileExistsBatch"]; }
			set { Set("WhenFileExistsBatch", value); }
		}
		public static bool UseCustomVideoPlayer
		{
			get { return (bool)cache["UseCustomVideoPlayer"]; }
			set { Set("UseCustomVideoPlayer", value); }
		}
		public static string CustomVideoPlayer
		{
			get { return (string)cache["CustomVideoPlayer"]; }
			set { Set("CustomVideoPlayer", value); }
		}
		public static bool PlaySoundOnCompletion
		{
			get { return (bool)cache["PlaySoundOnCompletion"]; }
			set { Set("PlaySoundOnCompletion", value); }
		}
		public static bool UseCustomCompletionSound
		{
			get { return (bool)cache["UseCustomCompletionSound"]; }
			set { Set("UseCustomCompletionSound", value); }
		}
		public static string CustomCompletionSound
		{
			get { return (string)cache["CustomCompletionSound"]; }
			set { Set("CustomCompletionSound", value); }
		}
		public static string LastPlayer
		{
			get { return (string)cache["LastPlayer"]; }
			set { Set("LastPlayer", value); }
		}
		public static bool QueueTitlesUseDirectoryOverride
		{
			get { return (bool)cache["QueueTitlesUseDirectoryOverride"]; }
			set { Set("QueueTitlesUseDirectoryOverride", value); }
		}
		public static string QueueTitlesDirectoryOverride
		{
			get { return (string)cache["QueueTitlesDirectoryOverride"]; }
			set { Set("QueueTitlesDirectoryOverride", value); }
		}
		public static bool QueueTitlesUseNameOverride
		{
			get { return (bool)cache["QueueTitlesUseNameOverride"]; }
			set { Set("QueueTitlesUseNameOverride", value); }
		}
		public static string QueueTitlesNameOverride
		{
			get { return (string)cache["QueueTitlesNameOverride"]; }
			set { Set("QueueTitlesNameOverride", value); }
		}
		public static bool DxvaDecoding
		{
			get { return (bool)cache["DxvaDecoding"]; }
			set { Set("DxvaDecoding", value); }
		}
		public static bool EnableLibDvdNav
		{
			get { return (bool)cache["EnableLibDvdNav"]; }
			set { Set("EnableLibDvdNav", value); }
		}
		public static int MinimumTitleLengthSeconds
		{
			get { return (int)cache["MinimumTitleLengthSeconds"]; }
			set { Set("MinimumTitleLengthSeconds", value); }
		}
		public static bool DeleteSourceFilesOnClearingCompleted
		{
			get { return (bool)cache["DeleteSourceFilesOnClearingCompleted"]; }
			set { Set("DeleteSourceFilesOnClearingCompleted", value); }
		}
		public static bool PreserveModifyTimeFiles
		{
			get { return (bool)cache["PreserveModifyTimeFiles"]; }
			set { Set("PreserveModifyTimeFiles", value); }
		}
		public static bool ResumeEncodingOnRestart
		{
			get { return (bool)cache["ResumeEncodingOnRestart"]; }
			set { Set("ResumeEncodingOnRestart", value); }
		}
		public static bool KeepFailedFiles
		{
			get { return (bool)cache["KeepFailedFiles"]; }
			set { Set("KeepFailedFiles", value); }
		}
		public static bool UseWorkerProcess
		{
			get { return (bool)cache["UseWorkerProcess"]; }
			set { Set("UseWorkerProcess", value); }
		}
		public static bool RememberPreviousFiles
		{
			get { return (bool)cache["RememberPreviousFiles"]; }
			set { Set("RememberPreviousFiles", value); }
		}
		public static string VideoFileExtensions
		{
			get { return (string)cache["VideoFileExtensions"]; }
			set { Set("VideoFileExtensions", value); }
		}
		public static string PreferredPlayer
		{
			get { return (string)cache["PreferredPlayer"]; }
			set { Set("PreferredPlayer", value); }
		}
		public static bool BetaUpdates
		{
			get { return (bool)cache["BetaUpdates"]; }
			set { Set("BetaUpdates", value); }
		}
		public static string InterfaceLanguageCode
		{
			get { return (string)cache["InterfaceLanguageCode"]; }
			set { Set("InterfaceLanguageCode", value); }
		}
		public static string AppTheme
		{
			get { return (string)cache["AppTheme"]; }
			set { Set("AppTheme", value); }
		}
		public static double CpuThrottlingFraction
		{
			get { return (double)cache["CpuThrottlingFraction"]; }
			set { Set("CpuThrottlingFraction", value); }
		}
		public static bool UseBuiltInPlayerForPreviews
		{
			get { return (bool)cache["UseBuiltInPlayerForPreviews"]; }
			set { Set("UseBuiltInPlayerForPreviews", value); }
		}
		public static double PreviewVolume
		{
			get { return (double)cache["PreviewVolume"]; }
			set { Set("PreviewVolume", value); }
		}
		public static bool TriggerEncodeCompleteActionWithErrors
		{
			get { return (bool)cache["TriggerEncodeCompleteActionWithErrors"]; }
			set { Set("TriggerEncodeCompleteActionWithErrors", value); }
		}
		public static string AudioLanguageCode
		{
			get { return (string)cache["AudioLanguageCode"]; }
			set { Set("AudioLanguageCode", value); }
		}
		public static string SubtitleLanguageCode
		{
			get { return (string)cache["SubtitleLanguageCode"]; }
			set { Set("SubtitleLanguageCode", value); }
		}
		public static string AutoAudio
		{
			get { return (string)cache["AutoAudio"]; }
			set { Set("AutoAudio", value); }
		}
		public static string AutoSubtitle
		{
			get { return (string)cache["AutoSubtitle"]; }
			set { Set("AutoSubtitle", value); }
		}
		public static bool AutoAudioAll
		{
			get { return (bool)cache["AutoAudioAll"]; }
			set { Set("AutoAudioAll", value); }
		}
		public static bool AutoSubtitleAll
		{
			get { return (bool)cache["AutoSubtitleAll"]; }
			set { Set("AutoSubtitleAll", value); }
		}
		public static bool AutoSubtitleOnlyIfDifferent
		{
			get { return (bool)cache["AutoSubtitleOnlyIfDifferent"]; }
			set { Set("AutoSubtitleOnlyIfDifferent", value); }
		}
		public static bool AutoSubtitleBurnIn
		{
			get { return (bool)cache["AutoSubtitleBurnIn"]; }
			set { Set("AutoSubtitleBurnIn", value); }
		}
		public static bool AutoSubtitleLanguageDefault
		{
			get { return (bool)cache["AutoSubtitleLanguageDefault"]; }
			set { Set("AutoSubtitleLanguageDefault", value); }
		}
		public static bool AutoSubtitleLanguageBurnIn
		{
			get { return (bool)cache["AutoSubtitleLanguageBurnIn"]; }
			set { Set("AutoSubtitleLanguageBurnIn", value); }
		}
		public static int QueueTitlesStartTime
		{
			get { return (int)cache["QueueTitlesStartTime"]; }
			set { Set("QueueTitlesStartTime", value); }
		}
		public static int QueueTitlesEndTime
		{
			get { return (int)cache["QueueTitlesEndTime"]; }
			set { Set("QueueTitlesEndTime", value); }
		}
		public static bool QueueTitlesUseRange
		{
			get { return (bool)cache["QueueTitlesUseRange"]; }
			set { Set("QueueTitlesUseRange", value); }
		}
		public static class Observables
		{
			public static IObservable<bool> MigratedConfigs => GetObservable<bool>("MigratedConfigs");
			public static IObservable<string> EncodeJobs2 => GetObservable<string>("EncodeJobs2");
			public static IObservable<bool> UpdateInProgress => GetObservable<bool>("UpdateInProgress");
			public static IObservable<string> UpdateVersion => GetObservable<string>("UpdateVersion");
			public static IObservable<string> UpdateInstallerLocation => GetObservable<string>("UpdateInstallerLocation");
			public static IObservable<string> UpdateChangelogLocation => GetObservable<string>("UpdateChangelogLocation");
			public static IObservable<string> LastOutputFolder => GetObservable<string>("LastOutputFolder");
			public static IObservable<string> LastInputFileFolder => GetObservable<string>("LastInputFileFolder");
			public static IObservable<string> LastVideoTSFolder => GetObservable<string>("LastVideoTSFolder");
			public static IObservable<string> LastSubtitleFolder => GetObservable<string>("LastSubtitleFolder");
			public static IObservable<string> LastCsvFolder => GetObservable<string>("LastCsvFolder");
			public static IObservable<string> LastPresetExportFolder => GetObservable<string>("LastPresetExportFolder");
			public static IObservable<string> AutoNameOutputFolder => GetObservable<string>("AutoNameOutputFolder");
			public static IObservable<bool> AutoNameCustomFormat => GetObservable<bool>("AutoNameCustomFormat");
			public static IObservable<string> AutoNameCustomFormatString => GetObservable<string>("AutoNameCustomFormatString");
			public static IObservable<string> NativeLanguageCode => GetObservable<string>("NativeLanguageCode");
			public static IObservable<bool> DubAudio => GetObservable<bool>("DubAudio");
			public static IObservable<int> LastPresetIndex => GetObservable<int>("LastPresetIndex");
			public static IObservable<int> LastPickerIndex => GetObservable<int>("LastPickerIndex");
			public static IObservable<int> EncodingDialogLastTab => GetObservable<int>("EncodingDialogLastTab");
			public static IObservable<int> OptionsDialogLastTab => GetObservable<int>("OptionsDialogLastTab");
			public static IObservable<string> CollapsedBuiltInFolders => GetObservable<string>("CollapsedBuiltInFolders");
			public static IObservable<string> MainWindowPlacement => GetObservable<string>("MainWindowPlacement");
			public static IObservable<string> EncodingDialogPlacement => GetObservable<string>("EncodingDialogPlacement");
			public static IObservable<string> ChapterMarkersDialogPlacement => GetObservable<string>("ChapterMarkersDialogPlacement");
			public static IObservable<string> PreviewWindowPlacement => GetObservable<string>("PreviewWindowPlacement");
			public static IObservable<string> QueueTitlesDialogPlacement2 => GetObservable<string>("QueueTitlesDialogPlacement2");
			public static IObservable<string> AddAutoPauseProcessDialogPlacement => GetObservable<string>("AddAutoPauseProcessDialogPlacement");
			public static IObservable<string> OptionsDialogPlacement => GetObservable<string>("OptionsDialogPlacement");
			public static IObservable<string> EncodeDetailsWindowPlacement => GetObservable<string>("EncodeDetailsWindowPlacement");
			public static IObservable<string> PickerWindowPlacement => GetObservable<string>("PickerWindowPlacement");
			public static IObservable<string> LogWindowPlacement => GetObservable<string>("LogWindowPlacement");
			public static IObservable<bool> EncodingWindowOpen => GetObservable<bool>("EncodingWindowOpen");
			public static IObservable<bool> PreviewWindowOpen => GetObservable<bool>("PreviewWindowOpen");
			public static IObservable<bool> LogWindowOpen => GetObservable<bool>("LogWindowOpen");
			public static IObservable<bool> EncodeDetailsWindowOpen => GetObservable<bool>("EncodeDetailsWindowOpen");
			public static IObservable<bool> PickerWindowOpen => GetObservable<bool>("PickerWindowOpen");
			public static IObservable<bool> VideoExpanded => GetObservable<bool>("VideoExpanded");
			public static IObservable<bool> AudioExpanded => GetObservable<bool>("AudioExpanded");
			public static IObservable<bool> SubtitlesExpanded => GetObservable<bool>("SubtitlesExpanded");
			public static IObservable<bool> UpdatesEnabled => GetObservable<bool>("UpdatesEnabled");
			public static IObservable<string> UpdatePromptTiming => GetObservable<string>("UpdatePromptTiming");
			public static IObservable<bool> UpdatesDisabled32BitOSWarningDisplayed => GetObservable<bool>("UpdatesDisabled32BitOSWarningDisplayed");
			public static IObservable<int> PreviewSeconds => GetObservable<int>("PreviewSeconds");
			public static IObservable<string> ApplicationVersion => GetObservable<string>("ApplicationVersion");
			public static IObservable<string> QueueColumns => GetObservable<string>("QueueColumns");
			public static IObservable<double> QueueLastColumnWidth => GetObservable<double>("QueueLastColumnWidth");
			public static IObservable<string> CompletedColumnWidths => GetObservable<string>("CompletedColumnWidths");
			public static IObservable<double> SourcePaneHeightStar => GetObservable<double>("SourcePaneHeightStar");
			public static IObservable<double> QueuePaneHeightStar => GetObservable<double>("QueuePaneHeightStar");
			public static IObservable<double> PickerListPaneWidth => GetObservable<double>("PickerListPaneWidth");
			public static IObservable<bool> EncodingListPaneOpen => GetObservable<bool>("EncodingListPaneOpen");
			public static IObservable<double> EncodingListPaneWidth => GetObservable<double>("EncodingListPaneWidth");
			public static IObservable<double> LogListPaneWidth => GetObservable<double>("LogListPaneWidth");
			public static IObservable<bool> ShowPickerWindowMessage => GetObservable<bool>("ShowPickerWindowMessage");
			public static IObservable<string> WorkerProcessPriority => GetObservable<string>("WorkerProcessPriority");
			public static IObservable<int> LogVerbosity => GetObservable<int>("LogVerbosity");
			public static IObservable<bool> CopyLogToOutputFolder => GetObservable<bool>("CopyLogToOutputFolder");
			public static IObservable<bool> CopyLogToCustomFolder => GetObservable<bool>("CopyLogToCustomFolder");
			public static IObservable<string> LogCustomFolder => GetObservable<string>("LogCustomFolder");
			public static IObservable<string> AutoPauseProcesses => GetObservable<string>("AutoPauseProcesses");
			public static IObservable<int> MaxSimultaneousEncodes => GetObservable<int>("MaxSimultaneousEncodes");
			public static IObservable<int> PreviewCount => GetObservable<int>("PreviewCount");
			public static IObservable<string> PreviewDisplay => GetObservable<string>("PreviewDisplay");
			public static IObservable<bool> UseCustomPreviewFolder => GetObservable<bool>("UseCustomPreviewFolder");
			public static IObservable<string> PreviewOutputFolder => GetObservable<string>("PreviewOutputFolder");
			public static IObservable<bool> QueueTitlesUseTitleOverride => GetObservable<bool>("QueueTitlesUseTitleOverride");
			public static IObservable<int> QueueTitlesTitleOverride => GetObservable<int>("QueueTitlesTitleOverride");
			public static IObservable<bool> ShowAudioTrackNameField => GetObservable<bool>("ShowAudioTrackNameField");
			public static IObservable<string> SourceHistory => GetObservable<string>("SourceHistory");
			public static IObservable<bool> MinimizeToTray => GetObservable<bool>("MinimizeToTray");
			public static IObservable<bool> OutputToSourceDirectory => GetObservable<bool>("OutputToSourceDirectory");
			public static IObservable<bool> PreserveFolderStructureInBatch => GetObservable<bool>("PreserveFolderStructureInBatch");
			public static IObservable<string> WhenFileExists => GetObservable<string>("WhenFileExists");
			public static IObservable<string> WhenFileExistsBatch => GetObservable<string>("WhenFileExistsBatch");
			public static IObservable<bool> UseCustomVideoPlayer => GetObservable<bool>("UseCustomVideoPlayer");
			public static IObservable<string> CustomVideoPlayer => GetObservable<string>("CustomVideoPlayer");
			public static IObservable<bool> PlaySoundOnCompletion => GetObservable<bool>("PlaySoundOnCompletion");
			public static IObservable<bool> UseCustomCompletionSound => GetObservable<bool>("UseCustomCompletionSound");
			public static IObservable<string> CustomCompletionSound => GetObservable<string>("CustomCompletionSound");
			public static IObservable<string> LastPlayer => GetObservable<string>("LastPlayer");
			public static IObservable<bool> QueueTitlesUseDirectoryOverride => GetObservable<bool>("QueueTitlesUseDirectoryOverride");
			public static IObservable<string> QueueTitlesDirectoryOverride => GetObservable<string>("QueueTitlesDirectoryOverride");
			public static IObservable<bool> QueueTitlesUseNameOverride => GetObservable<bool>("QueueTitlesUseNameOverride");
			public static IObservable<string> QueueTitlesNameOverride => GetObservable<string>("QueueTitlesNameOverride");
			public static IObservable<bool> DxvaDecoding => GetObservable<bool>("DxvaDecoding");
			public static IObservable<bool> EnableLibDvdNav => GetObservable<bool>("EnableLibDvdNav");
			public static IObservable<int> MinimumTitleLengthSeconds => GetObservable<int>("MinimumTitleLengthSeconds");
			public static IObservable<bool> DeleteSourceFilesOnClearingCompleted => GetObservable<bool>("DeleteSourceFilesOnClearingCompleted");
			public static IObservable<bool> PreserveModifyTimeFiles => GetObservable<bool>("PreserveModifyTimeFiles");
			public static IObservable<bool> ResumeEncodingOnRestart => GetObservable<bool>("ResumeEncodingOnRestart");
			public static IObservable<bool> KeepFailedFiles => GetObservable<bool>("KeepFailedFiles");
			public static IObservable<bool> UseWorkerProcess => GetObservable<bool>("UseWorkerProcess");
			public static IObservable<bool> RememberPreviousFiles => GetObservable<bool>("RememberPreviousFiles");
			public static IObservable<string> VideoFileExtensions => GetObservable<string>("VideoFileExtensions");
			public static IObservable<string> PreferredPlayer => GetObservable<string>("PreferredPlayer");
			public static IObservable<bool> BetaUpdates => GetObservable<bool>("BetaUpdates");
			public static IObservable<string> InterfaceLanguageCode => GetObservable<string>("InterfaceLanguageCode");
			public static IObservable<string> AppTheme => GetObservable<string>("AppTheme");
			public static IObservable<double> CpuThrottlingFraction => GetObservable<double>("CpuThrottlingFraction");
			public static IObservable<bool> UseBuiltInPlayerForPreviews => GetObservable<bool>("UseBuiltInPlayerForPreviews");
			public static IObservable<double> PreviewVolume => GetObservable<double>("PreviewVolume");
			public static IObservable<bool> TriggerEncodeCompleteActionWithErrors => GetObservable<bool>("TriggerEncodeCompleteActionWithErrors");
			public static IObservable<string> AudioLanguageCode => GetObservable<string>("AudioLanguageCode");
			public static IObservable<string> SubtitleLanguageCode => GetObservable<string>("SubtitleLanguageCode");
			public static IObservable<string> AutoAudio => GetObservable<string>("AutoAudio");
			public static IObservable<string> AutoSubtitle => GetObservable<string>("AutoSubtitle");
			public static IObservable<bool> AutoAudioAll => GetObservable<bool>("AutoAudioAll");
			public static IObservable<bool> AutoSubtitleAll => GetObservable<bool>("AutoSubtitleAll");
			public static IObservable<bool> AutoSubtitleOnlyIfDifferent => GetObservable<bool>("AutoSubtitleOnlyIfDifferent");
			public static IObservable<bool> AutoSubtitleBurnIn => GetObservable<bool>("AutoSubtitleBurnIn");
			public static IObservable<bool> AutoSubtitleLanguageDefault => GetObservable<bool>("AutoSubtitleLanguageDefault");
			public static IObservable<bool> AutoSubtitleLanguageBurnIn => GetObservable<bool>("AutoSubtitleLanguageBurnIn");
			public static IObservable<int> QueueTitlesStartTime => GetObservable<int>("QueueTitlesStartTime");
			public static IObservable<int> QueueTitlesEndTime => GetObservable<int>("QueueTitlesEndTime");
			public static IObservable<bool> QueueTitlesUseRange => GetObservable<bool>("QueueTitlesUseRange");
			private static IObservable<T> GetObservable<T>(string configName)
			{
				object observableObject;
				if (observableCache.TryGetValue(configName, out observableObject))
				{
					return (ConfigObservable<T>)observableObject;
				}

				var newObservable = new ConfigObservable<T>(configName);
				observableCache.Add(configName, newObservable);

				return newObservable;
			}
		}
	}
}
