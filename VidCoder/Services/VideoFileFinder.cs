using Microsoft.AnyContainer;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using VidCoder.Model;
using VidCoder.Resources;
using VidCoderCommon.Model;

namespace VidCoder.Services
{
	/// <summary>
	/// Looks for video files inside of folders.
	/// </summary>
	public class VideoFileFinder
	{
		public PickersService PickersService { get; } = StaticResolver.Resolve<PickersService>();

		/// <summary>
		/// Takes a list of file/folder paths and returns a list of valid video sources in them.
		/// </summary>
		/// <param name="itemList">The list of paths to process.</param>
		/// <param name="picker">The picker to use.</param>
		/// <returns>The list of video sources in the given paths.</returns>
		public List<SourcePath> GetPathList(IList<string> itemList, Picker picker = null)
		{
			var videoExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			string extensionsString = picker.VideoFileExtensions;
			string[] rawExtensions = extensionsString.Split(',', ';');
			foreach (string rawExtension in rawExtensions)
			{
				string extension = rawExtension.Trim();
				if (extension.Length > 0)
				{
					if (!extension.StartsWith("."))
					{
						extension = "." + extension;
					}

					videoExtensions.Add(extension);
				}
			}

			var pathList = new List<SourcePath>();

			if (CustomConfig.DragDropOrder == DragDropOrder.Alphabetical)
			{
				var files = new List<string>();
				var folders = new List<string>();

				// First make a pass and assign each path to the files or folders list
				foreach (string item in itemList)
				{
					try
					{
						if (FileUtilities.IsDirectory(item))
						{
							folders.Add(item);
						}
						else
						{
							// Path is a file
							files.Add(item);
						}
					}
					catch (Exception exception)
					{
						StaticResolver.Resolve<IAppLogger>().LogError($"Could not process {item} : " + Environment.NewLine + exception);
					}
				}

				// Sort the file and folder lists.
				files.Sort();
				folders.Sort();


				// Now add all folders
				foreach (string folder in folders)
				{
					AddDirectoryToPathList(folder, pathList, videoExtensions);
				}

				// Now add all files
				foreach (string file in files)
				{
					// Path is a file
					pathList.Add(new SourcePath { Path = file, SourceType = SourceType.File });
				}
			}
			else
			{
				// Selected order
				foreach (string item in itemList)
				{
					try
					{
						if (FileUtilities.IsDirectory(item))
						{
							AddDirectoryToPathList(item, pathList, videoExtensions);
						}
						else
						{
							// Path is a file
							pathList.Add(new SourcePath { Path = item, SourceType = SourceType.File });
						}
					}
					catch (Exception exception)
					{
						StaticResolver.Resolve<IAppLogger>().LogError($"Could not process {item} : " + Environment.NewLine + exception);
					}
				}
			}

			if (picker == null)
			{
				picker = this.PickersService.SelectedPicker.Picker;
			}

			if (picker.IgnoreFilesBelowMbEnabled)
			{
				long bytesThreshold = picker.IgnoreFilesBelowMb * 1024 * 1024;
				var filteredList = new List<SourcePath>();
				pathList = pathList.Where(p =>
				{
					if (p.SourceType != SourceType.File)
					{
						return true;
					}

					try
					{
						FileInfo info = new FileInfo(p.Path);
						return info.Length >= bytesThreshold;
					}
					catch
					{
						// Ignore errors, and include the file.
						return true;
					}
				}).ToList();
			}

			return pathList;
		}

		/// <summary>
		/// Adds the contents of the given folder to the path list. Determines if the folder is a disc folder.
		/// </summary>
		/// <param name="folder">The folder to add.</param>
		/// <param name="pathList">The path list to add to.</param>
		/// <param name="videoExtensions">The set of valid video extensions.</param>
		private static void AddDirectoryToPathList(string folder, List<SourcePath> pathList, HashSet<string> videoExtensions)
		{
			if (Utilities.IsDiscFolder(folder))
			{
				// If it's a disc folder, add it
				pathList.Add(new SourcePath { Path = Utilities.EnsureVideoTsFolder(folder), SourceType = SourceType.DiscVideoFolder });
			}
			else
			{
				string parentFolder = Path.GetDirectoryName(folder);
				pathList.AddRange(
					GetFilesOrVideoFolders(folder, videoExtensions)
						.Select(p => new SourcePath
						{
							Path = p.Path,
							ParentFolder = parentFolder,
							SourceType = p.SourceType
						}));
			}
		}

		private class SourcePathWithType
		{
			public string Path { get; set; }
			public SourceType SourceType { get; set; }
		}

		private static List<SourcePathWithType> GetFilesOrVideoFolders(string directory, ISet<string> videoExtensions)
		{
			var paths = new List<SourcePathWithType>();
			var errors = new List<string>();
			GetFilesOrVideoFoldersRecursive(directory, paths, errors, videoExtensions);

			if (errors.Count > 0)
			{
				var messageBuilder = new StringBuilder(CommonRes.CouldNotAccessDirectoriesError + Environment.NewLine);
				foreach (string accessError in errors)
				{
					messageBuilder.AppendLine(accessError);
				}

				MessageBox.Show(messageBuilder.ToString());
			}

			return paths;
		}

		private static void GetFilesOrVideoFoldersRecursive(string directory, List<SourcePathWithType> paths, List<string> errors, ISet<string> videoExtensions)
		{
			try
			{
				string[] subdirectories = Directory.GetDirectories(directory);
				Array.Sort(subdirectories);
				foreach (string subdirectory in subdirectories)
				{
					if (Utilities.IsDiscFolder(subdirectory))
					{
						paths.Add(new SourcePathWithType { Path = subdirectory, SourceType = SourceType.DiscVideoFolder });
					}
					else
					{
						GetFilesOrVideoFoldersRecursive(subdirectory, paths, errors, videoExtensions);
					}
				}
			}
			catch (Exception)
			{
				errors.Add(directory);
			}

			try
			{
				string[] files = Directory.GetFiles(directory);
				Array.Sort(files);
				paths.AddRange(files
					.Where(file =>
					{
						return videoExtensions.Contains(Path.GetExtension(file));
					})
					.Select(path => new SourcePathWithType { Path = path, SourceType = SourceType.File }));
			}
			catch (Exception)
			{
				errors.Add(directory);
			}
		}
	}
}
