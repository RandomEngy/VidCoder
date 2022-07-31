using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VidCoderCommon.Model;

namespace VidCoderCommon.Utilities
{
	public static class CommonFileUtilities
	{
		public static (List<SourcePathWithType> paths, List<string> inacessibleDirectories) GetFilesOrVideoFolders(string directory, ISet<string> videoExtensions, int ignoreFilesBelowMb)
		{
			var paths = new List<SourcePathWithType>();
			var inacessibleDirectories = new List<string>();

			try
			{
				DirectoryInfo directoryInfo = new DirectoryInfo(directory);
				GetFilesOrVideoFoldersRecursive(directoryInfo, paths, inacessibleDirectories, videoExtensions, ignoreFilesBelowMb);
			}
			catch
			{
				inacessibleDirectories.Add(directory);
			}

			return (paths, inacessibleDirectories);
		}

		private static void GetFilesOrVideoFoldersRecursive(DirectoryInfo directory, List<SourcePathWithType> paths, List<string> inacessibleDirectories, ISet<string> videoExtensions, int ignoreFilesBelowMb)
		{
			if (IsDiscFolder(directory.FullName))
			{
				paths.Add(new SourcePathWithType { Path = EnsureVideoTsFolder(directory.FullName), SourceType = SourceType.DiscVideoFolder });
				return;
			}

			try
			{
				DirectoryInfo[] subdirectories = directory.GetDirectories();
				Array.Sort(subdirectories, (a, b) => a.Name.CompareTo(b.Name));
				foreach (DirectoryInfo subdirectory in subdirectories)
				{
					GetFilesOrVideoFoldersRecursive(subdirectory, paths, inacessibleDirectories, videoExtensions, ignoreFilesBelowMb);
				}

				FileInfo[] files = directory.GetFiles();
				Array.Sort(files, (a, b) => a.Name.CompareTo(b.Name));
				paths.AddRange(files
					.Where(file =>
					{
						return videoExtensions.Contains(file.Extension) && file.Length >= ignoreFilesBelowMb * 1024 * 1024;
					})
					.Select(path => new SourcePathWithType { Path = path.FullName, SourceType = SourceType.File }));
			}
			catch (Exception)
			{
				inacessibleDirectories.Add(directory.FullName);
			}
		}

		public static FolderType GetFolderType(string directory)
		{
			try
			{
				var directoryInfo = new DirectoryInfo(directory);
				if (!directoryInfo.Exists)
				{
					return FolderType.NonExistent;
				}

				if (File.Exists(Path.Combine(directory, @"VIDEO_TS.IFO")) || File.Exists(Path.Combine(directory, @"VIDEO_TS\VIDEO_TS.IFO")))
				{
					return FolderType.Dvd;
				}

				if (Directory.Exists(Path.Combine(directory, "BDMV")))
				{
					return FolderType.BluRay;
				}
			}
			catch
			{
			}

			return FolderType.VideoFiles;
		}

		public static bool IsDiscFolder(string directory)
		{
			FolderType folderType = GetFolderType(directory);
			return folderType == FolderType.Dvd || folderType == FolderType.BluRay;
		}

		public static string EnsureVideoTsFolder(string directory)
		{
			string videoTsPath = Path.Combine(directory, "VIDEO_TS");
			if (Directory.Exists(videoTsPath))
			{
				return videoTsPath;
			}

			return directory;
		}
	}
}
