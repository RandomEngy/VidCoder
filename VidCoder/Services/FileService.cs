using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.IO;
using System.ComponentModel;
using System.Windows;
using Microsoft.AnyContainer;
using Ookii.Dialogs.Wpf;

namespace VidCoder.Services
{
	using Resources;

	public class FileService : IFileService
	{
		public static IFileService Instance
		{
			get
			{
				return Resolver.Resolve<IFileService>();
			}
		}

		public IList<string> GetFileNames(string initialDirectory)
		{
			var dialog = new Microsoft.Win32.OpenFileDialog();
			dialog.Multiselect = true;

			if (!string.IsNullOrEmpty(initialDirectory))
			{
				try
				{
					string fullDirectory = Path.GetFullPath(initialDirectory);

					if (Directory.Exists(fullDirectory))
					{
						dialog.InitialDirectory = fullDirectory;
					}
				}
				catch (NotSupportedException)
				{
					Resolver.Resolve<IAppLogger>().Log("Could not recognize initial directory " + initialDirectory);
				}
			}

			bool? result = dialog.ShowDialog();
			if (result == false)
			{
				return null;
			}

			return new List<string>(dialog.FileNames);
		}

		public string GetFileNameLoad(string initialDirectory = null, string title = null, string filter = null)
		{
			var dialog = new Microsoft.Win32.OpenFileDialog();

			if (title != null)
			{
				dialog.Title = title;
			}

			if (filter != null)
			{
				dialog.Filter = filter;
			}

			if (!string.IsNullOrEmpty(initialDirectory))
			{
				string fullDirectory = Path.GetFullPath(initialDirectory);

				if (Directory.Exists(fullDirectory))
				{
					dialog.InitialDirectory = fullDirectory;
				}
			}

			bool? result = dialog.ShowDialog();

			if (result == false)
			{
				return null;
			}

			return dialog.FileName;
		}

		public string GetFileNameSave(string initialDirectory = null, string title = null, string initialFileName = null, string defaultExt = null, string filter = null)
		{
			var dialog = new Microsoft.Win32.SaveFileDialog();

			if (!string.IsNullOrEmpty(initialDirectory))
			{
				string fullDirectory = Path.GetFullPath(initialDirectory);

				if (Directory.Exists(fullDirectory))
				{
					dialog.InitialDirectory = fullDirectory;
				}
			}

			if (title != null)
			{
				dialog.Title = title;
			}

			if (initialFileName != null)
			{
				dialog.FileName = initialFileName;
			}

			if (defaultExt != null)
			{
				dialog.DefaultExt = defaultExt;
			}

			if (filter != null)
			{
				dialog.Filter = filter;
			}

			bool? result = dialog.ShowDialog();

			if (result == false)
			{
				return null;
			}

			return dialog.FileName;
		}

		public string GetFolderName(string initialDirectory)
		{
			return this.GetFolderName(initialDirectory, description: null);
		}

		public string GetFolderName(string initialDirectory, string description)
		{
			var folderDialog = new VistaFolderBrowserDialog();
			if (description != null)
			{
				folderDialog.Description = description;
			}

			if (!string.IsNullOrEmpty(initialDirectory) && Directory.Exists(initialDirectory))
			{
				folderDialog.SelectedPath = initialDirectory;
			}

			if (folderDialog.ShowDialog() == true)
			{
				return folderDialog.SelectedPath;
			}

			return null;
		}

		public void LaunchFile(string fileName)
		{
			Process.Start(fileName);
		}

		public void LaunchUrl(string url)
		{
			try
			{
				Process.Start(url);
			}
			catch (Win32Exception)
			{
				MessageBox.Show(string.Format(MainRes.LaunchUrlError, url));
			}
		}

		public void PlayVideo(string fileName)
		{
			try
			{
				if (!Utilities.IsRunningAsAppx && Config.UseCustomVideoPlayer && !string.IsNullOrWhiteSpace(Config.CustomVideoPlayer))
				{
					if (File.Exists(Config.CustomVideoPlayer))
					{
						Process.Start(Config.CustomVideoPlayer, "\"" + fileName + "\"");
					}
					else
					{
						MessageBox.Show(string.Format(MainRes.CustomVideoPlayerError, Config.CustomVideoPlayer));
					}
				}
				else
				{
					this.LaunchFile(fileName);
				}
			}
			catch (Win32Exception)
			{
				MessageBox.Show(MainRes.VideoPlayError);
			}
		}
	}
}
