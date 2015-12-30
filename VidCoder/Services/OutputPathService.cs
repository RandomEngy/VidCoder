using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Text.RegularExpressions;
using HandBrake.ApplicationServices.Interop;
using HandBrake.ApplicationServices.Interop.Model.Encoding;
using ReactiveUI;
using VidCoder.Extensions;
using VidCoder.Model;
using VidCoder.Resources;
using VidCoder.Services.Windows;
using VidCoder.ViewModel;
using VidCoderCommon.Extensions;
using VidCoderCommon.Model;

namespace VidCoder.Services
{
	/// <summary>
	/// Controls automatic naming logic for the encoding output path.
	/// </summary>
	public class OutputPathService : ReactiveObject
	{
		private MainViewModel main = Ioc.Get<MainViewModel>();
		private ProcessingService processingService;
		private PresetsService presetsService;
		private PickersService pickersService;
		private IDriveService driveService = Ioc.Get<IDriveService>();

		public OutputPathService()
		{
			this.defaultOutputFolder = Config.AutoNameOutputFolder;

			// OutputFolderChosen
			this.WhenAnyValue(x => x.DefaultOutputFolder)
				.Select(defaultOutputFolder =>
				{
					return !string.IsNullOrEmpty(defaultOutputFolder);
				}).ToProperty(this, x => x.OutputFolderChosen, out this.outputFolderChosen);

			this.PickDefaultOutputFolder = ReactiveCommand.Create();
			this.PickDefaultOutputFolder.Subscribe(_ => this.PickDefaultOutputFolderImpl());

			this.PickOutputPath = ReactiveCommand.Create(this.WhenAnyValue(x => x.OutputFolderChosen));
			this.PickOutputPath.Subscribe(_ => this.PickOutputPathImpl());
		}

		public ProcessingService ProcessingService
		{
			get
			{
				if (this.processingService == null)
				{
					this.processingService = Ioc.Get<ProcessingService>();
				}

				return this.processingService;
			}
		}

		public PresetsService PresetsService
		{
			get
			{
				if (this.presetsService == null)
				{
					this.presetsService = Ioc.Get<PresetsService>();
				}

				return this.presetsService;
			}
		}

		public PickersService PickersService
		{
			get 
			{
				if (this.pickersService == null)
				{
					this.pickersService = Ioc.Get<PickersService>();
				}

				return this.pickersService;
			}
		}

		private string outputPath;
		public string OutputPath
		{
			get { return this.outputPath; }
			set { this.RaiseAndSetIfChanged(ref this.outputPath, value); }
		}

		private string defaultOutputFolder;
		private string DefaultOutputFolder
		{
			get { return this.defaultOutputFolder; }
			set { this.RaiseAndSetIfChanged(ref this.defaultOutputFolder, value); }
		}

		// The parent folder for the item (if it was inside a folder of files added in a batch)
		public string SourceParentFolder { get; set; }

		public string OldOutputPath { get; set; }

		public bool ManualOutputPath { get; set; }

		public string NameFormatOverride { get; set; }

		private bool editingDestination;
		public bool EditingDestination
		{
			get { return this.editingDestination; }
			set { this.RaiseAndSetIfChanged(ref this.editingDestination, value); }
		}

		private ObservableAsPropertyHelper<bool> outputFolderChosen;
		public bool OutputFolderChosen => this.outputFolderChosen.Value;

		public ReactiveCommand<object> PickDefaultOutputFolder { get; }
		public bool PickDefaultOutputFolderImpl()
		{
			string newOutputFolder = FileService.Instance.GetFolderName(null, MainRes.OutputDirectoryPickerText);

			if (newOutputFolder != null)
			{
				Config.AutoNameOutputFolder = newOutputFolder;
				this.NotifyDefaultOutputFolderChanged();
			}

			return newOutputFolder != null;
		}

		public ReactiveCommand<object> PickOutputPath { get; }
		private void PickOutputPathImpl()
		{
			string extensionDot = this.GetOutputExtension();
			string extension = this.GetOutputExtension(includeDot: false);
			string extensionLabel = extension.ToUpperInvariant();

			string newOutputPath = FileService.Instance.GetFileNameSave(
				Config.RememberPreviousFiles ? Config.LastOutputFolder : null,
				"Encode output location",
				null,
				extension,
				string.Format("{0} Files|*{1}", extensionLabel, extensionDot));
			this.SetManualOutputPath(newOutputPath, this.OutputPath);
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
				preference = CustomConfig.WhenFileExistsBatch;
			}
			else
			{
				preference = CustomConfig.WhenFileExists;
			}

			switch (preference)
			{
				case WhenFileExists.Prompt:
					break;
				case WhenFileExists.Overwrite:
					return initialOutputPath;
				case WhenFileExists.AutoRename:
					return FileUtilities.CreateUniqueFileName(initialOutputPath, queuedFiles);
				default:
					throw new ArgumentOutOfRangeException();
			}

