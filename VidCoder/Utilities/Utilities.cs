﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using HandBrake.Interop.Interop.Json.Scan;
using Microsoft.AnyContainer;
using VidCoder.Extensions;
using VidCoder.Model;
using VidCoder.Resources;
using VidCoder.Services;
using VidCoder.Services.HandBrakeProxy;
using VidCoder.View;
using VidCoderCommon;
using VidCoderCommon.Extensions;
using VidCoderCommon.Model;
using Windows.Foundation.Metadata;

namespace VidCoder
{
	public static class Utilities
	{
		public const int CurrentDatabaseVersion = 46;
		public const int LastUpdatedEncodingProfileDatabaseVersion = 44;
		public const int LastUpdatedPickerDatabaseVersion = 45;

		public const string SquirrelUpdateUrlBase = "https://engy.us/VidCoder/Squirrel";

		static Utilities()
		{
			InstallType = VidCoderInstallType.Zip;

			if (ProgramFolder.StartsWith(CommonUtilities.LocalAppFolder, StringComparison.OrdinalIgnoreCase))
			{
				InstallType = VidCoderInstallType.SquirrelInstaller;
			}
			else
			{
				var tempFolderPath = Environment.GetEnvironmentVariable("temp");
				if (tempFolderPath != null)
				{
					DirectoryInfo tempFolderInfo = new DirectoryInfo(tempFolderPath);
					DirectoryInfo currentDirectoryInfo = new DirectoryInfo(Directory.GetCurrentDirectory());

					if (currentDirectoryInfo.FullName.StartsWith(tempFolderInfo.FullName, StringComparison.OrdinalIgnoreCase))
					{
						InstallType = VidCoderInstallType.Portable;
					}
				}
			}
		}

		public static Version CurrentVersion
		{
			get { return Assembly.GetExecutingAssembly().GetName().Version; }
		}

		/// <summary>
		/// Displays version number with optional Beta marker.
		/// </summary>
		public static string VersionString
		{
			get
			{
				if (CommonUtilities.Beta)
				{
					return string.Format(MiscRes.BetaVersionFormat2, CurrentVersion.ToShortString());
				}
				else
				{
					return CurrentVersion.ToShortString();
				}
			}
		}

		public static VidCoderInstallType InstallType { get; }

		public static bool UwpApisAvailable
		{
			get
			{
				try
				{
					return IsApiContractPresent();
				}
				catch (Exception)
				{
					// The method call will throw if ApiInformation type is not found, which means we are not on Windows 10.
					return false;
				}
			}
		}

		private static bool IsApiContractPresent()
		{
			if (System.Environment.OSVersion.Version.Major < 10)
			{
				return false;
			}

			return ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 1, 0);
		}

		[System.Runtime.InteropServices.DllImport("user32.dll")]
		private static extern IntPtr GetForegroundWindow();

		public static bool IsInForeground
		{
			get
			{
				IntPtr foregroundWindow = GetForegroundWindow();

				foreach (var window in Application.Current.Windows.OfType<Window>())
				{
					if (foregroundWindow == new WindowInteropHelper(window).Handle)
					{
						return true;
					}
				}

				return false;
			}
		}

		public static string PackageFamilyName
		{
			get
			{
				if (CommonUtilities.Beta)
				{
					return "19358RandomEngy.VidCoderBeta_cf0dg7w8q6vfw";
				}
				else
				{
					return "19358RandomEngy.VidCoder_cf0dg7w8q6vfw";
				}
			}
		}

		public static string SquirrelUpdateUrl
		{
			get
			{
				return SquirrelUpdateUrlBase + (CommonUtilities.Beta ? "-Beta" : "-Stable");
			}
		}

		public static string SquirrelAppId
		{
			get
			{
				return CommonUtilities.Beta ? "VidCoder-Beta" : "VidCoder";
			}
		}

		public static bool SupportsUpdates => InstallType == VidCoderInstallType.SquirrelInstaller && CurrentVersion != new Version(1, 0, 0, 0);

