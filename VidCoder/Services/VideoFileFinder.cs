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
using VidCoderCommon.Utilities;

namespace VidCoder.Services;

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
	public List<SourcePathWithMetadata> GetPathList(IList<string> itemList, Picker picker = null)
	{
		if (picker == null)
		{
			picker = this.PickersService.SelectedPicker.Picker;
		}

		var pathList = new List<SourcePathWithMetadata>();

		var fileFilter = new PickerFileFilter
		{
			Extensions = CommonFileUtilities.ParseVideoExtensionList(picker.VideoFileExtensions),
			IgnoreFilesBelowMb = picker.IgnoreFilesBelowMbEnabled ? picker.IgnoreFilesBelowMb : 0
		};

		if (CustomConfig.DragDropOrder == DragDropOrder.Alphabetical)
		{
			var files = new List<string>();
			var folders = new List<string>();

			// First make a pass and assign each path to the files or folders list
			foreach (string item in itemList)
			{
				try
				{
					if (CommonFileUtilities.IsDirectory(item))
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
				AddDirectoryToPathList(folder, pathList, fileFilter);
			}

			// Now add all files
			foreach (string file in files)
			{
				// Path is a file
				pathList.Add(new SourcePathWithMetadata { Path = file, SourceType = SourceType.File });
			}
		}
		else
		{
			// Selected order
			foreach (string item in itemList)
			{
				try
				{
					if (CommonFileUtilities.IsDirectory(item))
					{
						AddDirectoryToPathList(item, pathList, fileFilter);
					}
					else
					{
						// Path is a file
						pathList.Add(new SourcePathWithMetadata { Path = item, SourceType = SourceType.File });
					}
				}
				catch (Exception exception)
				{
					StaticResolver.Resolve<IAppLogger>().LogError($"Could not process {item} : " + Environment.NewLine + exception);
				}
			}
		}

		return pathList;
	}

	/// <summary>
	/// Adds the contents of the given folder to the path list. Determines if the folder is a disc folder.
	/// </summary>
	/// <param name="folder">The folder to add.</param>
	/// <param name="fileFilter">The filter to use.</param>
	private static void AddDirectoryToPathList(string folder, List<SourcePathWithMetadata> pathList, PickerFileFilter fileFilter)
	{
		string parentFolder = Path.GetDirectoryName(folder);
		(List<SourcePathWithType> paths, List<string> inacessibleDirectories) = CommonFileUtilities.GetFilesOrVideoFolders(folder, fileFilter);

		if (inacessibleDirectories.Count > 0)
		{
			var messageBuilder = new StringBuilder(CommonRes.CouldNotAccessDirectoriesError + Environment.NewLine);
			foreach (string accessError in inacessibleDirectories)
			{
				messageBuilder.AppendLine(accessError);
			}

			MessageBox.Show(messageBuilder.ToString());
		}

		pathList.AddRange(
			paths
				.Select(p => new SourcePathWithMetadata
				{
					Path = p.Path,
					ParentFolder = parentFolder,
					SourceType = p.SourceType
				}));
	}
}
