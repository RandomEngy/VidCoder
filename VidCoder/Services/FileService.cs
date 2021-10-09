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
				return StaticResolver.Resolve<IFileService>();
			}
		}

		public IList<string> GetFileNames(string initialDirectory)
		{
			var dialogResult = ShowOpenDialogWithInitialDirectory(
				() =>
				{
					var dialog = new Microsoft.Win32.OpenFileDialog();
					dialog.Multiselect = true;

					return dialog;
				},
				initialDirectory);

			if (dialogResult.result == false)
			{
				return null;
			}

			return new List<string>(dialogResult.createdDialog.FileNames);
		}

		public string GetFileNameLoad(string initialDirectory = null, string title = null, string filter = null)
		{
			var dialogResult = ShowOpenDialogWithInitialDirectory(
				() =>
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

					return dialog;
				},
				initialDirectory);

			if (dialogResult.result == false)
			{
				return null;
			}

			return dialogResult.createdDialog.FileName;
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
			Process process = new Process();
			process.StartInfo.UseShellExecute = true;
			process.StartInfo.FileName = fileName;
			process.Start();
		}

		public void LaunchUrl(string url)
		{
			try
			{
				Process process = new Process();
				process.StartInfo.UseShellExecute = true;
				process.StartInfo.FileName = url;
				process.Start();
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
				if (Config.UseCustomVideoPlayer && !string.IsNullOrWhiteSpace(Config.CustomVideoPlayer))
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

		private static (bool? result, Microsoft.Win32.OpenFileDialog createdDialog) ShowOpenDialogWithInitialDirectory(Func<Microsoft.Win32.OpenFileDialog> dialogFunc, string initialDirectory)
		{
			var dialog = dialogFunc();

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
					StaticResolver.Resolve<IAppLogger>().Log("Could not recognize initial directory " + initialDirectory);
				}
			}

			try
			{
				return (dialog.ShowDialog(), dialog);
			}
			catch (Exception exception)
			{
				if (initialDirectory != null)
				{
					StaticResolver.Resolve<IAppLogger>().Log($"Error showing file dialog with initial directory '{initialDirectory}'" + Environment.NewLine + exception);
					return ShowOpenDialogWithInitialDirectory(dialogFunc, null);
				}
				else
				{
					StaticResolver.Resolve<IAppLogger>().Log($"Error showing file dialog with no initial directory" + Environment.NewLine + exception);
					return (false, null);
				}
			}
		}
	}
}