		public static bool IsDesigner
		{
			get
			{
				return DesignerProperties.GetIsInDesignMode(new DependencyObject());
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
				intPartsA[i] = int.Parse(stringPartsA[i], CultureInfo.InvariantCulture);
			}

			for (int i = 0; i < intPartsB.Length; i++)
			{
				intPartsB[i] = int.Parse(stringPartsB[i], CultureInfo.InvariantCulture);
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

		public static string GetChangelogUrl(Version version, bool beta)
		{
			string betaPortion = beta ? "-beta" : "";

			return $"https://github.com/RandomEngy/VidCoder/releases/tag/v{version.Major}.{version.Minor}{betaPortion}";
		}

		public static string GetFilePickerFilter(string extension)
		{
			if (extension.StartsWith("."))
			{
				extension = extension.Substring(1);
			}

			return string.Format(CommonRes.FilePickerExtTemplate, extension.ToUpperInvariant()) + "|*." + extension.ToLowerInvariant();
		}

		public static int CurrentProcessInstances
		{
			get
			{
				Process[] processList = Process.GetProcessesByName("VidCoder");
				return processList.Length;
			}
		}

		public static string ProgramPath
		{
			get
			{
				return Assembly.GetExecutingAssembly().Location;
			}
		}

		public static string ProgramFolder
		{
			get
			{
				return Path.GetDirectoryName(ProgramPath);
			}
		}

		public static string WorkerLogsFolder
		{
			get
			{
				return Path.Combine(Path.GetTempPath(), "VidCoderWorkerLogs");
			}
		}

		public static string UpdatesFolder
		{
			get
			{
				string updatesFolder = Path.Combine(CommonUtilities.AppFolder, "Updates");
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
				string imageCacheFolder = Path.Combine(CommonUtilities.AppFolder, "ImageCache");
				if (!Directory.Exists(imageCacheFolder))
				{
					Directory.CreateDirectory(imageCacheFolder);
				}

				return imageCacheFolder;
			}
		}

		public static Dictionary<string, double> DefaultQueueColumnSizes { get; } = new Dictionary<string, double>
		{
			{"Source", 200},
			{"Title", 35},
			{"Range", 60},
			{"Destination", 200},
			{"VideoEncoder", 100},
			{"VideoQuality", 80},
			{"Cropping", 90},
			{"OutputSize", 104},
			{"AudioEncoder", 100},
			{"AudioQuality", 80},
			{"AudioTracks", 120},
			{"SubtitleTracks", 120},
			{"Duration", 60},
			{"Preset", 120}
		};

		public static bool IsValidQueueColumn(string columnId)
		{
			return DefaultQueueColumnSizes.ContainsKey(columnId);
		}

		public static IEncodeProxy CreateEncodeProxy()
		{
			if (CustomConfig.UseWorkerProcess)
			{
				return new RemoteEncodeProxy();
			}
			else
			{
				return new LocalEncodeProxy();
			}
		}


		public static IScanProxy CreateScanProxy()
		{
			if (CustomConfig.UseWorkerProcess)
			{
				return new RemoteScanProxy();
			}
			else
			{
				return new LocalScanProxy();
			}
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

						if (columnId == "Chapters")
						{
							columnId = "Range";
						}

						if (IsValidQueueColumn(columnId) && double.TryParse(settingParts[1], out columnWidth))
						{
							resultList.Add(new Tuple<string, double>(columnId, columnWidth));
						}
					}
				}
			}

			return resultList;
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

			if (bytes < 1073741824)
			{
				double megabytes = ((double)bytes) / 1048576;

				return megabytes.ToString(GetFormatForFilesize(megabytes)) + " MB";
			}

			double gigabytes = ((double)bytes) / 1073741824;

			return gigabytes.ToString(GetFormatForFilesize(gigabytes)) + " GB";
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

			int decimalPlaces = Math.Max(0, Math.Min(2, 3 - digits));

