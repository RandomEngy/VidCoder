using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Diagnostics;
using System.IO;
using System.Windows;
using VidCoder.Services;
using Microsoft.Practices.Unity;
using HandBrake.Interop;
using HandBrake.SourceData;

namespace VidCoder
{
	public static class Utilities
	{
		public const string UpdateInfoUrl = "http://engy.us/VidCoder/latest.xml";
		public const string AppDataFolderName = "VidCoder";

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
			{"AudioBitrate", 80}
		};

		public static string CurrentVersion
		{
			get
			{
				return Assembly.GetExecutingAssembly().GetName().Version.ToString();
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
			DirectoryInfo directory = new DirectoryInfo(path);
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

		public static string CleanFileName(string fileName)
		{
			string cleanName = fileName;
			foreach (string disallowedChar in disallowedCharacters)
			{
				cleanName = cleanName.Replace(disallowedChar, "_");
			}

			return cleanName;
		}

		public static string CreateUniqueFileName(string baseName, string outputDirectory, HashSet<string> excludedPaths)
		{
			string fileName = Path.GetFileName(baseName);
			string candidateFilePath = Path.Combine(outputDirectory, fileName);
			if (!File.Exists(candidateFilePath) && !IsExcluded(fileName, excludedPaths))
			{
				return candidateFilePath;
			}

			string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(baseName);
			string extension = Path.GetExtension(baseName);

			for (int i = 1; i < 1000; i++)
			{
				string candidateFileName = fileNameWithoutExtension + "-" + i + extension;
				if (!IsExcluded(candidateFileName, excludedPaths))
				{
					candidateFilePath = Path.Combine(outputDirectory, candidateFileName);
					if (!File.Exists(candidateFilePath))
					{
						return candidateFilePath;
					}
				}
			}

			return null;
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

			char[] invalidChars = Path.GetInvalidPathChars();

			foreach (char c in path)
			{
				if (invalidChars.Contains(c))
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
			return encoder == AudioEncoder.Passthrough || encoder == AudioEncoder.Ac3Passthrough || encoder == AudioEncoder.DtsPassthrough;
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

		private static bool IsExcluded(string candidate, HashSet<string> exclusionList)
		{
			return exclusionList.Contains(candidate.ToLowerInvariant());
		}
	}
}
