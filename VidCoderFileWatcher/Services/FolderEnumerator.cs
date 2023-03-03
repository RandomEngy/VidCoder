using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using VidCoderCommon.Model;
using VidCoderCommon.Utilities;
using VidCoderFileWatcher.Model;

namespace VidCoderFileWatcher.Services;

public class FolderEnumerator
{
	private readonly IEnumerable<WatchedFolder> folders;
	private readonly WatcherService watcherService;
	private readonly IBasicLogger logger;
	private readonly bool logVerbose;

	public FolderEnumerator(IEnumerable<WatchedFolder> folders, WatcherService watcherService, IBasicLogger logger, bool logVerbose)
	{
		this.folders = folders;
		this.watcherService = watcherService;
		this.logger = logger;
		this.logVerbose = logVerbose;
	}

	public async Task CheckFolders()
	{
		IDictionary<string, ExistingFile> existingFiles = this.GetFilesInWatchedFolders();
		Dictionary<string, WatchedFile> entries = WatcherStorage.GetWatchedFiles(WatcherDatabase.Connection);

		// Go over all entries, marking any matches along the way
		var entriesToRemove = new List<string>();
		foreach (string path in entries.Keys)
		{
			if (existingFiles.TryGetValue(path, out ExistingFile? file))
			{
				file.HasEntry = true;
			}
			else
			{
				// The file for this entry no longer exists. We can clean up the entry.
				entriesToRemove.Add(path);
			}
		}

		// Remove any entries without a file match.
		if (entriesToRemove.Count > 0)
		{
			this.logger.Log("Removing entries for file(s) that no longer exist:");
			foreach (string path in entriesToRemove)
			{
				this.logger.Log("    " + path);
			}

			WatcherStorage.RemoveEntries(WatcherDatabase.Connection, entriesToRemove);

			await this.watcherService.NotifyMainProcessOfFilesRemoved(entriesToRemove);
		}

		// Any files without matching entries are new and should be enqueued
		var filesToEnqueue = new Dictionary<WatchedFolder, List<string>>();
		foreach (string path in existingFiles.Keys)
		{
			ExistingFile file = existingFiles[path];
			if (!file.HasEntry)
			{
				if (!filesToEnqueue.ContainsKey(file.Folder))
				{
					filesToEnqueue.Add(file.Folder, new List<string>());
				}

				filesToEnqueue[file.Folder].Add(file.Path);
			}
		}

		foreach (KeyValuePair<WatchedFolder, List<string>> pair in filesToEnqueue)
		{
			await this.watcherService.QueueInMainProcess(pair.Key, pair.Value).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Gets the source paths (files or folders) in the currently watched folders.
	/// </summary>
	/// <param name="log">True if this should log routine information.</param>
	/// <returns>The source paths in the currently watched folders.</returns>
	private IDictionary<string, ExistingFile> GetFilesInWatchedFolders()
	{
		var files = new Dictionary<string, ExistingFile>(StringComparer.OrdinalIgnoreCase);

		foreach (WatchedFolder folder in this.folders)
		{
			if (this.logVerbose)
			{
				this.logger.Log("Enumerating folder: " + folder.Path);
			}

			List<SourcePathWithType> sourcePaths = this.GetPathsInWatchedFolder(folder.Path, folder);

			foreach (SourcePathWithType sourcePath in sourcePaths)
			{
				if (this.logVerbose)
				{ 
					this.logger.Log("    " + sourcePath.Path);
				}

				files.Add(
					sourcePath.Path,
					new ExistingFile(sourcePath.Path, folder));
			}
		}

		return files;
	}

	/// <summary>
	/// Gets the source paths (files or folder) in the given watched folder.
	/// </summary>
	/// <param name="path">The path to look under.</param>
	/// <param name="watchedFolder">The folder to look in.</param>
	/// <returns>The source paths in the folder.</returns>
	public List<SourcePathWithType> GetPathsInWatchedFolder(string path, WatchedFolder watchedFolder)
	{
		(List<SourcePathWithType> sourcePaths, List<string> inacessibleDirectories) = CommonFileUtilities.GetFilesOrVideoFolders(
			path,
			this.watcherService.GetFileFilterForWatchedFolder(watchedFolder));

		foreach (string inaccessibleDirectory in inacessibleDirectories)
		{
			this.logger.Log("Could not access directory: " + inaccessibleDirectory);
		}

		return sourcePaths;
	}

	private class ExistingFile
	{
		public ExistingFile(string path, WatchedFolder folder)
		{
			this.Path = path;
			this.Folder = folder;
		}

		public string Path { get; set; }

		public bool HasEntry { get; set; }

		public WatchedFolder Folder { get; set; }
	}
}