			return "F" + decimalPlaces;
		}

		public static IMessageBoxService MessageBox
		{
			get
			{
				return StaticResolver.Resolve<IMessageBoxService>();
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

		public static SourceTitle GetFeatureTitle(List<SourceTitle> titles, int hbFeatureTitle)
		{
			// If the feature title is supplied, find it in the list.
			if (hbFeatureTitle > 0)
			{
				SourceTitle featureTitle = titles.FirstOrDefault(title => title.Index == hbFeatureTitle);
				if (featureTitle != null)
				{
					return featureTitle;
				}
			}

			// Select the first title within 80% of the duration of the longest title.
			double maxSeconds = titles.Max(title => title.Duration.ToSpan().TotalSeconds);
			foreach (SourceTitle title in titles)
			{
				if (title.Duration.ToSpan().TotalSeconds >= maxSeconds * .8)
				{
					return title;
				}
			}

			return titles[0];
		}

		// Assumes the hashset has a comparer of StringComparer.OrdinalIgnoreCase
		public static FileQueueCheckResult FileExistsOnDiskOrInQueue(string path, HashSet<string> queuedPaths)
		{
			if (queuedPaths.Contains(path))
			{
				return FileQueueCheckResult.InQueue;
			}

			if (File.Exists(path))
			{
				return FileQueueCheckResult.OnDisk;
			}

			return FileQueueCheckResult.NotFound;
		}

		public static SourceType GetSourceType(string sourcePath)
		{
			FileAttributes attributes = File.GetAttributes(sourcePath);
			if ((attributes & FileAttributes.Directory) == FileAttributes.Directory)
			{
				var driveService = StaticResolver.Resolve<IDriveService>();
				if (driveService.PathIsDrive(sourcePath))
				{
					return SourceType.Disc;
				}
				else
				{
					return SourceType.DiscVideoFolder;
				}
			}
			else
			{
				return SourceType.File;
			}
		}

		public static string GetSourceName(string sourcePath)
		{
			switch (GetSourceType(sourcePath))
			{
				case SourceType.DiscVideoFolder:
					return GetSourceNameFolder(sourcePath);
				case SourceType.Disc:
					var driveService = StaticResolver.Resolve<IDriveService>();
					DriveInformation info = driveService.GetDriveInformationFromPath(sourcePath);
					if (info != null)
					{
						return info.VolumeLabel;
					}
					else
					{
						return GetSourceNameFile(sourcePath);
					}
				default:
					return GetSourceNameFile(sourcePath);
			}
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
				if (videoDirectory.Root.FullName == videoDirectory.FullName)
				{
					return "VideoFolder";
				}
				else
				{
					return videoDirectory.Name;
				}
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

		public static FolderType GetFolderType(string directory)
		{
			try
			{
				var directoryInfo = new DirectoryInfo(directory);
				if (!directoryInfo.Exists)
				{
					return FolderType.NonExistent;
				}

				if (File.Exists(Path.Combine(directory, @"VIDEO_TS.IFO")) || File.Exists(Path.Combine(directory, @"VIDEO_TS\VIDEO_TS.IFO")))
				{
					return FolderType.Dvd;
				}

				if (Directory.Exists(Path.Combine(directory, "BDMV")))
				{
					return FolderType.BluRay;
				}
			}
			catch (UnauthorizedAccessException ex)
			{
				StaticResolver.Resolve<IAppLogger>().Log("Could not determine folder type: " + ex);
			}

			return FolderType.VideoFiles;
		}

		public static bool IsDiscFolder(string directory)
		{
			FolderType folderType = GetFolderType(directory);
			return folderType == FolderType.Dvd || folderType == FolderType.BluRay;
		}

		public static string EnsureVideoTsFolder(string directory)
		{
			string videoTsPath = Path.Combine(directory, "VIDEO_TS");
			if (Directory.Exists(videoTsPath))
			{
				return videoTsPath;
			}

			return directory;
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
	}
}
