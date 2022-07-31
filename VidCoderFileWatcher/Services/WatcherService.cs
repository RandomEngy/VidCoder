using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VidCoderCommon.Model;
using VidCoderCommon.Services;
using VidCoderCommon.Utilities;
using VidCoderFileWatcher.Model;

namespace VidCoderFileWatcher.Services
{
	public class WatcherService : IWatcherCommands
	{
		private readonly IBasicLogger logger;
		private Dictionary<string, StrippedPicker> pickers;
		private static readonly StrippedPicker DefaultPicker = new StrippedPicker
		{
			Name = "Default",
			IgnoreFilesBelowMbEnabled = false,
			VideoFileExtensions = PickerDefaults.VideoFileExtensions
		};

		public WatcherService(IBasicLogger logger)
		{
			this.logger = logger;
			this.RefreshPickers();
		}

		public void RefreshFromWatchedFolders()
		{
			this.logger.Log("Refreshing file list...");
			IDictionary<string, MarkableFile> existingFiles = this.GetFilesInWatchedFolders();
			Dictionary<string, WatchedFile> entries = WatcherStorage.GetWatchedFiles(WatcherDatabase.Connection);

			// Go over all entries, marking any matches along the way
			var entriesToRemove = new List<string>();
			foreach (string path in entries.Keys)
			{
				if (existingFiles.TryGetValue(path, out MarkableFile file))
				{
					file.HasEntry = true;
				}
				else
				{
					// The file for this entry no longer exists. We can clean up the entry.
					entriesToRemove.Add(path);
				}
			}

			if (entriesToRemove.Count > 0)
			{
				this.logger.Log("Removing entries for file(s) that no longer exist:");
				foreach (string path in entriesToRemove)
				{
					this.logger.Log("    " + path);
				}

				WatcherStorage.RemoveEntries(WatcherDatabase.Connection, entriesToRemove);
			}

			// Any files without matching entries are new and should be enqueued
			var filesToEnqueue = new List<string>();
			foreach (string path in existingFiles.Keys)
			{
				if (!existingFiles[path].HasEntry)
				{
					filesToEnqueue.Add(path);
				}
			}

			if (filesToEnqueue.Count > 0)
			{
				this.logger.Log("Sending video(s) to be queued:");
				foreach (string path in filesToEnqueue)
				{
					this.logger.Log("    " + path);
				}

				WatcherStorage.AddEntries(WatcherDatabase.Connection, filesToEnqueue);
				// TODO - signal main VidCoder process to start these
			}
		}

		/// <summary>
		/// Gets the source paths (files or folders) in the currently watched folders.
		/// </summary>
		/// <returns>The source paths in the currently watched folders.</returns>
		private IDictionary<string, MarkableFile> GetFilesInWatchedFolders()
		{
			var files = new Dictionary<string, MarkableFile>(StringComparer.OrdinalIgnoreCase);
			List<WatchedFolder> folders = WatcherStorage.GetWatchedFolders(WatcherDatabase.Connection);

			foreach (WatchedFolder folder in folders)
			{
				this.logger.Log("Enumerating folder: " + folder.Path);

				StrippedPicker picker;
				if (this.pickers.ContainsKey(folder.Picker))
				{
					picker = this.pickers[folder.Picker];
				}
				else
				{
					picker = DefaultPicker;
				}

				ISet<string> videoExtensions = CommonFileUtilities.ParseVideoExtensionList(picker.VideoFileExtensions);
				(List<SourcePathWithType> sourcePaths, List<string> inacessibleDirectories) = CommonFileUtilities.GetFilesOrVideoFolders(
					folder.Path, videoExtensions,
					picker.IgnoreFilesBelowMbEnabled ? picker.IgnoreFilesBelowMb : 0);

				foreach (string inaccessibleDirectory in inacessibleDirectories)
				{
					this.logger.Log("Could not access directory: " + inaccessibleDirectory);
				}

				foreach (SourcePathWithType sourcePath in sourcePaths)
				{
					this.logger.Log("    " + sourcePath.Path);
					files.Add(sourcePath.Path, new MarkableFile { Path = sourcePath.Path });
				}
			}

			return files;
		}

		public void Ping()
		{
		}

		private void RefreshPickers()
		{
			// TODO - thread protection
			this.pickers = new Dictionary<string, StrippedPicker>();
			foreach (StrippedPicker picker in WatcherRepository.GetPickerList())
			{
				this.pickers.Add(picker.Name, picker);
			}
		}

		private class MarkableFile
		{
			public string Path { get; set; }

			public bool HasEntry { get; set; }
		}
	}
}
