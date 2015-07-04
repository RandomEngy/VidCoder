using System.Collections.Generic;
using System.Data.SQLite;
using VidCoder.Model;

namespace VidCoder
{
	public static class Config
	{
		private static bool initialized;
		private static Dictionary<string, object> cache;

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
			cache.Add("LastSrtFolder", DatabaseConfig.Get("LastSrtFolder", "", connection));
			cache.Add("LastCsvFolder", DatabaseConfig.Get("LastCsvFolder", "", connection));
			cache.Add("LastPresetExportFolder", DatabaseConfig.Get("LastPresetExportFolder", "", connection));
			cache.Add("AutoNameOutputFolder", DatabaseConfig.Get("AutoNameOutputFolder", "", connection));
			cache.Add("AutoNameCustomFormat", DatabaseConfig.Get("AutoNameCustomFormat", false, connection));
			cache.Add("AutoNameCustomFormatString", DatabaseConfig.Get("AutoNameCustomFormatString", "{source}-{title}", connection));
			cache.Add("NativeLanguageCode", DatabaseConfig.Get("NativeLanguageCode", "", connection));
			cache.Add("DubAudio", DatabaseConfig.Get("DubAudio", false, connection));
			cache.Add("LastPresetIndex", DatabaseConfig.Get("LastPresetIndex", 0, connection));
			cache.Add("LastPickerIndex", DatabaseConfig.Get("LastPickerIndex", 0, connection));
			cache.Add("EncodingDialogLastTab", DatabaseConfig.Get("EncodingDialogLastTab", 0, connection));
			cache.Add("OptionsDialogLastTab", DatabaseConfig.Get("OptionsDialogLastTab", 0, connection));
			cache.Add("MainWindowPlacement", DatabaseConfig.Get("MainWindowPlacement", "", connection));
			cache.Add("SubtitlesDialogPlacement", DatabaseConfig.Get("SubtitlesDialogPlacement", "", connection));
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
			cache.Add("UpdatesEnabled", DatabaseConfig.Get("UpdatesEnabled", true, connection));
			cache.Add("PreviewSeconds", DatabaseConfig.Get("PreviewSeconds", 10, connection));
			cache.Add("ApplicationVersion", DatabaseConfig.Get("ApplicationVersion", "", connection));
			cache.Add("QueueColumns", DatabaseConfig.Get("QueueColumns", "Source:200|Title:35|Range:106|Destination:200", connection));
			cache.Add("QueueLastColumnWidth", DatabaseConfig.Get("QueueLastColumnWidth", 75.0, connection));
			cache.Add("CompletedColumnWidths", DatabaseConfig.Get("CompletedColumnWidths", "", connection));
			cache.Add("PickerListPaneWidth", DatabaseConfig.Get("PickerListPaneWidth", 135.0, connection));
			cache.Add("EncodingListPaneOpen", DatabaseConfig.Get("EncodingListPaneOpen", true, connection));
			cache.Add("EncodingListPaneWidth", DatabaseConfig.Get("EncodingListPaneWidth", 150.0, connection));
			cache.Add("ShowPickerWindowMessage", DatabaseConfig.Get("ShowPickerWindowMessage", true, connection));
			cache.Add("WorkerProcessPriority", DatabaseConfig.Get("WorkerProcessPriority", "Normal", connection));
			cache.Add("LogVerbosity", DatabaseConfig.Get("LogVerbosity", 1, connection));
			cache.Add("CopyLogToOutputFolder", DatabaseConfig.Get("CopyLogToOutputFolder", false, connection));
			cache.Add("AutoPauseProcesses", DatabaseConfig.Get("AutoPauseProcesses", "", connection));
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
			cache.Add("UseWorkerProcess", DatabaseConfig.Get("UseWorkerProcess", true, connection));
			cache.Add("RememberPreviousFiles", DatabaseConfig.Get("RememberPreviousFiles", true, connection));
			cache.Add("VideoFileExtensions", DatabaseConfig.Get("VideoFileExtensions", "avi, mkv, mp4, m4v, mpg, mpeg, mov, wmv", connection));
			cache.Add("PreferredPlayer", DatabaseConfig.Get("PreferredPlayer", "vlc", connection));
			cache.Add("BetaUpdates", DatabaseConfig.Get("BetaUpdates", false, connection));
			cache.Add("InterfaceLanguageCode", DatabaseConfig.Get("InterfaceLanguageCode", "", connection));
			cache.Add("CpuThrottlingFraction", DatabaseConfig.Get("CpuThrottlingFraction", 1.0, connection));
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
		}

