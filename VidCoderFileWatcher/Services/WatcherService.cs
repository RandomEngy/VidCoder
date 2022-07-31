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
					this.logger.Log("Found video file: " + sourcePath.Path);
				}
			}
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
	}
}
