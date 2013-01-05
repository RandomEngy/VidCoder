using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Diagnostics;
using System.IO;
using System.Windows;
using HandBrake.Interop.Model.Encoding;
using HandBrake.Interop.SourceData;
using Microsoft.Win32;
using VidCoder.Services;
using Microsoft.Practices.Unity;

namespace VidCoder
{
	using HandBrake.Interop.Model;
	using Properties;
	using Resources;

	public static class Utilities
	{
		public const string AppDataFolderName = "VidCoder";
		public const string LocalAppDataFolderName = "VidCoder";
		public const string TimeFormat = @"h\:mm\:ss";
		public const int CurrentDatabaseVersion = 17;
		public const int LastUpdatedEncodingProfileDatabaseVersion = 17;

		private static List<string> disallowedCharacters = new List<string> { "\\", "/", "\"", ":", "*", "?", "<", ">", "|" };

		private static Dictionary<string, double> defaultQueueColumnSizes = new Dictionary<string, double>
		{
			{"Source", 200},
			{"Title", 35},
			{"Chapters", 60},
			{"Destination", 200},
			{"VideoEncoder", 100},
			{"AudioEncoder", 100},
			{"VideoQuality", 80},
			{"Duration", 60},
			{"AudioQuality", 80}
		};

		public static string CurrentVersion
		{
			get
			{
				return Assembly.GetExecutingAssembly().GetName().Version.ToString();
			}
		}

		public static string VersionString
		{
			get
			{
#pragma warning disable 162
#if BETA
				return string.Format(MiscRes.BetaVersionFormat, CurrentVersion, Architecture);
#endif
				return string.Format(MiscRes.VersionFormat, CurrentVersion, Architecture);
#pragma warning restore 162
			}
		}

		public static string Architecture
		{
			get
			{
				if (IntPtr.Size == 4)
				{
					return "x86";
				}

				return "x64";
			}
		}

		public static int CompareVersions(string versionA, string versionB)
		{
			string[] stringPartsA = versionA.Split('.');
			string[] stringPartsB = versionB.Split('.');

			int[] intPartsA = new int[stringPartsA.Length];
			int[] intPartsB = new int[stringPartsB.Length];

			for (int i = 0; i < intPartsA.Length; i++)
			{
				intPartsA[i] = int.Parse(stringPartsA[i]);
			}

			for (int i = 0; i < intPartsB.Length; i++)
			{
				intPartsB[i] = int.Parse(stringPartsB[i]);
			}

			int compareLength = Math.Min(intPartsA.Length, intPartsB.Length);

			for (int i = 0; i < compareLength; i++)
			{
				if (intPartsA[i] > intPartsB[i])
				{
					return 1;
				}
				else if (intPartsA[i] < intPartsB[i])
				{
					return -1;
				}
			}

			if (intPartsA.Length > intPartsB.Length)
			{
				for (int i = compareLength; i < intPartsA.Length; i++)
				{
					if (intPartsA[i] > 0)
					{
						return 1;
					}
				}
			}

			if (intPartsA.Length < intPartsB.Length)
			{
				for (int i = compareLength; i < intPartsB.Length; i++)
				{
					if (intPartsB[i] > 0)
					{
						return 1;
					}
				}
			}

			return 0;
		}

		public static int CurrentProcessInstances
		{
			get
			{
				Process[] processList = Process.GetProcessesByName("VidCoder");
				return processList.Length;
			}
		}

		public static string AppFolder
		{
			get
			{
				return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppDataFolderName);
			}
		}

		public static string LocalAppFolder
		{
			get
			{
				return Path.Combine(
					Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
					LocalAppDataFolderName);
			}
		}

		public static string UpdatesFolder
		{
			get
			{
				string updatesFolder = Path.Combine(AppFolder, "Updates");
				if (!Directory.Exists(updatesFolder))
				{
					Directory.CreateDirectory(updatesFolder);
				}

				return updatesFolder;
			}
		}

		public static string ImageCacheFolder
		{
			get
			{
				string imageCacheFolder = Path.Combine(AppFolder, "ImageCache");
				if (!Directory.Exists(imageCacheFolder))
				{
					Directory.CreateDirectory(imageCacheFolder);
				}

				return imageCacheFolder;
			}
		}

		public static Dictionary<string, double> DefaultQueueColumnSizes
		{
			get
			{
				return defaultQueueColumnSizes;
			}
		}