			// Continue and prompt user for resolution

			var conflictDialog = new FileConflictDialogViewModel(initialOutputPath, (bool)conflict);
			Ioc.Get<IWindowManager>().OpenDialog(conflictDialog);

			switch (conflictDialog.FileConflictResolution)
			{
				case FileConflictResolution.Cancel:
					return null;
				case FileConflictResolution.Overwrite:
					return initialOutputPath;
				case FileConflictResolution.AutoRename:
					return FileUtilities.CreateUniqueFileName(initialOutputPath, queuedFiles);
				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		public string ResolveOutputPathConflicts(string initialOutputPath, bool isBatch)
		{
			return ResolveOutputPathConflicts(initialOutputPath, this.ProcessingService.GetQueuedFiles(), isBatch);
		}

		/// <summary>
		/// Gets the extension that should be used for the current encoding profile.
		/// </summary>
		/// <returns>The extension that should be used for current encoding profile.</returns>
		public string GetOutputExtension(bool includeDot = true)
		{
			VCProfile profile = this.PresetsService.SelectedPreset.Preset.EncodingProfile;
			return GetExtensionForProfile(profile, includeDot);
		}

		public static string GetExtensionForProfile(VCProfile profile, bool includeDot = true)
		{
			HBContainer container = HandBrakeEncoderHelpers.GetContainer(profile.ContainerName);

			if (container == null)
			{
				throw new ArgumentException("Could not find container with name " + profile.ContainerName, nameof(profile));
			}

			string extension;

			if (container.DefaultExtension == "mkv")
			{
				extension = "mkv";
			}
			else if (container.DefaultExtension == "mp4" && profile.PreferredExtension == VCOutputExtension.Mp4)
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

				if (Config.RememberPreviousFiles)
				{
					Config.LastOutputFolder = outputDirectory;
				}

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

		public void NotifyDefaultOutputFolderChanged()
		{
			this.DefaultOutputFolder = Config.AutoNameOutputFolder;
			this.GenerateOutputFileName();
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
				string outputFolder = this.PickerOutputFolder;
				if (outputFolder != null)
				{
					this.OutputPath = outputFolder + (outputFolder.EndsWith(@"\", StringComparison.Ordinal) ? string.Empty : @"\");
				}

				return;
			}

			if (this.main.SourceName == null || this.main.SelectedStartChapter == null || this.main.SelectedEndChapter == null)
			{
				return;
			}

			if (string.IsNullOrEmpty(Config.AutoNameOutputFolder))
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
				this.main.SelectedTitle.Index,
				this.main.SelectedTitle.Duration.ToSpan(),
				this.main.RangeType,
				this.main.SelectedStartChapter.ChapterNumber,
				this.main.SelectedEndChapter.ChapterNumber,
				this.main.SelectedTitle.ChapterList.Count,
				this.main.TimeRangeStart,
				this.main.TimeRangeEnd,
				this.main.FramesRangeStart,
				this.main.FramesRangeEnd,
				this.NameFormatOverride,
				multipleTitlesOnSource: this.main.ScanInstance.Titles.TitleList.Count > 1,
				picker: null);

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
			if (dvdSourceName.Any(char.IsLower))
			{
				// If we find any lowercase letters, this is not a DVD/Blu-ray disc name and
				// does not need any cleanup
				return dvdSourceName;
			}

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

		// Gets the default output folder, considering the picker and config
		public string PickerOutputFolder
		{
			get
			{
				Picker picker = this.PickersService.SelectedPicker.Picker;
				if (picker.OutputDirectoryOverrideEnabled)
				{
					return picker.OutputDirectoryOverride;
				}

				return Config.AutoNameOutputFolder;
			}
		}

		public string GetOutputFolder(string sourcePath, string sourceParentFolder = null, Picker picker = null)
		{
			string outputFolder = this.PickerOutputFolder;

			bool usedSourceDirectory = false;

			if (picker == null)
			{
				picker = this.PickersService.SelectedPicker.Picker;
			}

			if (picker.OutputToSourceDirectory ?? Config.OutputToSourceDirectory)
			{
				// Use the source directory if we can
				string sourceRoot = Path.GetPathRoot(sourcePath);
				IList<DriveInfo> driveInfo = this.driveService.GetDriveInformation();
				DriveInfo matchingDrive = driveInfo.FirstOrDefault(d => string.Compare(d.RootDirectory.FullName, sourceRoot, StringComparison.OrdinalIgnoreCase) == 0);

				string sourceDirectory = Path.GetDirectoryName(sourcePath);

				// Use the source directory if it exists and not on an optical drive
				if (!string.IsNullOrEmpty(sourceDirectory) && (matchingDrive == null || matchingDrive.DriveType != DriveType.CDRom))
				{
					outputFolder = sourceDirectory;
					usedSourceDirectory = true;
				}
			}

			bool preserveFolderStructure = picker.PreserveFolderStructureInBatch ?? Config.PreserveFolderStructureInBatch;
			if (!usedSourceDirectory && sourceParentFolder != null && preserveFolderStructure)
			{
				// Tack on some subdirectories if we have a parent folder specified and it's enabled, and we didn't use the source directory
				string sourceDirectory = Path.GetDirectoryName(sourcePath);

				if (sourceParentFolder.Length > sourceDirectory.Length)
				{
					throw new InvalidOperationException("sourceParentFolder (" + sourceParentFolder + ") is longer than sourceDirectory (" + sourceDirectory +")");
				}

				if (string.Compare(
					sourceDirectory.Substring(0, sourceParentFolder.Length),
					sourceParentFolder, 
					CultureInfo.InvariantCulture, 
					CompareOptions.IgnoreCase) != 0)
				{
					throw new InvalidOperationException("sourceParentFolder (" + sourceParentFolder + ") is not a parent of sourceDirectory (" + sourceDirectory + ")");
				}

				if (sourceParentFolder.Length < sourceDirectory.Length)
				{
					outputFolder = outputFolder + sourceDirectory.Substring(sourceParentFolder.Length);
				}
			}

			return outputFolder;
		}

		public string BuildOutputPath(string fileName, string extension, string sourcePath, string outputFolder = null)
		{
			if (outputFolder == null)
			{
				outputFolder = this.GetOutputFolder(sourcePath);
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

		public string BuildOutputFileName(
			string sourcePath,
			string sourceName, 
			int title, 
			TimeSpan titleDuration,
			int totalChapters,
			string nameFormatOverride = null,
			bool multipleTitlesOnSource = false,
			Picker picker = null)
		{
			return this.BuildOutputFileName(
				sourcePath,
				sourceName,
				title,
				titleDuration,
				VideoRangeType.Chapters,
				1,
				totalChapters,
				totalChapters,
				TimeSpan.Zero,
				TimeSpan.Zero,
				0,
				0,
				nameFormatOverride,
				multipleTitlesOnSource,
				picker);
		}

		public string BuildOutputFileName(
			string sourcePath, 
			string sourceName, 
			int title, 
			TimeSpan titleDuration, 
			VideoRangeType rangeType, 
			int startChapter, 
			int endChapter, 
			int totalChapters, 
			TimeSpan startTime, 
			TimeSpan endTime, 
			int startFrame, 
			int endFrame,
			string nameFormatOverride, 
			bool multipleTitlesOnSource,
			Picker picker)
		{
			string fileName;
			if (picker == null)
			{
				picker = this.PickersService.SelectedPicker.Picker;
			}

			if (Config.AutoNameCustomFormat || !string.IsNullOrWhiteSpace(nameFormatOverride))
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
						rangeString = startTime.ToFileName() + "-" + endTime.ToFileName();
						break;
					case VideoRangeType.Frames:
						rangeString = startFrame + "-" + endFrame;
						break;
				}

				if (!string.IsNullOrWhiteSpace(nameFormatOverride))
				{
					fileName = nameFormatOverride;
				}
				else if (picker.NameFormatOverrideEnabled)
				{
					fileName = picker.NameFormatOverride;
				}
				else
				{
					fileName = Config.AutoNameCustomFormatString;
				}

				fileName = fileName.Replace("{source}", sourceName);
				fileName = ReplaceTitles(fileName, title);
				fileName = fileName.Replace("{range}", rangeString);

				fileName = fileName.Replace("{titleduration}", titleDuration.ToFileName());

				// {chapters} is deprecated in favor of {range} but we replace here for backwards compatibility.
				fileName = fileName.Replace("{chapters}", rangeString);

				fileName = fileName.Replace("{preset}", this.PresetsService.SelectedPreset.Preset.Name);
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
					VCProfile profile = this.PresetsService.SelectedPreset.Preset.EncodingProfile;
					double quality = 0;
					switch (profile.VideoEncodeRateType)
					{
                        case VCVideoEncodeRateType.ConstantQuality:
							quality = profile.Quality;
							break;
                        case VCVideoEncodeRateType.AverageBitrate:
							quality = profile.VideoBitrate;
							break;
                        case VCVideoEncodeRateType.TargetSize:
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
				if (multipleTitlesOnSource)
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
						if (startTime > TimeSpan.Zero || (endTime < titleDuration && (titleDuration - endTime >= TimeSpan.FromSeconds(1) || endTime.Milliseconds != 0)))
						{
							rangeSection = " - " + startTime.ToFileName() + "-" + endTime.ToFileName();
						}

						break;
					case VideoRangeType.Frames:
						rangeSection = " - Frames " + startFrame + "-" + endFrame;
						break;
				}

				fileName = sourceName + titleSection + rangeSection;
			}

			return FileUtilities.CleanFileName(fileName, allowBackslashes: true);
		}

		public bool PathIsValid()
		{
			return Utilities.IsValidFullPath(this.OutputPath);
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
