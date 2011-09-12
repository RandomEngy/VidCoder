using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Input;
using HandBrake.Interop.Model;
using HandBrake.Interop.Model.Encoding;
using HandBrake.Interop.SourceData;
using Microsoft.Practices.Unity;
using VidCoder.Model;
using VidCoder.Properties;
using VidCoder.Services;

namespace VidCoder.ViewModel.Components
{
	/// <summary>
	/// Controls automatic naming logic for the encoding output path.
	/// </summary>
	public class OutputPathViewModel : ViewModelBase
	{
		private MainViewModel main = Unity.Container.Resolve<MainViewModel>();
		private ProcessingViewModel processingVM;
		private PresetsViewModel presetsVM;
		private IDriveService driveService = Unity.Container.Resolve<IDriveService>();

		private string outputPath;

		private bool editingDestination;

		private ICommand pickDefaultOutputFolderCommand;
		private ICommand pickOutputPathCommand;

		public ProcessingViewModel ProcessingVM
		{
			get
			{
				if (this.processingVM == null)
				{
					this.processingVM = Unity.Container.Resolve<ProcessingViewModel>();
				}

				return this.processingVM;
			}
		}

		public PresetsViewModel PresetsVM
		{
			get
			{
				if (this.presetsVM == null)
				{
					this.presetsVM = Unity.Container.Resolve<PresetsViewModel>();
				}

				return this.presetsVM;
			}
		}

		public string OutputPath
		{
			get
			{
				return this.outputPath;
			}

			set
			{
				this.outputPath = value;
				this.NotifyPropertyChanged("OutputPath");
			}
		}

		public string OldOutputPath { get; set; }

		public bool ManualOutputPath { get; set; }

		public bool EditingDestination
		{
			get
			{
				return this.editingDestination;
			}

			set
			{
				this.editingDestination = value;
				this.NotifyPropertyChanged("EditingDestination");
			}
		}

		public bool OutputFolderChosen
		{
			get
			{
				return !string.IsNullOrEmpty(Settings.Default.AutoNameOutputFolder);
			}
		}

		public ICommand PickDefaultOutputFolderCommand
		{
			get
			{
				if (this.pickDefaultOutputFolderCommand == null)
				{
					this.pickDefaultOutputFolderCommand = new RelayCommand(param =>
					{
						string newOutputFolder = FileService.Instance.GetFolderName(null, "Choose the output directory for encoded video files.");

						if (newOutputFolder != null)
						{
							Settings.Default.AutoNameOutputFolder = newOutputFolder;
							Settings.Default.Save();
							this.NotifyPropertyChanged("OutputFolderChosen");
							this.NotifyPropertyChanged("CanEnqueueMultipleTitles");
							this.NotifyPropertyChanged("EnqueueToolTip");
							this.NotifyPropertyChanged("EncodeToolTip");

							this.GenerateOutputFileName();
						}
					});
				}

				return this.pickDefaultOutputFolderCommand;
			}
		}

		public ICommand PickOutputPathCommand
		{
			get
			{
				if (this.pickOutputPathCommand == null)
				{
					this.pickOutputPathCommand = new RelayCommand(param =>
					{
						string extensionDot = this.GetOutputExtensionForCurrentEncodingProfile();
						string extension = this.GetOutputExtensionForCurrentEncodingProfile(includeDot: false);
						string extensionLabel = extension.ToUpperInvariant();

						string newOutputPath = FileService.Instance.GetFileNameSave(
							Settings.Default.LastOutputFolder,
							"Encode output location",
							null,
							extension,
							string.Format("{0} Files|*{1}", extensionLabel, extensionDot));
						this.SetManualOutputPath(newOutputPath, this.OutputPath);
					},
					param =>
					{
						return this.OutputFolderChosen;
					});
				}

				return this.pickOutputPathCommand;
			}
		}