		public static bool MigratedConfigs
		{
			get
			{
				return (bool)cache["MigratedConfigs"];
			}
			
			set
			{
				cache["MigratedConfigs"] = value;
				DatabaseConfig.Set("MigratedConfigs", value);
			}
		}
		public static string EncodeJobs2
		{
			get
			{
				return (string)cache["EncodeJobs2"];
			}
			
			set
			{
				cache["EncodeJobs2"] = value;
				DatabaseConfig.Set("EncodeJobs2", value);
			}
		}
		public static bool UpdateInProgress
		{
			get
			{
				return (bool)cache["UpdateInProgress"];
			}
			
			set
			{
				cache["UpdateInProgress"] = value;
				DatabaseConfig.Set("UpdateInProgress", value);
			}
		}
		public static string UpdateVersion
		{
			get
			{
				return (string)cache["UpdateVersion"];
			}
			
			set
			{
				cache["UpdateVersion"] = value;
				DatabaseConfig.Set("UpdateVersion", value);
			}
		}
		public static string UpdateInstallerLocation
		{
			get
			{
				return (string)cache["UpdateInstallerLocation"];
			}
			
			set
			{
				cache["UpdateInstallerLocation"] = value;
				DatabaseConfig.Set("UpdateInstallerLocation", value);
			}
		}
		public static string UpdateChangelogLocation
		{
			get
			{
				return (string)cache["UpdateChangelogLocation"];
			}
			
			set
			{
				cache["UpdateChangelogLocation"] = value;
				DatabaseConfig.Set("UpdateChangelogLocation", value);
			}
		}
		public static string LastOutputFolder
		{
			get
			{
				return (string)cache["LastOutputFolder"];
			}
			
			set
			{
				cache["LastOutputFolder"] = value;
				DatabaseConfig.Set("LastOutputFolder", value);
			}
		}
		public static string LastInputFileFolder
		{
			get
			{
				return (string)cache["LastInputFileFolder"];
			}
			
			set
			{
				cache["LastInputFileFolder"] = value;
				DatabaseConfig.Set("LastInputFileFolder", value);
			}
		}
		public static string LastVideoTSFolder
		{
			get
			{
				return (string)cache["LastVideoTSFolder"];
			}
			
			set
			{
				cache["LastVideoTSFolder"] = value;
				DatabaseConfig.Set("LastVideoTSFolder", value);
			}
		}
		public static string LastSrtFolder
		{
			get
			{
				return (string)cache["LastSrtFolder"];
			}
			
			set
			{
				cache["LastSrtFolder"] = value;
				DatabaseConfig.Set("LastSrtFolder", value);
			}
		}
		public static string LastCsvFolder
		{
			get
			{
				return (string)cache["LastCsvFolder"];
			}
			
			set
			{
				cache["LastCsvFolder"] = value;
				DatabaseConfig.Set("LastCsvFolder", value);
			}
		}
		public static string LastPresetExportFolder
		{
			get
			{
				return (string)cache["LastPresetExportFolder"];
			}
			
			set
			{
				cache["LastPresetExportFolder"] = value;
				DatabaseConfig.Set("LastPresetExportFolder", value);
			}
		}
		public static string AutoNameOutputFolder
		{
			get
			{
				return (string)cache["AutoNameOutputFolder"];
			}
			
			set
			{
				cache["AutoNameOutputFolder"] = value;
				DatabaseConfig.Set("AutoNameOutputFolder", value);
			}
		}
		public static bool AutoNameCustomFormat
		{
			get
			{
				return (bool)cache["AutoNameCustomFormat"];
			}
			
			set
			{
				cache["AutoNameCustomFormat"] = value;
				DatabaseConfig.Set("AutoNameCustomFormat", value);
			}
		}
		public static string AutoNameCustomFormatString
		{
			get
			{
				return (string)cache["AutoNameCustomFormatString"];
			}
			
			set
			{
				cache["AutoNameCustomFormatString"] = value;
				DatabaseConfig.Set("AutoNameCustomFormatString", value);
			}
		}
		public static string NativeLanguageCode
		{
			get
			{
				return (string)cache["NativeLanguageCode"];
			}
			
			set
			{
				cache["NativeLanguageCode"] = value;
				DatabaseConfig.Set("NativeLanguageCode", value);
			}
		}
		public static bool DubAudio
		{
			get
			{
				return (bool)cache["DubAudio"];
			}
			
			set
			{
				cache["DubAudio"] = value;
				DatabaseConfig.Set("DubAudio", value);
			}
		}
		public static int LastPresetIndex
		{
			get
			{
				return (int)cache["LastPresetIndex"];
			}
			
			set
			{
				cache["LastPresetIndex"] = value;
				DatabaseConfig.Set("LastPresetIndex", value);
			}
		}
		public static int LastPickerIndex
		{
			get
			{
				return (int)cache["LastPickerIndex"];
			}
			
			set
			{
				cache["LastPickerIndex"] = value;
				DatabaseConfig.Set("LastPickerIndex", value);
			}
		}
		public static int EncodingDialogLastTab
		{
			get
			{
				return (int)cache["EncodingDialogLastTab"];
			}
			
			set
			{
				cache["EncodingDialogLastTab"] = value;
				DatabaseConfig.Set("EncodingDialogLastTab", value);
			}
		}
		public static int OptionsDialogLastTab
		{
			get
			{
				return (int)cache["OptionsDialogLastTab"];
			}
			
			set
			{
				cache["OptionsDialogLastTab"] = value;
				DatabaseConfig.Set("OptionsDialogLastTab", value);
			}
		}
		public static string MainWindowPlacement
		{
			get
			{
				return (string)cache["MainWindowPlacement"];
			}
			
			set
			{
				cache["MainWindowPlacement"] = value;
				DatabaseConfig.Set("MainWindowPlacement", value);
			}
		}
		public static string SubtitlesDialogPlacement
		{
			get
			{
				return (string)cache["SubtitlesDialogPlacement"];
			}
			
			set
			{
				cache["SubtitlesDialogPlacement"] = value;
				DatabaseConfig.Set("SubtitlesDialogPlacement", value);
			}
		}
		public static string EncodingDialogPlacement
		{
			get
			{
				return (string)cache["EncodingDialogPlacement"];
			}
			
			set
			{
				cache["EncodingDialogPlacement"] = value;
				DatabaseConfig.Set("EncodingDialogPlacement", value);
			}
		}
		public static string ChapterMarkersDialogPlacement
		{
			get
			{
				return (string)cache["ChapterMarkersDialogPlacement"];
			}
			
			set
			{
				cache["ChapterMarkersDialogPlacement"] = value;
				DatabaseConfig.Set("ChapterMarkersDialogPlacement", value);
			}
		}
		public static string PreviewWindowPlacement
		{
			get
			{
				return (string)cache["PreviewWindowPlacement"];
			}
			
			set
			{
				cache["PreviewWindowPlacement"] = value;
				DatabaseConfig.Set("PreviewWindowPlacement", value);
			}
		}
		public static string QueueTitlesDialogPlacement2
		{
			get
			{
				return (string)cache["QueueTitlesDialogPlacement2"];
			}
			
			set
			{
				cache["QueueTitlesDialogPlacement2"] = value;
				DatabaseConfig.Set("QueueTitlesDialogPlacement2", value);
			}
		}
		public static string AddAutoPauseProcessDialogPlacement
		{
			get
			{
				return (string)cache["AddAutoPauseProcessDialogPlacement"];
			}
			
			set
			{
				cache["AddAutoPauseProcessDialogPlacement"] = value;
				DatabaseConfig.Set("AddAutoPauseProcessDialogPlacement", value);
			}
		}
		public static string OptionsDialogPlacement
		{
			get
			{
				return (string)cache["OptionsDialogPlacement"];
			}
			
			set
			{
				cache["OptionsDialogPlacement"] = value;
				DatabaseConfig.Set("OptionsDialogPlacement", value);
			}
		}
		public static string EncodeDetailsWindowPlacement
		{
			get
			{
				return (string)cache["EncodeDetailsWindowPlacement"];
			}
			
			set
			{
				cache["EncodeDetailsWindowPlacement"] = value;
				DatabaseConfig.Set("EncodeDetailsWindowPlacement", value);
			}
		}
		public static string PickerWindowPlacement
		{
			get
			{
				return (string)cache["PickerWindowPlacement"];
			}
			
			set
			{
				cache["PickerWindowPlacement"] = value;
				DatabaseConfig.Set("PickerWindowPlacement", value);
			}
		}
		public static string LogWindowPlacement
		{
			get
			{
				return (string)cache["LogWindowPlacement"];
			}
			
			set
			{
				cache["LogWindowPlacement"] = value;
				DatabaseConfig.Set("LogWindowPlacement", value);
			}
		}
		public static bool EncodingWindowOpen
		{
			get
			{
				return (bool)cache["EncodingWindowOpen"];
			}
			
			set
			{
				cache["EncodingWindowOpen"] = value;
				DatabaseConfig.Set("EncodingWindowOpen", value);
			}
		}
		public static bool PreviewWindowOpen
		{
			get
			{
				return (bool)cache["PreviewWindowOpen"];
			}
			
			set
			{
				cache["PreviewWindowOpen"] = value;
				DatabaseConfig.Set("PreviewWindowOpen", value);
			}
		}
		public static bool LogWindowOpen
		{
			get
			{
				return (bool)cache["LogWindowOpen"];
			}
			
			set
			{
				cache["LogWindowOpen"] = value;
				DatabaseConfig.Set("LogWindowOpen", value);
			}
		}
		public static bool EncodeDetailsWindowOpen
		{
			get
			{
				return (bool)cache["EncodeDetailsWindowOpen"];
			}
			
			set
			{
				cache["EncodeDetailsWindowOpen"] = value;
				DatabaseConfig.Set("EncodeDetailsWindowOpen", value);
			}
		}
		public static bool PickerWindowOpen
		{
			get
			{
				return (bool)cache["PickerWindowOpen"];
			}
			
			set
			{
				cache["PickerWindowOpen"] = value;
				DatabaseConfig.Set("PickerWindowOpen", value);
			}
		}
		public static bool UpdatesEnabled
		{
			get
			{
				return (bool)cache["UpdatesEnabled"];
			}
			
			set
			{
				cache["UpdatesEnabled"] = value;
				DatabaseConfig.Set("UpdatesEnabled", value);
			}
		}
		public static int PreviewSeconds
		{
			get
			{
				return (int)cache["PreviewSeconds"];
			}
			
			set
			{
				cache["PreviewSeconds"] = value;
				DatabaseConfig.Set("PreviewSeconds", value);
			}
		}
		public static string ApplicationVersion
		{
			get
			{
				return (string)cache["ApplicationVersion"];
			}
			
			set
			{
				cache["ApplicationVersion"] = value;
				DatabaseConfig.Set("ApplicationVersion", value);
			}
		}
		public static string QueueColumns
		{
			get
			{
				return (string)cache["QueueColumns"];
			}
			
			set
			{
				cache["QueueColumns"] = value;
				DatabaseConfig.Set("QueueColumns", value);
			}
		}
		public static double QueueLastColumnWidth
		{
			get
			{
				return (double)cache["QueueLastColumnWidth"];
			}
			
			set
			{
				cache["QueueLastColumnWidth"] = value;
				DatabaseConfig.Set("QueueLastColumnWidth", value);
			}
		}
		public static string CompletedColumnWidths
		{
			get
			{
				return (string)cache["CompletedColumnWidths"];
			}
			
			set
			{
				cache["CompletedColumnWidths"] = value;
				DatabaseConfig.Set("CompletedColumnWidths", value);
			}
		}
		public static double PickerListPaneWidth
		{
			get
			{
				return (double)cache["PickerListPaneWidth"];
			}
			
			set
			{
				cache["PickerListPaneWidth"] = value;
				DatabaseConfig.Set("PickerListPaneWidth", value);
			}
		}
		public static bool EncodingListPaneOpen
		{
			get
			{
				return (bool)cache["EncodingListPaneOpen"];
			}
			
			set
			{
				cache["EncodingListPaneOpen"] = value;
				DatabaseConfig.Set("EncodingListPaneOpen", value);
			}
		}
		public static double EncodingListPaneWidth
		{
			get
			{
				return (double)cache["EncodingListPaneWidth"];
			}
			
			set
			{
				cache["EncodingListPaneWidth"] = value;
				DatabaseConfig.Set("EncodingListPaneWidth", value);
			}
		}
		public static bool ShowPickerWindowMessage
		{
			get
			{
				return (bool)cache["ShowPickerWindowMessage"];
			}
			
			set
			{
				cache["ShowPickerWindowMessage"] = value;
				DatabaseConfig.Set("ShowPickerWindowMessage", value);
			}
		}
		public static string WorkerProcessPriority
		{
			get
			{
				return (string)cache["WorkerProcessPriority"];
			}
			
			set
			{
				cache["WorkerProcessPriority"] = value;
				DatabaseConfig.Set("WorkerProcessPriority", value);
			}
		}
		public static int LogVerbosity
		{
			get
			{
				return (int)cache["LogVerbosity"];
			}
			
			set
			{
				cache["LogVerbosity"] = value;
				DatabaseConfig.Set("LogVerbosity", value);
			}
		}
		public static bool CopyLogToOutputFolder
		{
			get
			{
				return (bool)cache["CopyLogToOutputFolder"];
			}
			
			set
			{
				cache["CopyLogToOutputFolder"] = value;
				DatabaseConfig.Set("CopyLogToOutputFolder", value);
			}
		}
		public static string AutoPauseProcesses
		{
			get
			{
				return (string)cache["AutoPauseProcesses"];
			}
			
			set
			{
				cache["AutoPauseProcesses"] = value;
				DatabaseConfig.Set("AutoPauseProcesses", value);
			}
		}
		public static int PreviewCount
		{
			get
			{
				return (int)cache["PreviewCount"];
			}
			
			set
			{
				cache["PreviewCount"] = value;
				DatabaseConfig.Set("PreviewCount", value);
			}
		}
		public static string PreviewDisplay
		{
			get
			{
				return (string)cache["PreviewDisplay"];
			}
			
			set
			{
				cache["PreviewDisplay"] = value;
				DatabaseConfig.Set("PreviewDisplay", value);
			}
		}
		public static bool UseCustomPreviewFolder
		{
			get
			{
				return (bool)cache["UseCustomPreviewFolder"];
			}
			
			set
			{
				cache["UseCustomPreviewFolder"] = value;
				DatabaseConfig.Set("UseCustomPreviewFolder", value);
			}
		}
		public static string PreviewOutputFolder
		{
			get
			{
				return (string)cache["PreviewOutputFolder"];
			}
			
			set
			{
				cache["PreviewOutputFolder"] = value;
				DatabaseConfig.Set("PreviewOutputFolder", value);
			}
		}
		public static bool QueueTitlesUseTitleOverride
		{
			get
			{
				return (bool)cache["QueueTitlesUseTitleOverride"];
			}
			
			set
			{
				cache["QueueTitlesUseTitleOverride"] = value;
				DatabaseConfig.Set("QueueTitlesUseTitleOverride", value);
			}
		}
		public static int QueueTitlesTitleOverride
		{
			get
			{
				return (int)cache["QueueTitlesTitleOverride"];
			}
			
			set
			{
				cache["QueueTitlesTitleOverride"] = value;
				DatabaseConfig.Set("QueueTitlesTitleOverride", value);
			}
		}
		public static bool ShowAudioTrackNameField
		{
			get
			{
				return (bool)cache["ShowAudioTrackNameField"];
			}
			
			set
			{
				cache["ShowAudioTrackNameField"] = value;
				DatabaseConfig.Set("ShowAudioTrackNameField", value);
			}
		}
		public static string SourceHistory
		{
			get
			{
				return (string)cache["SourceHistory"];
			}
			
			set
			{
				cache["SourceHistory"] = value;
				DatabaseConfig.Set("SourceHistory", value);
			}
		}
		public static bool MinimizeToTray
		{
			get
			{
				return (bool)cache["MinimizeToTray"];
			}
			
			set
			{
				cache["MinimizeToTray"] = value;
				DatabaseConfig.Set("MinimizeToTray", value);
			}
		}
		public static bool OutputToSourceDirectory
		{
			get
			{
				return (bool)cache["OutputToSourceDirectory"];
			}
			
			set
			{
				cache["OutputToSourceDirectory"] = value;
				DatabaseConfig.Set("OutputToSourceDirectory", value);
			}
		}
		public static bool PreserveFolderStructureInBatch
		{
			get
			{
				return (bool)cache["PreserveFolderStructureInBatch"];
			}
			
			set
			{
				cache["PreserveFolderStructureInBatch"] = value;
				DatabaseConfig.Set("PreserveFolderStructureInBatch", value);
			}
		}
		public static string WhenFileExists
		{
			get
			{
				return (string)cache["WhenFileExists"];
			}
			
			set
			{
				cache["WhenFileExists"] = value;
				DatabaseConfig.Set("WhenFileExists", value);
			}
		}
		public static string WhenFileExistsBatch
		{
			get
			{
				return (string)cache["WhenFileExistsBatch"];
			}
			
			set
			{
				cache["WhenFileExistsBatch"] = value;
				DatabaseConfig.Set("WhenFileExistsBatch", value);
			}
		}
		public static bool UseCustomVideoPlayer
		{
			get
			{
				return (bool)cache["UseCustomVideoPlayer"];
			}
			
			set
			{
				cache["UseCustomVideoPlayer"] = value;
				DatabaseConfig.Set("UseCustomVideoPlayer", value);
			}
		}
		public static string CustomVideoPlayer
		{
			get
			{
				return (string)cache["CustomVideoPlayer"];
			}
			
			set
			{
				cache["CustomVideoPlayer"] = value;
				DatabaseConfig.Set("CustomVideoPlayer", value);
			}
		}
		public static bool PlaySoundOnCompletion
		{
			get
			{
				return (bool)cache["PlaySoundOnCompletion"];
			}
			
			set
			{
				cache["PlaySoundOnCompletion"] = value;
				DatabaseConfig.Set("PlaySoundOnCompletion", value);
			}
		}
		public static bool UseCustomCompletionSound
		{
			get
			{
				return (bool)cache["UseCustomCompletionSound"];
			}
			
			set
			{
				cache["UseCustomCompletionSound"] = value;
				DatabaseConfig.Set("UseCustomCompletionSound", value);
			}
		}
		public static string CustomCompletionSound
		{
			get
			{
				return (string)cache["CustomCompletionSound"];
			}
			
			set
			{
				cache["CustomCompletionSound"] = value;
				DatabaseConfig.Set("CustomCompletionSound", value);
			}
		}
		public static string LastPlayer
		{
			get
			{
				return (string)cache["LastPlayer"];
			}
			
			set
			{
				cache["LastPlayer"] = value;
				DatabaseConfig.Set("LastPlayer", value);
			}
		}
		public static bool QueueTitlesUseDirectoryOverride
		{
			get
			{
				return (bool)cache["QueueTitlesUseDirectoryOverride"];
			}
			
			set
			{
				cache["QueueTitlesUseDirectoryOverride"] = value;
				DatabaseConfig.Set("QueueTitlesUseDirectoryOverride", value);
			}
		}
		public static string QueueTitlesDirectoryOverride
		{
			get
			{
				return (string)cache["QueueTitlesDirectoryOverride"];
			}
			
			set
			{
				cache["QueueTitlesDirectoryOverride"] = value;
				DatabaseConfig.Set("QueueTitlesDirectoryOverride", value);
			}
		}
		public static bool QueueTitlesUseNameOverride
		{
			get
			{
				return (bool)cache["QueueTitlesUseNameOverride"];
			}
			
			set
			{
				cache["QueueTitlesUseNameOverride"] = value;
				DatabaseConfig.Set("QueueTitlesUseNameOverride", value);
			}
		}
		public static string QueueTitlesNameOverride
		{
			get
			{
				return (string)cache["QueueTitlesNameOverride"];
			}
			
			set
			{
				cache["QueueTitlesNameOverride"] = value;
				DatabaseConfig.Set("QueueTitlesNameOverride", value);
			}
		}
		public static bool DxvaDecoding
		{
			get
			{
				return (bool)cache["DxvaDecoding"];
			}
			
			set
			{
				cache["DxvaDecoding"] = value;
				DatabaseConfig.Set("DxvaDecoding", value);
			}
		}
		public static bool EnableLibDvdNav
		{
			get
			{
				return (bool)cache["EnableLibDvdNav"];
			}
			
			set
			{
				cache["EnableLibDvdNav"] = value;
				DatabaseConfig.Set("EnableLibDvdNav", value);
			}
		}
		public static int MinimumTitleLengthSeconds
		{
			get
			{
				return (int)cache["MinimumTitleLengthSeconds"];
			}
			
			set
			{
				cache["MinimumTitleLengthSeconds"] = value;
				DatabaseConfig.Set("MinimumTitleLengthSeconds", value);
			}
		}
		public static bool DeleteSourceFilesOnClearingCompleted
		{
			get
			{
				return (bool)cache["DeleteSourceFilesOnClearingCompleted"];
			}
			
			set
			{
				cache["DeleteSourceFilesOnClearingCompleted"] = value;
				DatabaseConfig.Set("DeleteSourceFilesOnClearingCompleted", value);
			}
		}
		public static bool PreserveModifyTimeFiles
		{
			get
			{
				return (bool)cache["PreserveModifyTimeFiles"];
			}
			
			set
			{
				cache["PreserveModifyTimeFiles"] = value;
				DatabaseConfig.Set("PreserveModifyTimeFiles", value);
			}
		}
		public static bool ResumeEncodingOnRestart
		{
			get
			{
				return (bool)cache["ResumeEncodingOnRestart"];
			}
			
			set
			{
				cache["ResumeEncodingOnRestart"] = value;
				DatabaseConfig.Set("ResumeEncodingOnRestart", value);
			}
		}
		public static bool UseWorkerProcess
		{
			get
			{
				return (bool)cache["UseWorkerProcess"];
			}
			
			set
			{
				cache["UseWorkerProcess"] = value;
				DatabaseConfig.Set("UseWorkerProcess", value);
			}
		}
		public static bool RememberPreviousFiles
		{
			get
			{
				return (bool)cache["RememberPreviousFiles"];
			}
			
			set
			{
				cache["RememberPreviousFiles"] = value;
				DatabaseConfig.Set("RememberPreviousFiles", value);
			}
		}
		public static string VideoFileExtensions
		{
			get
			{
				return (string)cache["VideoFileExtensions"];
			}
			
			set
			{
				cache["VideoFileExtensions"] = value;
				DatabaseConfig.Set("VideoFileExtensions", value);
			}
		}
		public static string PreferredPlayer
		{
			get
			{
				return (string)cache["PreferredPlayer"];
			}
			
			set
			{
				cache["PreferredPlayer"] = value;
				DatabaseConfig.Set("PreferredPlayer", value);
			}
		}
		public static bool BetaUpdates
		{
			get
			{
				return (bool)cache["BetaUpdates"];
			}
			
			set
			{
				cache["BetaUpdates"] = value;
				DatabaseConfig.Set("BetaUpdates", value);
			}
		}
		public static string InterfaceLanguageCode
		{
			get
			{
				return (string)cache["InterfaceLanguageCode"];
			}
			
			set
			{
				cache["InterfaceLanguageCode"] = value;
				DatabaseConfig.Set("InterfaceLanguageCode", value);
			}
		}
		public static double CpuThrottlingFraction
		{
			get
			{
				return (double)cache["CpuThrottlingFraction"];
			}
			
			set
			{
				cache["CpuThrottlingFraction"] = value;
				DatabaseConfig.Set("CpuThrottlingFraction", value);
			}
		}
		public static string AudioLanguageCode
		{
			get
			{
				return (string)cache["AudioLanguageCode"];
			}
			
			set
			{
				cache["AudioLanguageCode"] = value;
				DatabaseConfig.Set("AudioLanguageCode", value);
			}
		}
		public static string SubtitleLanguageCode
		{
			get
			{
				return (string)cache["SubtitleLanguageCode"];
			}
			
			set
			{
				cache["SubtitleLanguageCode"] = value;
				DatabaseConfig.Set("SubtitleLanguageCode", value);
			}
		}
		public static string AutoAudio
		{
			get
			{
				return (string)cache["AutoAudio"];
			}
			
			set
			{
				cache["AutoAudio"] = value;
				DatabaseConfig.Set("AutoAudio", value);
			}
		}
		public static string AutoSubtitle
		{
			get
			{
				return (string)cache["AutoSubtitle"];
			}
			
			set
			{
				cache["AutoSubtitle"] = value;
				DatabaseConfig.Set("AutoSubtitle", value);
			}
		}
		public static bool AutoAudioAll
		{
			get
			{
				return (bool)cache["AutoAudioAll"];
			}
			
			set
			{
				cache["AutoAudioAll"] = value;
				DatabaseConfig.Set("AutoAudioAll", value);
			}
		}
		public static bool AutoSubtitleAll
		{
			get
			{
				return (bool)cache["AutoSubtitleAll"];
			}
			
			set
			{
				cache["AutoSubtitleAll"] = value;
				DatabaseConfig.Set("AutoSubtitleAll", value);
			}
		}
		public static bool AutoSubtitleOnlyIfDifferent
		{
			get
			{
				return (bool)cache["AutoSubtitleOnlyIfDifferent"];
			}
			
			set
			{
				cache["AutoSubtitleOnlyIfDifferent"] = value;
				DatabaseConfig.Set("AutoSubtitleOnlyIfDifferent", value);
			}
		}
		public static bool AutoSubtitleBurnIn
		{
			get
			{
				return (bool)cache["AutoSubtitleBurnIn"];
			}
			
			set
			{
				cache["AutoSubtitleBurnIn"] = value;
				DatabaseConfig.Set("AutoSubtitleBurnIn", value);
			}
		}
		public static bool AutoSubtitleLanguageDefault
		{
			get
			{
				return (bool)cache["AutoSubtitleLanguageDefault"];
			}
			
			set
			{
				cache["AutoSubtitleLanguageDefault"] = value;
				DatabaseConfig.Set("AutoSubtitleLanguageDefault", value);
			}
		}
		public static bool AutoSubtitleLanguageBurnIn
		{
			get
			{
				return (bool)cache["AutoSubtitleLanguageBurnIn"];
			}
			
			set
			{
				cache["AutoSubtitleLanguageBurnIn"] = value;
				DatabaseConfig.Set("AutoSubtitleLanguageBurnIn", value);
			}
		}
		public static int QueueTitlesStartTime
		{
			get
			{
				return (int)cache["QueueTitlesStartTime"];
			}
			
			set
			{
				cache["QueueTitlesStartTime"] = value;
				DatabaseConfig.Set("QueueTitlesStartTime", value);
			}
		}
		public static int QueueTitlesEndTime
		{
			get
			{
				return (int)cache["QueueTitlesEndTime"];
			}
			
			set
			{
				cache["QueueTitlesEndTime"] = value;
				DatabaseConfig.Set("QueueTitlesEndTime", value);
			}
		}
		public static bool QueueTitlesUseRange
		{
			get
			{
				return (bool)cache["QueueTitlesUseRange"];
			}
			
			set
			{
				cache["QueueTitlesUseRange"] = value;
				DatabaseConfig.Set("QueueTitlesUseRange", value);
			}
		}
	}
}