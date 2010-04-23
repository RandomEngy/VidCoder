using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.IO;

namespace VidCoder
{
    public class FileService : IFileService
    {
        private static IFileService instance;

        public static IFileService Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new FileService();
                }

                return instance;
            }
        }

        public string GetFileNameLoad()
        {
            return this.GetFileNameLoad(null, null, null);
        }

        public string GetFileNameLoad(string defaultExt, string filter, string initialDirectory)
        {
            Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog();

            if (defaultExt != null)
            {
                dialog.DefaultExt = defaultExt;
            }

            if (filter != null)
            {
                dialog.Filter = filter;
            }

            if (!string.IsNullOrEmpty(initialDirectory) && Directory.Exists(initialDirectory))
            {
                dialog.InitialDirectory = initialDirectory;
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
            Microsoft.Win32.SaveFileDialog dialog = new Microsoft.Win32.SaveFileDialog();

            if (!string.IsNullOrEmpty(initialDirectory) && Directory.Exists(initialDirectory))
            {
                dialog.InitialDirectory = initialDirectory;
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
            System.Windows.Forms.FolderBrowserDialog folderDialog = new System.Windows.Forms.FolderBrowserDialog();
            if (description != null)
            {
                folderDialog.Description = description;
            }

            if (!string.IsNullOrEmpty(initialDirectory) && Directory.Exists(initialDirectory))
            {
                folderDialog.SelectedPath = initialDirectory;
            }

            if (folderDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                return folderDialog.SelectedPath;
            }

            return null;
        }

        public void LaunchFile(string fileName)
        {
            Process.Start(fileName);
        }
    }
}
