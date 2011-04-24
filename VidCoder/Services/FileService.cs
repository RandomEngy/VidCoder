using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.IO;
using System.ComponentModel;
using System.Windows;
using Ookii.Dialogs.Wpf;
using Microsoft.Practices.Unity;

namespace VidCoder.Services
{
	public class FileService : IFileService
	{
		public static IFileService Instance
		{
			get
			{
				return Unity.Container.Resolve<IFileService>();
			}
		}

		public IList<string> GetFileNames(string initialDirectory)
		{
			var dialog = new Microsoft.Win32.OpenFileDialog();
			dialog.Multiselect = true;

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

			return new List<string>(dialog.FileNames);
		}

		public string GetFileNameLoad()
		{
			return this.GetFileNameLoad(null, null, null);
		}

		public string GetFileNameLoad(string initialDirectory, string defaultExt, string filter)
		{
			var dialog = new Microsoft.Win32.OpenFileDialog();

			if (defaultExt != null)
			{
				dialog.DefaultExt = defaultExt;
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

		public string GetFileNameSave(string initialDirectory)
		{
			return this.GetFileNameSave(initialDirectory, initialFileName: null, defaultExt: null, filter: null);
		}

		public string GetFileNameSave(string initialDirectory, string initialFileName, string defaultExt, string filter)
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
				MessageBox.Show("Error launching URL: " + url);
			}
		}
	}
}