		// Resolves any conflicts for the given output path.
		// Returns a non-conflicting output path.
		// May return the same value if there are no conflicts.
		// null means cancel.
		public string ResolveOutputPathConflicts(string initialOutputPath, HashSet<string> excludedPaths, bool isBatch)
		{
			HashSet<string> queuedFiles = excludedPaths;
			bool? conflict = Utilities.FileExists(initialOutputPath, queuedFiles);

			if (conflict == null)
			{
				return initialOutputPath;
			}

			WhenFileExists preference;
			if (isBatch)
			{
				preference = Settings.Default.WhenFileExistsBatch;
			}
			else
			{
				preference = Settings.Default.WhenFileExists;
			}

			switch (preference)
			{
				case WhenFileExists.Prompt:
					break;
				case WhenFileExists.Overwrite:
					return initialOutputPath;
				case WhenFileExists.AutoRename:
					return Utilities.CreateUniqueFileName(initialOutputPath, queuedFiles);
				default:
					throw new ArgumentOutOfRangeException();
			}

			// Continue and prompt user for resolution

			var conflictDialog = new FileConflictDialogViewModel(initialOutputPath, (bool)conflict);
			WindowManager.OpenDialog(conflictDialog, this);

			switch (conflictDialog.FileConflictResolution)
			{
				case FileConflictResolution.Cancel:
					return null;
				case FileConflictResolution.Overwrite:
					return initialOutputPath;
				case FileConflictResolution.AutoRename:
					return Utilities.CreateUniqueFileName(initialOutputPath, queuedFiles);
				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		public string ResolveOutputPathConflicts(string initialOutputPath, bool isBatch)
		{
			return ResolveOutputPathConflicts(initialOutputPath, this.ProcessingVM.GetQueuedFiles(), isBatch);
		}

		/// <summary>
		/// Gets the output extension for the given subtitles and title, and with the
		/// current encoding settings.
		/// </summary>
		/// <param name="givenSubtitles">The subtitles to determine the extension for.</param>
		/// <param name="givenTitle">The title to determine the extension for.</param>
		/// <returns>The output extension (with dot) for the given subtitles and title,
		/// and with the current encoding settings.</returns>
		public string GetOutputExtension(Subtitles givenSubtitles, Title givenTitle)
		{
			string extension;

			// If we have a text source subtitle, force .m4v extension.
			bool allowMp4Extension = true;
			if (givenSubtitles != null && givenSubtitles.SourceSubtitles != null)
			{
				foreach (SourceSubtitle sourceSubtitle in givenSubtitles.SourceSubtitles)
				{
					if (sourceSubtitle.TrackNumber > 0)
					{
						if (givenTitle.Subtitles[sourceSubtitle.TrackNumber - 1].SubtitleType == SubtitleType.Text)
						{
							allowMp4Extension = false;
						}
					}
				}
			}

			EncodingProfile profile = this.PresetsVM.SelectedPreset.Preset.EncodingProfile;
			if (profile.OutputFormat == OutputFormat.Mkv)
			{
				extension = ".mkv";
			}
			else if (profile.PreferredExtension == OutputExtension.Mp4 && allowMp4Extension)
			{
				extension = ".mp4";
			}
			else
			{
				extension = ".m4v";
			}

			return extension;
		}

		/// <summary>
		/// Gets the extension that should be used for the current encoding settings, subtitles
		/// and title.
		/// </summary>
		/// <returns>The extension (with dot) that should be used for current encoding settings, subtitles
		/// and title. If no video source is present, this is determined from the encoding settings.</returns>
		private string GetOutputExtension()
		{
			if (this.main.HasVideoSource)
			{
				return this.GetOutputExtension(this.main.CurrentSubtitles, this.main.SelectedTitle);
			}

			return this.GetOutputExtensionForCurrentEncodingProfile();
		}

		public string GetOutputExtensionForCurrentEncodingProfile(bool includeDot = true)
		{
			EncodingProfile profile = this.PresetsVM.SelectedPreset.Preset.EncodingProfile;
			string extension;

			if (profile.OutputFormat == OutputFormat.Mkv)
			{
				extension = "mkv";
			}
			else if (profile.PreferredExtension == OutputExtension.Mp4)
			{
				extension = "mp4";
			}
			else
			{
				extension = "m4v";
			}

			return includeDot ? "." + extension : extension;
		}

		/// <summary>
		/// Processes and sets a user-provided output path.
		/// </summary>
		/// <param name="newOutputPath">The user provided output path.</param>
		/// <param name="oldOutputPath">The previous output path.</param>
		public void SetManualOutputPath(string newOutputPath, string oldOutputPath)
		{
			if (newOutputPath == oldOutputPath)
			{
				return;
			}

			if (Utilities.IsValidFullPath(newOutputPath))
			{
				string outputDirectory = Path.GetDirectoryName(newOutputPath);
				Settings.Default.LastOutputFolder = outputDirectory;
				Settings.Default.Save();

				string fileName = Path.GetFileNameWithoutExtension(newOutputPath);
				string extension = this.GetOutputExtension();

				this.ManualOutputPath = true;
				this.OutputPath = Path.Combine(outputDirectory, fileName + extension);
			}
			else
			{
				// If it's not a valid path, revert the change.
				if (this.main.HasVideoSource && string.IsNullOrEmpty(Path.GetFileName(oldOutputPath)))
				{
					// If we've got a video source now and the old path was blank, generate a file name
					this.GenerateOutputFileName();
				}
				else
				{
					// Else just fall back to whatever the old path was
					this.OutputPath = oldOutputPath;
				}
			}
		}

		public void GenerateOutputFileName()
		{
			string fileName;

			// If our original path was empty and we're editing it at the moment, don't clobber
			// whatever the user is typing.
			if (string.IsNullOrEmpty(Path.GetFileName(this.OldOutputPath)) && this.EditingDestination)
			{
				return;
			}

			if (this.ManualOutputPath)
			{
				// When a manual path has been specified, keep the directory and base file name.
				fileName = Path.GetFileNameWithoutExtension(this.OutputPath);
				this.OutputPath = Path.Combine(Path.GetDirectoryName(this.OutputPath), fileName + this.GetOutputExtension());
				return;
			}

			if (!this.main.HasVideoSource)
			{
				string outputFolder = Settings.Default.AutoNameOutputFolder;
				if (outputFolder != null)
				{
					this.OutputPath = outputFolder + (outputFolder.EndsWith(@"\") ? string.Empty : @"\");
				}

				return;
			}

			if (this.main.SourceName == null || this.main.SelectedStartChapter == null || this.main.SelectedEndChapter == null)
			{
				return;
			}

			if (string.IsNullOrEmpty(Settings.Default.AutoNameOutputFolder))
			{
				return;
			}

			// Change casing on DVD titles to be a little more friendly
			string translatedSourceName = this.main.SourceName;
			if ((this.main.SelectedSource.Type == SourceType.Dvd || this.main.SelectedSource.Type == SourceType.VideoFolder) && !string.IsNullOrWhiteSpace(this.main.SourceName))
			{
				translatedSourceName = this.TranslateDvdSourceName(this.main.SourceName);
			}

			fileName = this.BuildOutputFileName(
				this.main.SourcePath,
				translatedSourceName,
				this.main.SelectedTitle.TitleNumber,
				this.main.VideoRangeType,
				this.main.SelectedStartChapter.ChapterNumber,
				this.main.SelectedEndChapter.ChapterNumber,
				this.main.SelectedTitle.Chapters.Count,
				this.main.SecondsRangeStart,
				this.main.SecondsRangeEnd,
				this.main.FramesRangeStart,
				this.main.FramesRangeEnd);

			string extension = this.GetOutputExtension();

			this.OutputPath = this.BuildOutputPath(fileName, extension, sourcePath: this.main.SourcePath);

			// If we've pushed a new name into the destination text box, we need to update the "baseline" name so the
			// auto-generated name doesn't get mistakenly labeled as manual when focus leaves it
			if (this.EditingDestination)
			{
				this.OldOutputPath = this.OutputPath;
			}
		}

		/// <summary>
		/// Changes casing on DVD titles to be a little more friendly.
		/// </summary>
		/// <param name="dvdSourceName">The source name of the DVD.</param>
		/// <returns>Cleaned up version of the source name.</returns>
		public string TranslateDvdSourceName(string dvdSourceName)
		{
			string[] titleWords = dvdSourceName.Split('_');
			var translatedTitleWords = new List<string>();
			bool reachedModifiers = false;

			foreach (string titleWord in titleWords)
			{
				// After the disc designator, stop changing capitalization.
				if (!reachedModifiers && titleWord.Length == 2 && titleWord[0] == 'D' && char.IsDigit(titleWord[1]))
				{
					reachedModifiers = true;
				}

				if (reachedModifiers)
				{
					translatedTitleWords.Add(titleWord);
				}
				else
				{
					if (titleWord.Length > 0)
					{
						translatedTitleWords.Add(titleWord[0] + titleWord.Substring(1).ToLower());
					}
				}
			}

			return string.Join(" ", translatedTitleWords);
		}

		public string BuildOutputPath(string fileName, string extension, string sourcePath)
		{
			// Use our default output folder by default
			string outputFolder = Settings.Default.AutoNameOutputFolder;
			if (Settings.Default.OutputToSourceDirectory)
			{
				string sourceRoot = Path.GetPathRoot(sourcePath);
				IList<DriveInfo> driveInfo = this.driveService.GetDriveInformation();
				DriveInfo matchingDrive = driveInfo.FirstOrDefault(d => string.Compare(d.RootDirectory.FullName, sourceRoot, StringComparison.OrdinalIgnoreCase) == 0);

				string sourceDirectory = Path.GetDirectoryName(sourcePath);

				// Use the source directory if it exists and not on an optical drive
				if (!string.IsNullOrEmpty(sourceDirectory) && (matchingDrive == null || matchingDrive.DriveType != DriveType.CDRom))
				{
					outputFolder = sourceDirectory;
				}
			}

			if (!string.IsNullOrEmpty(outputFolder))
			{
				string result = Path.Combine(outputFolder, fileName + extension);
				if (result == sourcePath)
				{
					result = Path.Combine(outputFolder, fileName + " (Encoded)" + extension);
				}

				return result;
			}

			return null;
		}

		public string BuildOutputFileName(string sourcePath, string sourceName, int title, int totalChapters)
		{
			return this.BuildOutputFileName(
				sourcePath,
				sourceName,
				title,
				VideoRangeType.Chapters,
				1,
				totalChapters,
				totalChapters,
				0,
				0,
				0,
				0);
		}

		public string BuildOutputFileName(string sourcePath, string sourceName, int title, VideoRangeType rangeType, int startChapter, int endChapter, int totalChapters, double startSecond, double endSecond, int startFrame, int endFrame)
		{
			string fileName;
			if (Settings.Default.AutoNameCustomFormat)
			{
				string rangeString = string.Empty;
				switch (rangeType)
				{
					case VideoRangeType.Chapters:
						if (startChapter == endChapter)
						{
							rangeString = startChapter.ToString();
						}
						else
						{
							rangeString = startChapter + "-" + endChapter;
						}

						break;
					case VideoRangeType.Seconds:
						rangeString = startSecond + "-" + endSecond;
						break;
					case VideoRangeType.Frames:
						rangeString = startFrame + "-" + endFrame;
						break;
				}

				fileName = Settings.Default.AutoNameCustomFormatString;

				fileName = fileName.Replace("{source}", sourceName);
				fileName = ReplaceTitles(fileName, title);
				fileName = fileName.Replace("{range}", rangeString);

				// {chapters} is deprecated in favor of {range} but we replace here for backwards compatibility.
				fileName = fileName.Replace("{chapters}", rangeString);

				fileName = fileName.Replace("{preset}", this.PresetsVM.SelectedPreset.Preset.Name);
				fileName = ReplaceParents(fileName, sourcePath);

				DateTime now = DateTime.Now;
				if (fileName.Contains("{date}"))
				{
					fileName = fileName.Replace("{date}", now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
				}

				if (fileName.Contains("{time}"))
				{
					fileName = fileName.Replace("{time}", string.Format("{0:d2}.{1:d2}.{2:d2}", now.Hour, now.Minute, now.Second));
				}

				if (fileName.Contains("{quality}"))
				{
					EncodingProfile profile = this.PresetsVM.SelectedPreset.Preset.EncodingProfile;
					double quality = 0;
					switch (profile.VideoEncodeRateType)
					{
						case VideoEncodeRateType.ConstantQuality:
							quality = profile.Quality;
							break;
						case VideoEncodeRateType.AverageBitrate:
							quality = profile.VideoBitrate;
							break;
						case VideoEncodeRateType.TargetSize:
							quality = profile.TargetSize;
							break;
						default:
							break;
					}

					fileName = fileName.Replace("{quality}", quality.ToString());
				}
			}
			else
			{
				string titleSection = string.Empty;
				if (this.main.SelectedSource.Type != SourceType.File)
				{
					titleSection = " - Title " + title;
				}

				string rangeSection = string.Empty;
				switch (rangeType)
				{
					case VideoRangeType.Chapters:
						if (startChapter > 1 || endChapter < totalChapters)
						{
							if (startChapter == endChapter)
							{
								rangeSection = " - Chapter " + startChapter;
							}
							else
							{
								rangeSection = " - Chapters " + startChapter + "-" + endChapter;
							}
						}

						break;
					case VideoRangeType.Seconds:
						rangeSection = " - Seconds " + startSecond + "-" + endSecond;
						break;
					case VideoRangeType.Frames:
						rangeSection = " - Frames " + startFrame + "-" + endFrame;
						break;
				}

				fileName = sourceName + titleSection + rangeSection;
			}

			return Utilities.CleanFileName(fileName, allowBackslashes: true);
		}

		private static string ReplaceTitles(string inputString, int title)
		{
			inputString = inputString.Replace("{title}", title.ToString());

			Regex regex = new Regex("{title:(?<number>[0-9]+)}");
			Match match;
			while ((match = regex.Match(inputString)).Success)
			{
				Capture capture = match.Groups["number"].Captures[0];
				int replaceIndex = capture.Index - 7;
				int replaceLength = capture.Length + 8;

				int digits = int.Parse(capture.Value);

				if (digits > 0 && digits <= 10)
				{
					inputString = inputString.Substring(0, replaceIndex) + string.Format("{0:D" + digits + "}", title) + inputString.Substring(replaceIndex + replaceLength);
				}
			}

			return inputString;
		}

		/// <summary>
		/// Takes a string and replaces instances of {parent} or {parent:x} with the appropriate parent.
		/// </summary>
		/// <param name="inputString">The input string to perform replacements in.</param>
		/// <param name="path">The path to take the parents from.</param>
		/// <returns>The string with instances replaced.</returns>
		private static string ReplaceParents(string inputString, string path)
		{
			string directParentName = Path.GetDirectoryName(path);
			if (directParentName == null)
			{
				return inputString;
			}

			DirectoryInfo directParent = new DirectoryInfo(directParentName);

			if (directParent.Root.FullName == directParent.FullName)
			{
				return inputString;
			}

			inputString = inputString.Replace("{parent}", directParent.Name);

			Regex regex = new Regex("{parent:(?<number>[0-9]+)}");
			Match match;
			while ((match = regex.Match(inputString)).Success)
			{
				Capture capture = match.Groups["number"].Captures[0];
				int replaceIndex = capture.Index - 8;
				int replaceLength = capture.Length + 9;

				inputString = inputString.Substring(0, replaceIndex) + FindParent(path, int.Parse(capture.Value)) + inputString.Substring(replaceIndex + replaceLength);
			}

			return inputString;
		}

		private static string FindParent(string path, int parentNumber)
		{
			string directParentName = Path.GetDirectoryName(path);
			if (directParentName == null)
			{
				return string.Empty;
			}

			DirectoryInfo directParent = new DirectoryInfo(directParentName);
			string rootName = directParent.Root.FullName;

			DirectoryInfo currentDirectory = directParent;
			for (int i = 1; i < parentNumber; i++)
			{
				currentDirectory = currentDirectory.Parent;

				if (currentDirectory.FullName == rootName)
				{
					return string.Empty;
				}
			}

			if (currentDirectory.FullName == rootName)
			{
				return string.Empty;
			}

			return currentDirectory.Name;
		}
	}
}
