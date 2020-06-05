using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Permissions;
using System.Text;
using Microsoft.AnyContainer;
using VidCoder.Services;
using VidCoderCommon.Services;

namespace VidCoder
{
	public static class FileUtilities
	{
		[DllImport("shell32.dll", SetLastError = true)]
		public static extern int SHOpenFolderAndSelectItems(IntPtr pidlFolder, uint cidl, [In, MarshalAs(UnmanagedType.LPArray)] IntPtr[] apidl, uint dwFlags);

		[DllImport("shell32.dll", SetLastError = true)]
		public static extern void SHParseDisplayName([MarshalAs(UnmanagedType.LPWStr)] string name, IntPtr bindingContext, [Out] out IntPtr pidl, uint sfgaoIn, [Out] out uint psfgaoOut);

		public static void OpenFolderAndSelectItem(string filePath)
		{
			OpenFolderAndSelectItem(Path.GetDirectoryName(filePath), Path.GetFileName(filePath));
		}

		public static void OpenFolderAndSelectItem(string folderPath, string file)
		{
			IntPtr nativeFolder;
			uint psfgaoOut;
			SHParseDisplayName(folderPath, IntPtr.Zero, out nativeFolder, 0, out psfgaoOut);

			if (nativeFolder == IntPtr.Zero)
			{
				StaticResolver.Resolve<IAppLogger>().LogError($"Could not find folder {folderPath}");

				return;
			}

			IntPtr nativeFile;
			SHParseDisplayName(Path.Combine(folderPath, file), IntPtr.Zero, out nativeFile, 0, out psfgaoOut);

			IntPtr[] fileArray;
			if (nativeFile == IntPtr.Zero)
			{
				StaticResolver.Resolve<IAppLogger>().LogError($"Could not find file {Path.Combine(folderPath, file)}");

				fileArray = new IntPtr[0];
			}
			else
			{
				fileArray = new IntPtr[] { nativeFile };
			}

			SHOpenFolderAndSelectItems(nativeFolder, (uint)fileArray.Length, fileArray, 0);

			Marshal.FreeCoTaskMem(nativeFolder);
			if (nativeFile != IntPtr.Zero)
			{
				Marshal.FreeCoTaskMem(nativeFile);
			}
		}

		private static readonly List<string> DisallowedCharacters = new List<string> { "\\", "/", "\"", ":", "*", "?", "<", ">", "|" };

		public static IList<string> SubtitleExtensions = new List<string> { ".srt", ".ssa", ".ass" };

		public static bool OverrideTempFolder { get; set; }
		public static string TempFolderOverride { get; set; }

		public static bool HasWriteAccessOnFolder(string folder)
		{
			try
			{
				using (FileStream fs = File.Create(
					Path.Combine(folder, Path.GetRandomFileName()),
					1,
					FileOptions.DeleteOnClose))
				{
				}

				return true;
			}
			catch
			{
				return false;
			}
		}

		public static bool IsDirectory(string filePath)
		{
			var fileAttributes = File.GetAttributes(filePath);
			return (fileAttributes & FileAttributes.Directory) == FileAttributes.Directory;
		}

		public static void CopyDirectory(string sourceDir, string destDir)
		{
			// Create root directory
			if (!Directory.Exists(destDir))
			{
				Directory.CreateDirectory(destDir);
			}

			// Create sub directories
			foreach (string dirPath in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
			{
				string subdirectory = dirPath.Replace(sourceDir, destDir);

				if (!Directory.Exists(subdirectory))
				{
					Directory.CreateDirectory(subdirectory);
				}
			}

			// Create files
			foreach (string newPath in Directory.GetFiles(sourceDir, "*.*", SearchOption.AllDirectories))
			{
				File.Copy(newPath, newPath.Replace(sourceDir, destDir));
			}
		}

		public static void DeleteDirectory(string path)
		{
			var directory = new DirectoryInfo(path);
			FileInfo[] files = directory.GetFiles();
			foreach (FileInfo file in files)
			{
				File.Delete(file.FullName);
			}

			DirectoryInfo[] subdirectories = directory.GetDirectories();
			foreach (DirectoryInfo subdirectory in subdirectories)
			{
				DeleteDirectory(subdirectory.FullName);
			}

			Directory.Delete(path);
		}

		public static void ClearFiles(string path)
		{
			var directory = new DirectoryInfo(path);
			FileInfo[] files = directory.GetFiles();
			foreach (FileInfo file in files)
			{
				File.Delete(file.FullName);
			}

			DirectoryInfo[] subdirectories = directory.GetDirectories();
			foreach (DirectoryInfo subdirectory in subdirectories)
			{
				ClearFiles(subdirectory.FullName);
			}
		}

		public static string CleanFileName(string fileName, bool allowBackslashes = false)
		{
			string cleanName = fileName;
			foreach (string disallowedChar in DisallowedCharacters)
			{
				if (disallowedChar != @"\" || !allowBackslashes)
				{
					cleanName = cleanName.Replace(disallowedChar, "_");
				}
			}

			return cleanName;
		}

		/// <summary>
		/// Creates a unique file name with the given constraints.
		/// </summary>
		/// <param name="baseName">The base file name to work with.</param>
		/// <param name="outputDirectory">The directory the file should be written to.</param>
		/// <param name="excludedPaths">Any paths to be excluded. Collection is expected to have StringComparer.OrdinalIgnoreCase.</param>
		/// <returns>A file name that does not exist and does not match any of the given paths.</returns>
		public static string CreateUniqueFileName(string baseName, string outputDirectory, HashSet<string> excludedPaths)
		{
			string candidateFilePath = Path.Combine(outputDirectory, baseName);
			if (!File.Exists(candidateFilePath) && !IsExcluded(candidateFilePath, excludedPaths))
			{
				return candidateFilePath;
			}

			string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(baseName);
			string extension = Path.GetExtension(baseName);

			for (int i = 1; i < 1000; i++)
			{
				string candidateFileName = fileNameWithoutExtension + "-" + i + extension;
				candidateFilePath = Path.Combine(outputDirectory, candidateFileName);

				if (!IsExcluded(candidateFilePath, excludedPaths))
				{
					if (!File.Exists(candidateFilePath))
					{
						return candidateFilePath;
					}
				}
			}

			return null;
		}

		public static string CreateUniqueFileName(string filePath, HashSet<string> excludedPaths)
		{
			return CreateUniqueFileName(Path.GetFileName(filePath), Path.GetDirectoryName(filePath), excludedPaths);
		}

		/// <summary>
		/// Gets the real location for the given file, substituting the local app data appropriately if running in desktop bridge
		/// </summary>
		/// <param name="filePath">The file location according to the program.</param>
		/// <returns>The real file location.</returns>
		public static string GetRealFilePath(string filePath)
		{
			if (Utilities.IsRunningAsAppx)
			{
				string roamingFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

				if (filePath.StartsWith(roamingFolder, StringComparison.OrdinalIgnoreCase))
				{
					string relativePath = filePath.Substring(roamingFolder.Length);
					if (relativePath.StartsWith("\\", StringComparison.Ordinal))
					{
						relativePath = relativePath.Substring(1);
					}

					string candidatePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Packages", Utilities.PackageFamilyName, @"LocalCache\Roaming", relativePath);
					if (File.Exists(candidatePath))
					{
						return candidatePath;
					}
				}
			}

			return filePath;
		}

		private static bool IsExcluded(string candidate, HashSet<string> exclusionList)
		{
			return exclusionList.Contains(candidate);
		}
	}
}
