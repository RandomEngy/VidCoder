using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VidCoder
{
	using System.Data.SQLite;
	using Model;
	using Properties;

	public static class ConfigMigration
	{
		public static void MigrateConfigSettings()
		{
			using (SQLiteTransaction transaction = Database.ThreadLocalConnection.BeginTransaction())
			{
				Config.EncodingDialogLastTab = Settings.Default.EncodingDialogLastTab;
				Config.LastOutputFolder = Settings.Default.LastOutputFolder;
				Config.LastInputFileFolder = Settings.Default.LastInputFileFolder;
				Config.LastVideoTSFolder = Settings.Default.LastVideoTSFolder;
				Config.LastSrtFolder = Settings.Default.LastSrtFolder;
				Config.LastPresetIndex = Settings.Default.LastPresetIndex;
				Config.AutoNameOutputFolder = Settings.Default.AutoNameOutputFolder;
				Config.AutoNameCustomFormat = Settings.Default.AutoNameCustomFormat;
				Config.AutoNameCustomFormatString = Settings.Default.AutoNameCustomFormatString;
				Config.NativeLanguageCode = Settings.Default.NativeLanguageCode;
				Config.DubAudio = Settings.Default.DubAudio;
				Config.OptionsDialogLastTab = Settings.Default.OptionsDialogLastTab;
				Config.LastCsvFolder = Settings.Default.LastCsvFolder;
				Config.MainWindowPlacement = Settings.Default.MainWindowPlacement;
				Config.SubtitlesDialogPlacement = Settings.Default.SubtitlesDialogPlacement;
				Config.EncodingDialogPlacement = Settings.Default.EncodingDialogPlacement;
				Config.ChapterMarkersDialogPlacement = Settings.Default.ChapterMarkersDialogPlacement;
				Config.PreviewWindowPlacement = Settings.Default.PreviewWindowPlacement;
				Config.PreviewSeconds = Settings.Default.PreviewSeconds;
				Config.EncodingWindowOpen = Settings.Default.EncodingWindowOpen;
				Config.PreviewWindowOpen = Settings.Default.PreviewWindowOpen;
				Config.UpdatesEnabled = Settings.Default.UpdatesEnabled;
				Config.ApplicationVersion = Settings.Default.ApplicationVersion;
				Config.LogWindowPlacement = Settings.Default.LogWindowPlacement;
				Config.LogWindowOpen = Settings.Default.LogWindowOpen;
				Config.QueueColumns = Settings.Default.QueueColumns;
				Config.QueueLastColumnWidth = Settings.Default.QueueLastColumnWidth;
				Config.QueueTitlesDialogPlacement = Settings.Default.QueueTitlesDialogPlacement;
				Config.CompletedColumnWidths = Settings.Default.CompletedColumnWidths;
				Config.LogVerbosity = Settings.Default.LogVerbosity;
				Config.AddAutoPauseProcessDialogPlacement = Settings.Default.AddAutoPauseProcessDialogPlacement;
				Config.PreviewCount = Settings.Default.PreviewCount;
				Config.QueueTitlesUseTitleOverride = Settings.Default.QueueTitlesUseTitleOverride;
				Config.QueueTitlesTitleOverride = Settings.Default.QueueTitlesTitleOverride;
				Config.ShowAudioTrackNameField = Settings.Default.ShowAudioTrackNameField;
				Config.LastPresetExportFolder = Settings.Default.LastPresetExportFolder;
				Config.SourceHistory = Settings.Default.SourceHistory;
				Config.MinimizeToTray = Settings.Default.MinimizeToTray;
				Config.OutputToSourceDirectory = Settings.Default.OutputToSourceDirectory;
				CustomConfig.WhenFileExists = Settings.Default.WhenFileExists;
				CustomConfig.WhenFileExistsBatch = Settings.Default.WhenFileExistsBatch;
				Config.KeepScansAfterCompletion = Settings.Default.KeepScansAfterCompletion;
				Config.PlaySoundOnCompletion = Settings.Default.PlaySoundOnCompletion;
				Config.LastPlayer = Settings.Default.LastPlayer;
				Config.QueueTitlesUseNameOverride = Settings.Default.QueueTitlesUseNameOverride;
				Config.QueueTitlesNameOverride = Settings.Default.QueueTitlesNameOverride;
				Config.QueueTitlesDialogPlacement2 = Settings.Default.QueueTitlesDialogPlacement2;
				Config.EnableLibDvdNav = Settings.Default.EnableLibDvdNav;
				Config.MinimumTitleLengthSeconds = Settings.Default.MinimumTitleLengthSeconds;
				Config.DeleteSourceFilesOnClearingCompleted = Settings.Default.DeleteSourceFilesOnClearingCompleted;
				Config.RememberPreviousFiles = Settings.Default.RememberPreviousFiles;
				Config.VideoFileExtensions = Settings.Default.VideoFileExtensions;
				Config.PreferredPlayer = Settings.Default.PreferredPlayer;
				Config.BetaUpdates = Settings.Default.BetaUpdates;
				Config.InterfaceLanguageCode = Settings.Default.InterfaceLanguageCode;

				if (Settings.Default.AutoPauseProcesses != null)
				{
					var autoPauseList = Settings.Default.AutoPauseProcesses.Cast<string>().ToList();
					CustomConfig.AutoPauseProcesses = autoPauseList;
				}

				Config.MigratedConfigs = true;

				transaction.Commit();
			}
		}
	}
}