		public static bool IsValidQueueColumn(string columnId)
		{
			return defaultQueueColumnSizes.ContainsKey(columnId);
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
			//string fileName = Path.GetFileName(baseName);
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
		/// Parse a size list in the format {column id 1}:{width 1}|{column id 2}:{width 2}|...
		/// </summary>
		/// <param name="listString">The string to parse.</param>
		/// <returns>The parsed list of sizes.</returns>
		public static List<Tuple<string, double>> ParseQueueColumnList(string listString)
		{
			var resultList = new List<Tuple<string, double>>();

			string[] columnSettings = listString.Split('|');
			foreach (string columnSetting in columnSettings)
			{
				if (!string.IsNullOrWhiteSpace(columnSetting))
				{
					string[] settingParts = columnSetting.Split(':');
					if (settingParts.Length == 2)
					{
						double columnWidth;
						string columnId = settingParts[0];
						if (IsValidQueueColumn(columnId) && double.TryParse(settingParts[1], out columnWidth))
						{
							resultList.Add(new Tuple<string, double>(columnId, columnWidth));
						}
					}
				}
			}

			return resultList;
		}

		/// <summary>
		/// Formats a TimeSpan into a short, friendly format.
		/// </summary>
		/// <param name="span">The TimeSpan to format.</param>
		/// <returns>The display for the TimeSpan.</returns>
		public static string FormatTimeSpan(TimeSpan span)
		{
			var etaComponents = new List<string>();
			if (span.TotalDays >= 1.0)
			{
				return string.Format("{0}d {1}h", Math.Floor(span.TotalDays), span.Hours);
			}

			if (span.TotalHours >= 1.0)
			{
				return string.Format("{0}h {1:d2}m", span.Hours, span.Minutes);
			}

			if (span.TotalMinutes >= 1.0)
			{
				return string.Format("{0}m {1:d2}s", span.Minutes, span.Seconds);
			}

			return string.Format("{0}s", span.Seconds);
		}

		public static string FormatFileSize(long bytes)
		{
			if (bytes < 1024)
			{
				return bytes.ToString(CultureInfo.InvariantCulture) + " bytes";
			}

			if (bytes < 1048576)
			{
				double kilobytes = ((double)bytes) / 1024;

				return kilobytes.ToString(GetFormatForFilesize(kilobytes)) + " KB";
			}

			double megabytes = ((double)bytes) / 1048576;

			return megabytes.ToString(GetFormatForFilesize(megabytes)) + " MB";
		}

		private static string GetFormatForFilesize(double size)
		{
			int digits = 0;
			double num = size;

			while (num > 1.0)
			{
				num /= 10;
				digits++;
			}

			int decimalPlaces = Math.Min(2, 3 - digits);

			return "F" + decimalPlaces;
		}

		public static IMessageBoxService MessageBox
		{
			get
			{
				return Unity.Container.Resolve<IMessageBoxService>();
			}
		}

		public static bool IsValidFullPath(string path)
		{
			if (string.IsNullOrWhiteSpace(path))
			{
				return false;
			}

			char[] invalidPathChars = Path.GetInvalidPathChars();

			foreach (char c in path)
			{
				if (invalidPathChars.Contains(c))
				{
					return false;
				}
			}

			string fileName = Path.GetFileName(path);
			char[] invalidFileNameChars = Path.GetInvalidFileNameChars();

			foreach (char c in fileName)
			{
				if (invalidFileNameChars.Contains(c))
				{
					return false;
				}
			}

			if (!Path.IsPathRooted(path))
			{
				return false;
			}

			if (string.IsNullOrEmpty(Path.GetFileName(path)))
			{
				return false;
			}

			return true;
		}

		public static bool IsPassthrough(AudioEncoder encoder)
		{
			return encoder == AudioEncoder.Passthrough || encoder == AudioEncoder.Ac3Passthrough;
		}

		public static bool CanPassthrough(AudioCodec codec)
		{
			return
				codec == AudioCodec.Ac3 ||
				codec == AudioCodec.Dts || 
				codec == AudioCodec.DtsHD ||
				codec == AudioCodec.Aac ||
				codec == AudioCodec.Mp3;
		}

		public static Title GetFeatureTitle(List<Title> titles, int hbFeatureTitle)
		{
			// If the feature title is supplied, find it in the list.
			if (hbFeatureTitle > 0)
			{
				return titles.FirstOrDefault(title => title.TitleNumber == hbFeatureTitle);
			}

			// Select the first title within 80% of the duration of the longest title.
			double maxSeconds = titles.Max(title => title.Duration.TotalSeconds);
			foreach (Title title in titles)
			{
				if (title.Duration.TotalSeconds >= maxSeconds * .8)
				{
					return title;
				}
			}

			return titles[0];
		}

		// Assumes the hashset has a comparer of StringComparer.OrdinalIgnoreCase
		public static bool? FileExists(string path, HashSet<string> queuedPaths)
		{
			if (File.Exists(path))
			{
				return true;
			}

			if (queuedPaths.Contains(path))
			{
				return false;
			}

			return null;
		}

		public static string GetSourceNameFile(string videoFile)
		{
			return Path.GetFileNameWithoutExtension(videoFile);
		}

		public static string GetSourceNameFolder(string videoFolder)
		{
			// If the directory is not VIDEO_TS, take its name for the source name (user picked root directory)
			var videoDirectory = new DirectoryInfo(videoFolder);
			if (videoDirectory.Name != "VIDEO_TS")
			{
				return videoDirectory.Name;
			}

			// If the directory is named VIDEO_TS, take the source name from the parent folder (user picked VIDEO_TS folder on DVD)
			DirectoryInfo parentDirectory = videoDirectory.Parent;
			if (parentDirectory == null || parentDirectory.Root.FullName == parentDirectory.FullName)
			{
				return "VideoFolder";
			}
			else
			{
				return parentDirectory.Name;
			}
		}

		public static bool IsDvdFolder(string directory)
		{
			return Path.GetFileName(directory) == "VIDEO_TS" || Directory.Exists(Path.Combine(directory, "VIDEO_TS"));
		}

		public static bool IsDiscFolder(string directory)
		{
			try
			{
				var directoryInfo = new DirectoryInfo(directory);
				if (directoryInfo.Name == "VIDEO_TS")
				{
					return true;
				}

				if (Directory.Exists(Path.Combine(directory, "VIDEO_TS")))
				{
					return true;
				}

				if (Directory.Exists(Path.Combine(directory, "BDMV")))
				{
					return true;
				}

				return false;
			}
			catch (UnauthorizedAccessException ex)
			{
				Unity.Container.Resolve<ILogger>().Log("Could not determine if folder was disc: " + ex);
				return false;
			}
		}

		public static string Wow64RegistryKey
		{
			get
			{
				if (IntPtr.Size == 8 || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PROCESSOR_ARCHITEW6432")))
				{
					return @"SOFTWARE\Wow6432Node";
				}

				return @"SOFTWARE";
			}
		}

		public static string ProgramFilesx86()
		{
			if (IntPtr.Size == 8 || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PROCESSOR_ARCHITEW6432")))
			{
				return Environment.GetEnvironmentVariable("ProgramFiles(x86)");
			}

			return Environment.GetEnvironmentVariable("ProgramFiles");
		}

		public static void SetDragIcon(DragEventArgs e)
		{
			var data = e.Data as DataObject;
			if (data != null && data.ContainsFileDropList())
			{
				e.Effects = DragDropEffects.Copy;
				e.Handled = true;
			}
		}

		public static List<string> GetFiles(string directory)
		{
			var files = new List<string>();
			var accessErrors = new List<string>();
			GetFilesRecursive(directory, files, accessErrors);

			if (accessErrors.Count > 0)
			{
				var messageBuilder = new StringBuilder(CommonRes.CouldNotAccessDirectoriesError + Environment.NewLine);
				foreach (string accessError in accessErrors)
				{
					messageBuilder.AppendLine(accessError);
				}

				MessageBox.Show(messageBuilder.ToString());
			}

			return files;
		}

		private static void GetFilesRecursive(string directory, List<string> files, List<string> accessErrors)
		{
			try
			{
				string[] subdirectories = Directory.GetDirectories(directory);
				foreach (string subdirectory in subdirectories)
				{
					GetFilesRecursive(subdirectory, files, accessErrors);
				}
			}
			catch (UnauthorizedAccessException)
			{
				accessErrors.Add(directory);
			}

			try
			{
				string[] files2 = Directory.GetFiles(directory);
				files.AddRange(files2);
			}
			catch (UnauthorizedAccessException)
			{
				accessErrors.Add(directory);
			}
		}

		public static List<string> GetFilesOrVideoFolders(string directory, IList<string> videoExtensions)
		{
			var path = new List<string>();
			var accessErrors = new List<string>();
			GetFilesOrVideoFoldersRecursive(directory, path, accessErrors, videoExtensions);

			if (accessErrors.Count > 0)
			{
				var messageBuilder = new StringBuilder(CommonRes.CouldNotAccessDirectoriesError + Environment.NewLine);
				foreach (string accessError in accessErrors)
				{
					messageBuilder.AppendLine(accessError);
				}

				MessageBox.Show(messageBuilder.ToString());
			}

			return path;
		}

		private static void GetFilesOrVideoFoldersRecursive(string directory, List<string> paths, List<string> accessErrors, IList<string> videoExtensions)
		{
			try
			{
				string[] subdirectories = Directory.GetDirectories(directory);
				foreach (string subdirectory in subdirectories)
				{
					if (IsDiscFolder(subdirectory))
					{
						paths.Add(subdirectory);
					}
					else
					{
						GetFilesOrVideoFoldersRecursive(subdirectory, paths, accessErrors, videoExtensions);
					}
				}
			}
			catch (UnauthorizedAccessException)
			{
				accessErrors.Add(directory);
			}

			try
			{
				string[] files = Directory.GetFiles(directory);
				paths.AddRange(
					files.Where(
						f => videoExtensions.Any(
							e => f.EndsWith(e, StringComparison.OrdinalIgnoreCase))));
			}
			catch (UnauthorizedAccessException)
			{
				accessErrors.Add(directory);
			}
		}

		private static bool IsExcluded(string candidate, HashSet<string> exclusionList)
		{
			return exclusionList.Contains(candidate);
		}
	}
}
