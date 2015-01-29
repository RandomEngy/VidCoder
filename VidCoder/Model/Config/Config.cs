namespace VidCoder
{
	using System.Data.SQLite;
	using Model;

	public static class Config
	{
		public static void Initialize(SQLiteConnection connection)
		{
			MigratedConfigs_Field = DatabaseConfig.GetConfig("MigratedConfigs", false, connection);
			EncodeJobs_Field = DatabaseConfig.GetConfig("EncodeJobs", "", connection);
			EncodeJobs2_Field = DatabaseConfig.GetConfig("EncodeJobs2", "", connection);
			UpdateInProgress_Field = DatabaseConfig.GetConfig("UpdateInProgress", false, connection);
			UpdateVersion_Field = DatabaseConfig.GetConfig("UpdateVersion", "", connection);
			UpdateInstallerLocation_Field = DatabaseConfig.GetConfig("UpdateInstallerLocation", "", connection);
			UpdateChangelogLocation_Field = DatabaseConfig.GetConfig("UpdateChangelogLocation", "", connection);
			LastOutputFolder_Field = DatabaseConfig.GetConfig("LastOutputFolder", "", connection);
			LastInputFileFolder_Field = DatabaseConfig.GetConfig("LastInputFileFolder", "", connection);
			LastVideoTSFolder_Field = DatabaseConfig.GetConfig("LastVideoTSFolder", "", connection);
			LastSrtFolder_Field = DatabaseConfig.GetConfig("LastSrtFolder", "", connection);
			LastCsvFolder_Field = DatabaseConfig.GetConfig("LastCsvFolder", "", connection);
			LastPresetExportFolder_Field = DatabaseConfig.GetConfig("LastPresetExportFolder", "", connection);
			AutoNameOutputFolder_Field = DatabaseConfig.GetConfig("AutoNameOutputFolder", "", connection);
			AutoNameCustomFormat_Field = DatabaseConfig.GetConfig("AutoNameCustomFormat", false, connection);
			AutoNameCustomFormatString_Field = DatabaseConfig.GetConfig("AutoNameCustomFormatString", "{source}-{title}", connection);
			NativeLanguageCode_Field = DatabaseConfig.GetConfig("NativeLanguageCode", "", connection);
			DubAudio_Field = DatabaseConfig.GetConfig("DubAudio", false, connection);
			LastPresetIndex_Field = DatabaseConfig.GetConfig("LastPresetIndex", 0, connection);
			LastPickerIndex_Field = DatabaseConfig.GetConfig("LastPickerIndex", 0, connection);
			EncodingDialogLastTab_Field = DatabaseConfig.GetConfig("EncodingDialogLastTab", 0, connection);
			OptionsDialogLastTab_Field = DatabaseConfig.GetConfig("OptionsDialogLastTab", 0, connection);
			MainWindowPlacement_Field = DatabaseConfig.GetConfig("MainWindowPlacement", "", connection);
			SubtitlesDialogPlacement_Field = DatabaseConfig.GetConfig("SubtitlesDialogPlacement", "", connection);
			EncodingDialogPlacement_Field = DatabaseConfig.GetConfig("EncodingDialogPlacement", "", connection);
			ChapterMarkersDialogPlacement_Field = DatabaseConfig.GetConfig("ChapterMarkersDialogPlacement", "", connection);
			PreviewWindowPlacement_Field = DatabaseConfig.GetConfig("PreviewWindowPlacement", "", connection);
			QueueTitlesDialogPlacement_Field = DatabaseConfig.GetConfig("QueueTitlesDialogPlacement", "", connection);
			QueueTitlesDialogPlacement2_Field = DatabaseConfig.GetConfig("QueueTitlesDialogPlacement2", "", connection);
			AddAutoPauseProcessDialogPlacement_Field = DatabaseConfig.GetConfig("AddAutoPauseProcessDialogPlacement", "", connection);
			OptionsDialogPlacement_Field = DatabaseConfig.GetConfig("OptionsDialogPlacement", "", connection);
			EncodeDetailsWindowPlacement_Field = DatabaseConfig.GetConfig("EncodeDetailsWindowPlacement", "", connection);
			PickerWindowPlacement_Field = DatabaseConfig.GetConfig("PickerWindowPlacement", "", connection);
			EncodingWindowOpen_Field = DatabaseConfig.GetConfig("EncodingWindowOpen", false, connection);
			PreviewWindowOpen_Field = DatabaseConfig.GetConfig("PreviewWindowOpen", false, connection);
			LogWindowOpen_Field = DatabaseConfig.GetConfig("LogWindowOpen", false, connection);
			EncodeDetailsWindowOpen_Field = DatabaseConfig.GetConfig("EncodeDetailsWindowOpen", false, connection);
			PickerWindowOpen_Field = DatabaseConfig.GetConfig("PickerWindowOpen", false, connection);
			UpdatesEnabled_Field = DatabaseConfig.GetConfig("UpdatesEnabled", true, connection);
			PreviewSeconds_Field = DatabaseConfig.GetConfig("PreviewSeconds", 10, connection);
			ApplicationVersion_Field = DatabaseConfig.GetConfig("ApplicationVersion", "", connection);
			LogWindowPlacement_Field = DatabaseConfig.GetConfig("LogWindowPlacement", "", connection);
			QueueColumns_Field = DatabaseConfig.GetConfig("QueueColumns", "Source:200|Title:35|Range:106|Destination:200", connection);
			QueueLastColumnWidth_Field = DatabaseConfig.GetConfig("QueueLastColumnWidth", 75.0, connection);
			CompletedColumnWidths_Field = DatabaseConfig.GetConfig("CompletedColumnWidths", "", connection);
			PickerListPaneWidth_Field = DatabaseConfig.GetConfig("PickerListPaneWidth", 135.0, connection);
			EncodingListPaneOpen_Field = DatabaseConfig.GetConfig("EncodingListPaneOpen", true, connection);
			EncodingListPaneWidth_Field = DatabaseConfig.GetConfig("EncodingListPaneWidth", 150.0, connection);
			ShowPickerWindowMessage_Field = DatabaseConfig.GetConfig("ShowPickerWindowMessage", true, connection);
			WorkerProcessPriority_Field = DatabaseConfig.GetConfig("WorkerProcessPriority", "Normal", connection);
			LogVerbosity_Field = DatabaseConfig.GetConfig("LogVerbosity", 1, connection);
			CopyLogToOutputFolder_Field = DatabaseConfig.GetConfig("CopyLogToOutputFolder", false, connection);
			AutoPauseProcesses_Field = DatabaseConfig.GetConfig("AutoPauseProcesses", "", connection);
			PreviewCount_Field = DatabaseConfig.GetConfig("PreviewCount", 10, connection);
			PreviewDisplay_Field = DatabaseConfig.GetConfig("PreviewDisplay", "FitToWindow", connection);
			UseCustomPreviewFolder_Field = DatabaseConfig.GetConfig("UseCustomPreviewFolder", false, connection);
			PreviewOutputFolder_Field = DatabaseConfig.GetConfig("PreviewOutputFolder", "", connection);
			QueueTitlesUseTitleOverride_Field = DatabaseConfig.GetConfig("QueueTitlesUseTitleOverride", false, connection);
			QueueTitlesTitleOverride_Field = DatabaseConfig.GetConfig("QueueTitlesTitleOverride", 1, connection);
			ShowAudioTrackNameField_Field = DatabaseConfig.GetConfig("ShowAudioTrackNameField", false, connection);
			SourceHistory_Field = DatabaseConfig.GetConfig("SourceHistory", "", connection);
			MinimizeToTray_Field = DatabaseConfig.GetConfig("MinimizeToTray", false, connection);
			OutputToSourceDirectory_Field = DatabaseConfig.GetConfig("OutputToSourceDirectory", false, connection);
			PreserveFolderStructureInBatch_Field = DatabaseConfig.GetConfig("PreserveFolderStructureInBatch", false, connection);
			WhenFileExists_Field = DatabaseConfig.GetConfig("WhenFileExists", "Prompt", connection);
			WhenFileExistsBatch_Field = DatabaseConfig.GetConfig("WhenFileExistsBatch", "AutoRename", connection);
			KeepScansAfterCompletion_Field = DatabaseConfig.GetConfig("KeepScansAfterCompletion", true, connection);
			UseCustomVideoPlayer_Field = DatabaseConfig.GetConfig("UseCustomVideoPlayer", false, connection);
			CustomVideoPlayer_Field = DatabaseConfig.GetConfig("CustomVideoPlayer", "", connection);
			PlaySoundOnCompletion_Field = DatabaseConfig.GetConfig("PlaySoundOnCompletion", false, connection);
			UseCustomCompletionSound_Field = DatabaseConfig.GetConfig("UseCustomCompletionSound", false, connection);
			CustomCompletionSound_Field = DatabaseConfig.GetConfig("CustomCompletionSound", "", connection);
			LastPlayer_Field = DatabaseConfig.GetConfig("LastPlayer", "vlc", connection);
			QueueTitlesUseDirectoryOverride_Field = DatabaseConfig.GetConfig("QueueTitlesUseDirectoryOverride", false, connection);
			QueueTitlesDirectoryOverride_Field = DatabaseConfig.GetConfig("QueueTitlesDirectoryOverride", "", connection);
			QueueTitlesUseNameOverride_Field = DatabaseConfig.GetConfig("QueueTitlesUseNameOverride", false, connection);
			QueueTitlesNameOverride_Field = DatabaseConfig.GetConfig("QueueTitlesNameOverride", "", connection);
			DxvaDecoding_Field = DatabaseConfig.GetConfig("DxvaDecoding", false, connection);
			EnableLibDvdNav_Field = DatabaseConfig.GetConfig("EnableLibDvdNav", true, connection);
			MinimumTitleLengthSeconds_Field = DatabaseConfig.GetConfig("MinimumTitleLengthSeconds", 10, connection);
			DeleteSourceFilesOnClearingCompleted_Field = DatabaseConfig.GetConfig("DeleteSourceFilesOnClearingCompleted", false, connection);
			PreserveModifyTimeFiles_Field = DatabaseConfig.GetConfig("PreserveModifyTimeFiles", false, connection);
			ResumeEncodingOnRestart_Field = DatabaseConfig.GetConfig("ResumeEncodingOnRestart", false, connection);
			UseWorkerProcess_Field = DatabaseConfig.GetConfig("UseWorkerProcess", true, connection);
			RememberPreviousFiles_Field = DatabaseConfig.GetConfig("RememberPreviousFiles", true, connection);
			VideoFileExtensions_Field = DatabaseConfig.GetConfig("VideoFileExtensions", "avi, mkv, mp4, m4v, mpg, mpeg, mov, wmv", connection);
			PreferredPlayer_Field = DatabaseConfig.GetConfig("PreferredPlayer", "vlc", connection);
			BetaUpdates_Field = DatabaseConfig.GetConfig("BetaUpdates", false, connection);
			InterfaceLanguageCode_Field = DatabaseConfig.GetConfig("InterfaceLanguageCode", "", connection);
			AudioLanguageCode_Field = DatabaseConfig.GetConfig("AudioLanguageCode", "und", connection);
			SubtitleLanguageCode_Field = DatabaseConfig.GetConfig("SubtitleLanguageCode", "und", connection);
			AutoAudio_Field = DatabaseConfig.GetConfig("AutoAudio", "Disabled", connection);
			AutoSubtitle_Field = DatabaseConfig.GetConfig("AutoSubtitle", "Disabled", connection);
			AutoAudioAll_Field = DatabaseConfig.GetConfig("AutoAudioAll", false, connection);
			AutoSubtitleAll_Field = DatabaseConfig.GetConfig("AutoSubtitleAll", false, connection);
			AutoSubtitleOnlyIfDifferent_Field = DatabaseConfig.GetConfig("AutoSubtitleOnlyIfDifferent", true, connection);
			AutoSubtitleBurnIn_Field = DatabaseConfig.GetConfig("AutoSubtitleBurnIn", true, connection);
			AutoSubtitleLanguageDefault_Field = DatabaseConfig.GetConfig("AutoSubtitleLanguageDefault", false, connection);
			AutoSubtitleLanguageBurnIn_Field = DatabaseConfig.GetConfig("AutoSubtitleLanguageBurnIn", false, connection);
			QueueTitlesStartTime_Field = DatabaseConfig.GetConfig("QueueTitlesStartTime", 40, connection);
			QueueTitlesEndTime_Field = DatabaseConfig.GetConfig("QueueTitlesEndTime", 45, connection);
			QueueTitlesUseRange_Field = DatabaseConfig.GetConfig("QueueTitlesUseRange", false, connection);
		}
		private static bool MigratedConfigs_Field;
		public static bool MigratedConfigs
		{
			get
			{
				return MigratedConfigs_Field;
			}
			
			set
			{
				MigratedConfigs_Field = value;
				DatabaseConfig.SetConfigValue("MigratedConfigs", value);
			}
		}
		private static string EncodeJobs_Field;
		public static string EncodeJobs
		{
			get
			{
				return EncodeJobs_Field;
			}
			
			set
			{
				EncodeJobs_Field = value;
				DatabaseConfig.SetConfigValue("EncodeJobs", value);
			}
		}
		private static string EncodeJobs2_Field;
		public static string EncodeJobs2
		{
			get
			{
				return EncodeJobs2_Field;
			}
			
			set
			{
				EncodeJobs2_Field = value;
				DatabaseConfig.SetConfigValue("EncodeJobs2", value);
			}
		}
		private static bool UpdateInProgress_Field;
		public static bool UpdateInProgress
		{
			get
			{
				return UpdateInProgress_Field;
			}
			
			set
			{
				UpdateInProgress_Field = value;
				DatabaseConfig.SetConfigValue("UpdateInProgress", value);
			}
		}
		private static string UpdateVersion_Field;
		public static string UpdateVersion
		{
			get
			{
				return UpdateVersion_Field;
			}
			
			set
			{
				UpdateVersion_Field = value;
				DatabaseConfig.SetConfigValue("UpdateVersion", value);
			}
		}
		private static string UpdateInstallerLocation_Field;
		public static string UpdateInstallerLocation
		{
			get
			{
				return UpdateInstallerLocation_Field;
			}
			
			set
			{
				UpdateInstallerLocation_Field = value;
				DatabaseConfig.SetConfigValue("UpdateInstallerLocation", value);
			}
		}
		private static string UpdateChangelogLocation_Field;
		public static string UpdateChangelogLocation
		{
			get
			{
				return UpdateChangelogLocation_Field;
			}
			
			set
			{
				UpdateChangelogLocation_Field = value;
				DatabaseConfig.SetConfigValue("UpdateChangelogLocation", value);
			}
		}
		private static string LastOutputFolder_Field;
		public static string LastOutputFolder
		{
			get
			{
				return LastOutputFolder_Field;
			}
			
			set
			{
				LastOutputFolder_Field = value;
				DatabaseConfig.SetConfigValue("LastOutputFolder", value);
			}
		}
		private static string LastInputFileFolder_Field;
		public static string LastInputFileFolder
		{
			get
			{
				return LastInputFileFolder_Field;
			}
			
			set
			{
				LastInputFileFolder_Field = value;
				DatabaseConfig.SetConfigValue("LastInputFileFolder", value);
			}
		}
		private static string LastVideoTSFolder_Field;
		public static string LastVideoTSFolder
		{
			get
			{
				return LastVideoTSFolder_Field;
			}
			
			set
			{
				LastVideoTSFolder_Field = value;
				DatabaseConfig.SetConfigValue("LastVideoTSFolder", value);
			}
		}
		private static string LastSrtFolder_Field;
		public static string LastSrtFolder
		{
			get
			{
				return LastSrtFolder_Field;
			}
			
			set
			{
				LastSrtFolder_Field = value;
				DatabaseConfig.SetConfigValue("LastSrtFolder", value);
			}
		}
		private static string LastCsvFolder_Field;
		public static string LastCsvFolder
		{
			get
			{
				return LastCsvFolder_Field;
			}
			
			set
			{
				LastCsvFolder_Field = value;
				DatabaseConfig.SetConfigValue("LastCsvFolder", value);
			}
		}
		private static string LastPresetExportFolder_Field;
		public static string LastPresetExportFolder
		{
			get
			{
				return LastPresetExportFolder_Field;
			}
			
			set
			{
				LastPresetExportFolder_Field = value;
				DatabaseConfig.SetConfigValue("LastPresetExportFolder", value);
			}
		}
		private static string AutoNameOutputFolder_Field;
		public static string AutoNameOutputFolder
		{
			get
			{
				return AutoNameOutputFolder_Field;
			}
			
			set
			{
				AutoNameOutputFolder_Field = value;
				DatabaseConfig.SetConfigValue("AutoNameOutputFolder", value);
			}
		}
		private static bool AutoNameCustomFormat_Field;
		public static bool AutoNameCustomFormat
		{
			get
			{
				return AutoNameCustomFormat_Field;
			}
			
			set
			{
				AutoNameCustomFormat_Field = value;
				DatabaseConfig.SetConfigValue("AutoNameCustomFormat", value);
			}
		}
		private static string AutoNameCustomFormatString_Field;
		public static string AutoNameCustomFormatString
		{
			get
			{
				return AutoNameCustomFormatString_Field;
			}
			
			set
			{
				AutoNameCustomFormatString_Field = value;
				DatabaseConfig.SetConfigValue("AutoNameCustomFormatString", value);
			}
		}
		private static string NativeLanguageCode_Field;
		public static string NativeLanguageCode
		{
			get
			{
				return NativeLanguageCode_Field;
			}
			
			set
			{
				NativeLanguageCode_Field = value;
				DatabaseConfig.SetConfigValue("NativeLanguageCode", value);
			}
		}
		private static bool DubAudio_Field;
		public static bool DubAudio
		{
			get
			{
				return DubAudio_Field;
			}
			
			set
			{
				DubAudio_Field = value;
				DatabaseConfig.SetConfigValue("DubAudio", value);
			}
		}
		private static int LastPresetIndex_Field;
		public static int LastPresetIndex
		{
			get
			{
				return LastPresetIndex_Field;
			}
			
			set
			{
				LastPresetIndex_Field = value;
				DatabaseConfig.SetConfigValue("LastPresetIndex", value);
			}
		}
		private static int LastPickerIndex_Field;
		public static int LastPickerIndex
		{
			get
			{
				return LastPickerIndex_Field;
			}
			
			set
			{
				LastPickerIndex_Field = value;
				DatabaseConfig.SetConfigValue("LastPickerIndex", value);
			}
		}
		private static int EncodingDialogLastTab_Field;
		public static int EncodingDialogLastTab
		{
			get
			{
				return EncodingDialogLastTab_Field;
			}
			
			set
			{
				EncodingDialogLastTab_Field = value;
				DatabaseConfig.SetConfigValue("EncodingDialogLastTab", value);
			}
		}
		private static int OptionsDialogLastTab_Field;
		public static int OptionsDialogLastTab
		{
			get
			{
				return OptionsDialogLastTab_Field;
			}
			
			set
			{
				OptionsDialogLastTab_Field = value;
				DatabaseConfig.SetConfigValue("OptionsDialogLastTab", value);
			}
		}
		private static string MainWindowPlacement_Field;
		public static string MainWindowPlacement
		{
			get
			{
				return MainWindowPlacement_Field;
			}
			
			set
			{
				MainWindowPlacement_Field = value;
				DatabaseConfig.SetConfigValue("MainWindowPlacement", value);
			}
		}
		private static string SubtitlesDialogPlacement_Field;
		public static string SubtitlesDialogPlacement
		{
			get
			{
				return SubtitlesDialogPlacement_Field;
			}
			
			set
			{
				SubtitlesDialogPlacement_Field = value;
				DatabaseConfig.SetConfigValue("SubtitlesDialogPlacement", value);
			}
		}
		private static string EncodingDialogPlacement_Field;
		public static string EncodingDialogPlacement
		{
			get
			{
				return EncodingDialogPlacement_Field;
			}
			
			set
			{
				EncodingDialogPlacement_Field = value;
				DatabaseConfig.SetConfigValue("EncodingDialogPlacement", value);
			}
		}
		private static string ChapterMarkersDialogPlacement_Field;
		public static string ChapterMarkersDialogPlacement
		{
			get
			{
				return ChapterMarkersDialogPlacement_Field;
			}
			
			set
			{
				ChapterMarkersDialogPlacement_Field = value;
				DatabaseConfig.SetConfigValue("ChapterMarkersDialogPlacement", value);
			}
		}
		private static string PreviewWindowPlacement_Field;
		public static string PreviewWindowPlacement
		{
			get
			{
				return PreviewWindowPlacement_Field;
			}
			
			set
			{
				PreviewWindowPlacement_Field = value;
				DatabaseConfig.SetConfigValue("PreviewWindowPlacement", value);
			}
		}
		private static string QueueTitlesDialogPlacement_Field;
		public static string QueueTitlesDialogPlacement
		{
			get
			{
				return QueueTitlesDialogPlacement_Field;
			}
			
			set
			{
				QueueTitlesDialogPlacement_Field = value;
				DatabaseConfig.SetConfigValue("QueueTitlesDialogPlacement", value);
			}
		}
		private static string QueueTitlesDialogPlacement2_Field;
		public static string QueueTitlesDialogPlacement2
		{
			get
			{
				return QueueTitlesDialogPlacement2_Field;
			}
			
			set
			{
				QueueTitlesDialogPlacement2_Field = value;
				DatabaseConfig.SetConfigValue("QueueTitlesDialogPlacement2", value);
			}
		}
		private static string AddAutoPauseProcessDialogPlacement_Field;
		public static string AddAutoPauseProcessDialogPlacement
		{
			get
			{
				return AddAutoPauseProcessDialogPlacement_Field;
			}
			
			set
			{
				AddAutoPauseProcessDialogPlacement_Field = value;
				DatabaseConfig.SetConfigValue("AddAutoPauseProcessDialogPlacement", value);
			}
		}
		private static string OptionsDialogPlacement_Field;
		public static string OptionsDialogPlacement
		{
			get
			{
				return OptionsDialogPlacement_Field;
			}
			
			set
			{
				OptionsDialogPlacement_Field = value;
				DatabaseConfig.SetConfigValue("OptionsDialogPlacement", value);
			}
		}
		private static string EncodeDetailsWindowPlacement_Field;
		public static string EncodeDetailsWindowPlacement
		{
			get
			{
				return EncodeDetailsWindowPlacement_Field;
			}
			
			set
			{
				EncodeDetailsWindowPlacement_Field = value;
				DatabaseConfig.SetConfigValue("EncodeDetailsWindowPlacement", value);
			}
		}
		private static string PickerWindowPlacement_Field;
		public static string PickerWindowPlacement
		{
			get
			{
				return PickerWindowPlacement_Field;
			}
			
			set
			{
				PickerWindowPlacement_Field = value;
				DatabaseConfig.SetConfigValue("PickerWindowPlacement", value);
			}
		}
		private static bool EncodingWindowOpen_Field;
		public static bool EncodingWindowOpen
		{
			get
			{
				return EncodingWindowOpen_Field;
			}
			
			set
			{
				EncodingWindowOpen_Field = value;
				DatabaseConfig.SetConfigValue("EncodingWindowOpen", value);
			}
		}
		private static bool PreviewWindowOpen_Field;
		public static bool PreviewWindowOpen
		{
			get
			{
				return PreviewWindowOpen_Field;
			}
			
			set
			{
				PreviewWindowOpen_Field = value;
				DatabaseConfig.SetConfigValue("PreviewWindowOpen", value);
			}
		}
		private static bool LogWindowOpen_Field;
		public static bool LogWindowOpen
		{
			get
			{
				return LogWindowOpen_Field;
			}
			
			set
			{
				LogWindowOpen_Field = value;
				DatabaseConfig.SetConfigValue("LogWindowOpen", value);
			}
		}
		private static bool EncodeDetailsWindowOpen_Field;
		public static bool EncodeDetailsWindowOpen
		{
			get
			{
				return EncodeDetailsWindowOpen_Field;
			}
			
			set
			{
				EncodeDetailsWindowOpen_Field = value;
				DatabaseConfig.SetConfigValue("EncodeDetailsWindowOpen", value);
			}
		}
		private static bool PickerWindowOpen_Field;
		public static bool PickerWindowOpen
		{
			get
			{
				return PickerWindowOpen_Field;
			}
			
			set
			{
				PickerWindowOpen_Field = value;
				DatabaseConfig.SetConfigValue("PickerWindowOpen", value);
			}
		}
		private static bool UpdatesEnabled_Field;
		public static bool UpdatesEnabled
		{
			get
			{
				return UpdatesEnabled_Field;
			}
			
			set
			{
				UpdatesEnabled_Field = value;
				DatabaseConfig.SetConfigValue("UpdatesEnabled", value);
			}
		}
		private static int PreviewSeconds_Field;
		public static int PreviewSeconds
		{
			get
			{
				return PreviewSeconds_Field;
			}
			
			set
			{
				PreviewSeconds_Field = value;
				DatabaseConfig.SetConfigValue("PreviewSeconds", value);
			}
		}
		private static string ApplicationVersion_Field;
		public static string ApplicationVersion
		{
			get
			{
				return ApplicationVersion_Field;
			}
			
			set
			{
				ApplicationVersion_Field = value;
				DatabaseConfig.SetConfigValue("ApplicationVersion", value);
			}
		}
		private static string LogWindowPlacement_Field;
		public static string LogWindowPlacement
		{
			get
			{
				return LogWindowPlacement_Field;
			}
			
			set
			{
				LogWindowPlacement_Field = value;
				DatabaseConfig.SetConfigValue("LogWindowPlacement", value);
			}
		}
		private static string QueueColumns_Field;
		public static string QueueColumns
		{
			get
			{
				return QueueColumns_Field;
			}
			
			set
			{
				QueueColumns_Field = value;
				DatabaseConfig.SetConfigValue("QueueColumns", value);
			}
		}
		private static double QueueLastColumnWidth_Field;
		public static double QueueLastColumnWidth
		{
			get
			{
				return QueueLastColumnWidth_Field;
			}
			
			set
			{
				QueueLastColumnWidth_Field = value;
				DatabaseConfig.SetConfigValue("QueueLastColumnWidth", value);
			}
		}
		private static string CompletedColumnWidths_Field;
		public static string CompletedColumnWidths
		{
			get
			{
				return CompletedColumnWidths_Field;
			}
			
			set
			{
				CompletedColumnWidths_Field = value;
				DatabaseConfig.SetConfigValue("CompletedColumnWidths", value);
			}
		}
		private static double PickerListPaneWidth_Field;
		public static double PickerListPaneWidth
		{
			get
			{
				return PickerListPaneWidth_Field;
			}
			
			set
			{
				PickerListPaneWidth_Field = value;
				DatabaseConfig.SetConfigValue("PickerListPaneWidth", value);
			}
		}
		private static bool EncodingListPaneOpen_Field;
		public static bool EncodingListPaneOpen
		{
			get
			{
				return EncodingListPaneOpen_Field;
			}
			
			set
			{
				EncodingListPaneOpen_Field = value;
				DatabaseConfig.SetConfigValue("EncodingListPaneOpen", value);
			}
		}
		private static double EncodingListPaneWidth_Field;
		public static double EncodingListPaneWidth
		{
			get
			{
				return EncodingListPaneWidth_Field;
			}
			
			set
			{
				EncodingListPaneWidth_Field = value;
				DatabaseConfig.SetConfigValue("EncodingListPaneWidth", value);
			}
		}
		private static bool ShowPickerWindowMessage_Field;
		public static bool ShowPickerWindowMessage
		{
			get
			{
				return ShowPickerWindowMessage_Field;
			}
			
			set
			{
				ShowPickerWindowMessage_Field = value;
				DatabaseConfig.SetConfigValue("ShowPickerWindowMessage", value);
			}
		}
		private static string WorkerProcessPriority_Field;
		public static string WorkerProcessPriority
		{
			get
			{
				return WorkerProcessPriority_Field;
			}
			
			set
			{
				WorkerProcessPriority_Field = value;
				DatabaseConfig.SetConfigValue("WorkerProcessPriority", value);
			}
		}
		private static int LogVerbosity_Field;
		public static int LogVerbosity
		{
			get
			{
				return LogVerbosity_Field;
			}
			
			set
			{
				LogVerbosity_Field = value;
				DatabaseConfig.SetConfigValue("LogVerbosity", value);
			}
		}
		private static bool CopyLogToOutputFolder_Field;
		public static bool CopyLogToOutputFolder
		{
			get
			{
				return CopyLogToOutputFolder_Field;
			}
			
			set
			{
				CopyLogToOutputFolder_Field = value;
				DatabaseConfig.SetConfigValue("CopyLogToOutputFolder", value);
			}
		}
		private static string AutoPauseProcesses_Field;
		public static string AutoPauseProcesses
		{
			get
			{
				return AutoPauseProcesses_Field;
			}
			
			set
			{
				AutoPauseProcesses_Field = value;
				DatabaseConfig.SetConfigValue("AutoPauseProcesses", value);
			}
		}
		private static int PreviewCount_Field;
		public static int PreviewCount
		{
			get
			{
				return PreviewCount_Field;
			}
			
			set
			{
				PreviewCount_Field = value;
				DatabaseConfig.SetConfigValue("PreviewCount", value);
			}
		}
		private static string PreviewDisplay_Field;
		public static string PreviewDisplay
		{
			get
			{
				return PreviewDisplay_Field;
			}
			
			set
			{
				PreviewDisplay_Field = value;
				DatabaseConfig.SetConfigValue("PreviewDisplay", value);
			}
		}
		private static bool UseCustomPreviewFolder_Field;
		public static bool UseCustomPreviewFolder
		{
			get
			{
				return UseCustomPreviewFolder_Field;
			}
			
			set
			{
				UseCustomPreviewFolder_Field = value;
				DatabaseConfig.SetConfigValue("UseCustomPreviewFolder", value);
			}
		}
		private static string PreviewOutputFolder_Field;
		public static string PreviewOutputFolder
		{
			get
			{
				return PreviewOutputFolder_Field;
			}
			
			set
			{
				PreviewOutputFolder_Field = value;
				DatabaseConfig.SetConfigValue("PreviewOutputFolder", value);
			}
		}
		private static bool QueueTitlesUseTitleOverride_Field;
		public static bool QueueTitlesUseTitleOverride
		{
			get
			{
				return QueueTitlesUseTitleOverride_Field;
			}
			
			set
			{
				QueueTitlesUseTitleOverride_Field = value;
				DatabaseConfig.SetConfigValue("QueueTitlesUseTitleOverride", value);
			}
		}
		private static int QueueTitlesTitleOverride_Field;
		public static int QueueTitlesTitleOverride
		{
			get
			{
				return QueueTitlesTitleOverride_Field;
			}
			
			set
			{
				QueueTitlesTitleOverride_Field = value;
				DatabaseConfig.SetConfigValue("QueueTitlesTitleOverride", value);
			}
		}
		private static bool ShowAudioTrackNameField_Field;
		public static bool ShowAudioTrackNameField
		{
			get
			{
				return ShowAudioTrackNameField_Field;
			}
			
			set
			{
				ShowAudioTrackNameField_Field = value;
				DatabaseConfig.SetConfigValue("ShowAudioTrackNameField", value);
			}
		}
		private static string SourceHistory_Field;
		public static string SourceHistory
		{
			get
			{
				return SourceHistory_Field;
			}
			
			set
			{
				SourceHistory_Field = value;
				DatabaseConfig.SetConfigValue("SourceHistory", value);
			}
		}
		private static bool MinimizeToTray_Field;
		public static bool MinimizeToTray
		{
			get
			{
				return MinimizeToTray_Field;
			}
			
			set
			{
				MinimizeToTray_Field = value;
				DatabaseConfig.SetConfigValue("MinimizeToTray", value);
			}
		}
		private static bool OutputToSourceDirectory_Field;
		public static bool OutputToSourceDirectory
		{
			get
			{
				return OutputToSourceDirectory_Field;
			}
			
			set
			{
				OutputToSourceDirectory_Field = value;
				DatabaseConfig.SetConfigValue("OutputToSourceDirectory", value);
			}
		}
		private static bool PreserveFolderStructureInBatch_Field;
		public static bool PreserveFolderStructureInBatch
		{
			get
			{
				return PreserveFolderStructureInBatch_Field;
			}
			
			set
			{
				PreserveFolderStructureInBatch_Field = value;
				DatabaseConfig.SetConfigValue("PreserveFolderStructureInBatch", value);
			}
		}
		private static string WhenFileExists_Field;
		public static string WhenFileExists
		{
			get
			{
				return WhenFileExists_Field;
			}
			
			set
			{
				WhenFileExists_Field = value;
				DatabaseConfig.SetConfigValue("WhenFileExists", value);
			}
		}
		private static string WhenFileExistsBatch_Field;
		public static string WhenFileExistsBatch
		{
			get
			{
				return WhenFileExistsBatch_Field;
			}
			
			set
			{
				WhenFileExistsBatch_Field = value;
				DatabaseConfig.SetConfigValue("WhenFileExistsBatch", value);
			}
		}
		private static bool KeepScansAfterCompletion_Field;
		public static bool KeepScansAfterCompletion
		{
			get
			{
				return KeepScansAfterCompletion_Field;
			}
			
			set
			{
				KeepScansAfterCompletion_Field = value;
				DatabaseConfig.SetConfigValue("KeepScansAfterCompletion", value);
			}
		}
		private static bool UseCustomVideoPlayer_Field;
		public static bool UseCustomVideoPlayer
		{
			get
			{
				return UseCustomVideoPlayer_Field;
			}
			
			set
			{
				UseCustomVideoPlayer_Field = value;
				DatabaseConfig.SetConfigValue("UseCustomVideoPlayer", value);
			}
		}
		private static string CustomVideoPlayer_Field;
		public static string CustomVideoPlayer
		{
			get
			{
				return CustomVideoPlayer_Field;
			}
			
			set
			{
				CustomVideoPlayer_Field = value;
				DatabaseConfig.SetConfigValue("CustomVideoPlayer", value);
			}
		}
		private static bool PlaySoundOnCompletion_Field;
		public static bool PlaySoundOnCompletion
		{
			get
			{
				return PlaySoundOnCompletion_Field;
			}
			
			set
			{
				PlaySoundOnCompletion_Field = value;
				DatabaseConfig.SetConfigValue("PlaySoundOnCompletion", value);
			}
		}
		private static bool UseCustomCompletionSound_Field;
		public static bool UseCustomCompletionSound
		{
			get
			{
				return UseCustomCompletionSound_Field;
			}
			
			set
			{
				UseCustomCompletionSound_Field = value;
				DatabaseConfig.SetConfigValue("UseCustomCompletionSound", value);
			}
		}
		private static string CustomCompletionSound_Field;
		public static string CustomCompletionSound
		{
			get
			{
				return CustomCompletionSound_Field;
			}
			
			set
			{
				CustomCompletionSound_Field = value;
				DatabaseConfig.SetConfigValue("CustomCompletionSound", value);
			}
		}
		private static string LastPlayer_Field;
		public static string LastPlayer
		{
			get
			{
				return LastPlayer_Field;
			}
			
			set
			{
				LastPlayer_Field = value;
				DatabaseConfig.SetConfigValue("LastPlayer", value);
			}
		}
		private static bool QueueTitlesUseDirectoryOverride_Field;
		public static bool QueueTitlesUseDirectoryOverride
		{
			get
			{
				return QueueTitlesUseDirectoryOverride_Field;
			}
			
			set
			{
				QueueTitlesUseDirectoryOverride_Field = value;
				DatabaseConfig.SetConfigValue("QueueTitlesUseDirectoryOverride", value);
			}
		}
		private static string QueueTitlesDirectoryOverride_Field;
		public static string QueueTitlesDirectoryOverride
		{
			get
			{
				return QueueTitlesDirectoryOverride_Field;
			}
			
			set
			{
				QueueTitlesDirectoryOverride_Field = value;
				DatabaseConfig.SetConfigValue("QueueTitlesDirectoryOverride", value);
			}
		}
		private static bool QueueTitlesUseNameOverride_Field;
		public static bool QueueTitlesUseNameOverride
		{
			get
			{
				return QueueTitlesUseNameOverride_Field;
			}
			
			set
			{
				QueueTitlesUseNameOverride_Field = value;
				DatabaseConfig.SetConfigValue("QueueTitlesUseNameOverride", value);
			}
		}
		private static string QueueTitlesNameOverride_Field;
		public static string QueueTitlesNameOverride
		{
			get
			{
				return QueueTitlesNameOverride_Field;
			}
			
			set
			{
				QueueTitlesNameOverride_Field = value;
				DatabaseConfig.SetConfigValue("QueueTitlesNameOverride", value);
			}
		}
		private static bool DxvaDecoding_Field;
		public static bool DxvaDecoding
		{
			get
			{
				return DxvaDecoding_Field;
			}
			
			set
			{
				DxvaDecoding_Field = value;
				DatabaseConfig.SetConfigValue("DxvaDecoding", value);
			}
		}
		private static bool EnableLibDvdNav_Field;
		public static bool EnableLibDvdNav
		{
			get
			{
				return EnableLibDvdNav_Field;
			}
			
			set
			{
				EnableLibDvdNav_Field = value;
				DatabaseConfig.SetConfigValue("EnableLibDvdNav", value);
			}
		}
		private static int MinimumTitleLengthSeconds_Field;
		public static int MinimumTitleLengthSeconds
		{
			get
			{
				return MinimumTitleLengthSeconds_Field;
			}
			
			set
			{
				MinimumTitleLengthSeconds_Field = value;
				DatabaseConfig.SetConfigValue("MinimumTitleLengthSeconds", value);
			}
		}
		private static bool DeleteSourceFilesOnClearingCompleted_Field;
		public static bool DeleteSourceFilesOnClearingCompleted
		{
			get
			{
				return DeleteSourceFilesOnClearingCompleted_Field;
			}
			
			set
			{
				DeleteSourceFilesOnClearingCompleted_Field = value;
				DatabaseConfig.SetConfigValue("DeleteSourceFilesOnClearingCompleted", value);
			}
		}
		private static bool PreserveModifyTimeFiles_Field;
		public static bool PreserveModifyTimeFiles
		{
			get
			{
				return PreserveModifyTimeFiles_Field;
			}
			
			set
			{
				PreserveModifyTimeFiles_Field = value;
				DatabaseConfig.SetConfigValue("PreserveModifyTimeFiles", value);
			}
		}
		private static bool ResumeEncodingOnRestart_Field;
		public static bool ResumeEncodingOnRestart
		{
			get
			{
				return ResumeEncodingOnRestart_Field;
			}
			
			set
			{
				ResumeEncodingOnRestart_Field = value;
				DatabaseConfig.SetConfigValue("ResumeEncodingOnRestart", value);
			}
		}
		private static bool UseWorkerProcess_Field;
		public static bool UseWorkerProcess
		{
			get
			{
				return UseWorkerProcess_Field;
			}
			
			set
			{
				UseWorkerProcess_Field = value;
				DatabaseConfig.SetConfigValue("UseWorkerProcess", value);
			}
		}
		private static bool RememberPreviousFiles_Field;
		public static bool RememberPreviousFiles
		{
			get
			{
				return RememberPreviousFiles_Field;
			}
			
			set
			{
				RememberPreviousFiles_Field = value;
				DatabaseConfig.SetConfigValue("RememberPreviousFiles", value);
			}
		}
		private static string VideoFileExtensions_Field;
		public static string VideoFileExtensions
		{
			get
			{
				return VideoFileExtensions_Field;
			}
			
			set
			{
				VideoFileExtensions_Field = value;
				DatabaseConfig.SetConfigValue("VideoFileExtensions", value);
			}
		}
		private static string PreferredPlayer_Field;
		public static string PreferredPlayer
		{
			get
			{
				return PreferredPlayer_Field;
			}
			
			set
			{
				PreferredPlayer_Field = value;
				DatabaseConfig.SetConfigValue("PreferredPlayer", value);
			}
		}
		private static bool BetaUpdates_Field;
		public static bool BetaUpdates
		{
			get
			{
				return BetaUpdates_Field;
			}
			
			set
			{
				BetaUpdates_Field = value;
				DatabaseConfig.SetConfigValue("BetaUpdates", value);
			}
		}
		private static string InterfaceLanguageCode_Field;
		public static string InterfaceLanguageCode
		{
			get
			{
				return InterfaceLanguageCode_Field;
			}
			
			set
			{
				InterfaceLanguageCode_Field = value;
				DatabaseConfig.SetConfigValue("InterfaceLanguageCode", value);
			}
		}
		private static string AudioLanguageCode_Field;
		public static string AudioLanguageCode
		{
			get
			{
				return AudioLanguageCode_Field;
			}
			
			set
			{
				AudioLanguageCode_Field = value;
				DatabaseConfig.SetConfigValue("AudioLanguageCode", value);
			}
		}
		private static string SubtitleLanguageCode_Field;
		public static string SubtitleLanguageCode
		{
			get
			{
				return SubtitleLanguageCode_Field;
			}
			
			set
			{
				SubtitleLanguageCode_Field = value;
				DatabaseConfig.SetConfigValue("SubtitleLanguageCode", value);
			}
		}
		private static string AutoAudio_Field;
		public static string AutoAudio
		{
			get
			{
				return AutoAudio_Field;
			}
			
			set
			{
				AutoAudio_Field = value;
				DatabaseConfig.SetConfigValue("AutoAudio", value);
			}
		}
		private static string AutoSubtitle_Field;
		public static string AutoSubtitle
		{
			get
			{
				return AutoSubtitle_Field;
			}
			
			set
			{
				AutoSubtitle_Field = value;
				DatabaseConfig.SetConfigValue("AutoSubtitle", value);
			}
		}
		private static bool AutoAudioAll_Field;
		public static bool AutoAudioAll
		{
			get
			{
				return AutoAudioAll_Field;
			}
			
			set
			{
				AutoAudioAll_Field = value;
				DatabaseConfig.SetConfigValue("AutoAudioAll", value);
			}
		}
		private static bool AutoSubtitleAll_Field;
		public static bool AutoSubtitleAll
		{
			get
			{
				return AutoSubtitleAll_Field;
			}
			
			set
			{
				AutoSubtitleAll_Field = value;
				DatabaseConfig.SetConfigValue("AutoSubtitleAll", value);
			}
		}
		private static bool AutoSubtitleOnlyIfDifferent_Field;
		public static bool AutoSubtitleOnlyIfDifferent
		{
			get
			{
				return AutoSubtitleOnlyIfDifferent_Field;
			}
			
			set
			{
				AutoSubtitleOnlyIfDifferent_Field = value;
				DatabaseConfig.SetConfigValue("AutoSubtitleOnlyIfDifferent", value);
			}
		}
		private static bool AutoSubtitleBurnIn_Field;
		public static bool AutoSubtitleBurnIn
		{
			get
			{
				return AutoSubtitleBurnIn_Field;
			}
			
			set
			{
				AutoSubtitleBurnIn_Field = value;
				DatabaseConfig.SetConfigValue("AutoSubtitleBurnIn", value);
			}
		}
		private static bool AutoSubtitleLanguageDefault_Field;
		public static bool AutoSubtitleLanguageDefault
		{
			get
			{
				return AutoSubtitleLanguageDefault_Field;
			}
			
			set
			{
				AutoSubtitleLanguageDefault_Field = value;
				DatabaseConfig.SetConfigValue("AutoSubtitleLanguageDefault", value);
			}
		}
		private static bool AutoSubtitleLanguageBurnIn_Field;
		public static bool AutoSubtitleLanguageBurnIn
		{
			get
			{
				return AutoSubtitleLanguageBurnIn_Field;
			}
			
			set
			{
				AutoSubtitleLanguageBurnIn_Field = value;
				DatabaseConfig.SetConfigValue("AutoSubtitleLanguageBurnIn", value);
			}
		}
		private static int QueueTitlesStartTime_Field;
		public static int QueueTitlesStartTime
		{
			get
			{
				return QueueTitlesStartTime_Field;
			}
			
			set
			{
				QueueTitlesStartTime_Field = value;
				DatabaseConfig.SetConfigValue("QueueTitlesStartTime", value);
			}
		}
		private static int QueueTitlesEndTime_Field;
		public static int QueueTitlesEndTime
		{
			get
			{
				return QueueTitlesEndTime_Field;
			}
			
			set
			{
				QueueTitlesEndTime_Field = value;
				DatabaseConfig.SetConfigValue("QueueTitlesEndTime", value);
			}
		}
		private static bool QueueTitlesUseRange_Field;
		public static bool QueueTitlesUseRange
		{
			get
			{
				return QueueTitlesUseRange_Field;
			}
			
			set
			{
				QueueTitlesUseRange_Field = value;
				DatabaseConfig.SetConfigValue("QueueTitlesUseRange", value);
			}
		}
	}
}