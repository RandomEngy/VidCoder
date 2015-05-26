using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Security.Permissions;
using System.Text;

namespace VidCoder
{
	public static class FileUtilities
	{
		private static List<string> disallowedCharacters = new List<string> { "\\", "/", "\"", ":", "*", "?", "<", ">", "|" };

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
			foreach (string disallowedChar in disallowedCharacters)
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

		private static bool IsExcluded(string candidate, HashSet<string> exclusionList)
		{
			return exclusionList.Contains(candidate);
		}
	}
}
