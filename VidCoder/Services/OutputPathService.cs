using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Text;
using System.Text.RegularExpressions;
using HandBrake.Interop.Interop;
using HandBrake.Interop.Interop.Json.Scan;
using HandBrake.Interop.Interop.Model.Encoding;
using Microsoft.AnyContainer;
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
		private Lazy<MainViewModel> mainViewModel = new Lazy<MainViewModel>(() => StaticResolver.Resolve<MainViewModel>());
		private ProcessingService processingService;
		private PresetsService presetsService;
		private PickersService pickersService;
		private IDriveService driveService = StaticResolver.Resolve<IDriveService>();

		public ProcessingService ProcessingService
		{
			get
			{
				if (this.processingService == null)
				{
					this.processingService = StaticResolver.Resolve<ProcessingService>();
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
					this.presetsService = StaticResolver.Resolve<PresetsService>();
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
					this.pickersService = StaticResolver.Resolve<PickersService>();
				}

				return this.pickersService;
			}
		}

		private string outputPath;
		public string OutputPath
		{
			get => this.outputPath;
			set => this.RaiseAndSetIfChanged(ref this.outputPath, value);
		}

		// The parent folder for the item (if it was inside a folder of files added in a batch)
		public string SourceParentFolder { get; set; }

		public string OldOutputPath { get; set; }

		private bool manualOutputPath;
		public bool ManualOutputPath
		{
			get => this.manualOutputPath;
			set => this.RaiseAndSetIfChanged(ref this.manualOutputPath, value);
		}

		public string NameFormatOverride { get; set; }

		private bool editingDestination;
		public bool EditingDestination
		{
			get => this.editingDestination;
			set => this.RaiseAndSetIfChanged(ref this.editingDestination, value);
		}

		private ReactiveCommand<Unit, Unit> pickOutputPath;
		public ReactiveCommand<Unit, Unit> PickOutputPath
		{
			get
			{
				return this.pickOutputPath ?? (this.pickOutputPath = ReactiveCommand.Create(
					() =>
					{
						string extensionDot = this.GetOutputExtension();
						string extension = this.GetOutputExtension(includeDot: false);
						string extensionLabel = extension.ToUpperInvariant();

						string initialFileName = null;
						if (!string.IsNullOrWhiteSpace(this.OutputPath) && !this.OutputPath.EndsWith("\\", StringComparison.Ordinal))
						{
							initialFileName = Path.GetFileName(this.OutputPath);
						}

						string newOutputPath = FileService.Instance.GetFileNameSave(
							Config.RememberPreviousFiles ? Config.LastOutputFolder : null,
							"Encode output location",
							initialFileName,
							extension,
							string.Format("{0} Files|*{1}", extensionLabel, extensionDot));
						this.SetManualOutputPath(newOutputPath, this.OutputPath);
					}));
			}
		}

		private ReactiveCommand<Unit, Unit> changeToAutomatic;
		public ReactiveCommand<Unit, Unit> ChangeToAutomatic
		{
			get
			{
				return this.changeToAutomatic ?? (this.changeToAutomatic = ReactiveCommand.Create(
					() =>
					{
						this.ManualOutputPath = false;
						this.NameFormatOverride = null;
						this.SourceParentFolder = null;
						this.GenerateOutputFileName();
					}));
			}
		}

		private ReactiveCommand<Unit, Unit> openPickerToDestination;
		public ReactiveCommand<Unit, Unit> OpenPickerToDestination
		{
			get
			{
				return this.openPickerToDestination ?? (this.openPickerToDestination = ReactiveCommand.Create(
					() =>
					{
						var pickerWindowViewModel = StaticResolver.Resolve<IWindowManager>().OpenOrFocusWindow<PickerWindowViewModel>();
						pickerWindowViewModel.View.ScrollDestinationSectionIntoView();
					}));
			}
		}

		// Resolves any conflicts for the given output path.
		// Returns a non-conflicting output path.
		// May return the same value if there are no conflicts.
		// null means cancel.
		public string ResolveOutputPathConflicts(
			string initialOutputPath,
			string sourcePath,
			HashSet<string> excludedPaths,
			bool isBatch,
			Picker picker,
			bool allowConflictDialog,
			bool allowQueueRemoval)
		{
			// If the output is going to be the same as the source path, add (Encoded) to it
			if (string.Compare(initialOutputPath, sourcePath, StringComparison.InvariantCultureIgnoreCase) == 0)
			{
				string outputFolder = Path.GetDirectoryName(initialOutputPath);
				string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(initialOutputPath);
				string extension = Path.GetExtension(initialOutputPath);

				initialOutputPath = Path.Combine(outputFolder, fileNameWithoutExtension + " (Encoded)" + extension);
			}

			HashSet<string> queuedFiles = excludedPaths;
			FileQueueCheckResult checkResult = Utilities.FileExistsOnDiskOrInQueue(initialOutputPath, queuedFiles);

			if (checkResult == FileQueueCheckResult.NotFound)
			{
				return initialOutputPath;
			}

			WhenFileExists preference;
			if (isBatch)
			{
				preference = picker.WhenFileExistsBatch;
			}
			else
			{
				preference = picker.WhenFileExistsSingle;
			}

			if (!allowConflictDialog && preference == WhenFileExists.Prompt)
			{
				preference = WhenFileExists.Overwrite;
			}

			switch (preference)
			{
				case WhenFileExists.Prompt:
					break;
				case WhenFileExists.Overwrite:
					if (checkResult == FileQueueCheckResult.InQueue && allowQueueRemoval)
					{
						this.RemoveQueuedJobWithOutputPath(initialOutputPath);
					}

					return initialOutputPath;
				case WhenFileExists.AutoRename:
					return FileUtilities.CreateUniqueFileName(initialOutputPath, queuedFiles);
				default:
					throw new ArgumentOutOfRangeException();
			}

			// Continue and prompt user for resolution
			string dialogMessageTemplate;
			string overwriteButtonText;
			if (checkResult == FileQueueCheckResult.OnDisk || this.IsConflictingJobInProgress(initialOutputPath))
			{
				dialogMessageTemplate = MiscRes.FileConflictWarning;
				overwriteButtonText = MiscRes.OverwriteButton;
			}
			else
			{
				dialogMessageTemplate = MiscRes.QueueFileConflictWarning;
				overwriteButtonText = MiscRes.ReplaceJobButton;
			}

			string dialogMessage = string.Format(dialogMessageTemplate, initialOutputPath);
			var conflictDialog = new CustomMessageDialogViewModel<FileConflictResolution>(
				MiscRes.FileConflictDialogTitle,
				dialogMessage,
				new List<CustomDialogButton<FileConflictResolution>>
				{
					new CustomDialogButton<FileConflictResolution>(FileConflictResolution.Overwrite, overwriteButtonText, ButtonType.Default),
					new CustomDialogButton<FileConflictResolution>(FileConflictResolution.AutoRename, MiscRes.AutoRenameButton),
					new CustomDialogButton<FileConflictResolution>(FileConflictResolution.Cancel, CommonRes.Cancel, ButtonType.Cancel),
				});

			StaticResolver.Resolve<IWindowManager>().OpenDialog(conflictDialog);

			switch (conflictDialog.Result)
			{
				case FileConflictResolution.Cancel:
					return null;
				case FileConflictResolution.Overwrite:
					if (checkResult == FileQueueCheckResult.InQueue && allowQueueRemoval)
					{
						this.RemoveQueuedJobWithOutputPath(initialOutputPath);
					}

					return initialOutputPath;
				case FileConflictResolution.AutoRename:
					return FileUtilities.CreateUniqueFileName(initialOutputPath, queuedFiles);
				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		private bool IsConflictingJobInProgress(string outputPath)
		{
			return this.processingService.EncodingJobs.Any(j => string.Compare(j.Job.FinalOutputPath, outputPath, StringComparison.OrdinalIgnoreCase) == 0);
		}

		private void RemoveQueuedJobWithOutputPath(string outputPath)
		{
			EncodeJobViewModel jobToRemove = this.processingService.EncodeQueue.Items.FirstOrDefault(j => string.Compare(j.Job.FinalOutputPath, outputPath, StringComparison.OrdinalIgnoreCase) == 0 && !j.Encoding);
			if (jobToRemove != null)
			{
				this.processingService.RemoveQueueJob(jobToRemove);
			}
		}

		public string ResolveOutputPathConflicts(string initialOutputPath, string sourcePath, bool isBatch, Picker picker, bool allowConflictDialog, bool allowQueueRemoval)
		{
			return this.ResolveOutputPathConflicts(initialOutputPath, sourcePath, this.ProcessingService.GetQueuedFiles(), isBatch, picker, allowConflictDialog, allowQueueRemoval);
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

			if (container.DefaultExtension == "mp4" && profile.PreferredExtension == VCOutputExtension.M4v)
			{
				extension = "m4v";
			}
			else
			{
				extension = container.DefaultExtension;
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
				if (this.mainViewModel.Value.HasVideoSource && string.IsNullOrEmpty(Path.GetFileName(oldOutputPath)))
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

		/// <summary>
		/// Generates an output file name for the "destination" text box.
		/// </summary>
		public void GenerateOutputFileName()
		{
			string fileName;

			MainViewModel main = this.mainViewModel.Value;

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

			if (!main.HasVideoSource)
			{
				string outputFolder = this.SelectedPickerOutputFolder;
				if (outputFolder != null)
				{
					this.OutputPath = outputFolder + (outputFolder.EndsWith(@"\", StringComparison.Ordinal) ? string.Empty : @"\");
				}

				return;
			}

			if (main.SourceName == null)
			{
				return;
			}

			if (main.RangeType == VideoRangeType.Chapters && (main.SelectedStartChapter == null || main.SelectedEndChapter == null))
			{
				return;
			}
			
			string nameFormat = null;
			Picker picker = this.pickersService.SelectedPicker.Picker;
			if (this.NameFormatOverride != null)
			{
				nameFormat = this.NameFormatOverride;
			}
			else
			{
				if (picker.UseCustomFileNameFormat)
				{
					nameFormat = picker.OutputFileNameFormat;
				}
			}

			fileName = this.BuildOutputFileName(
				main.SourcePath,
				// Change casing on DVD titles to be a little more friendly
				this.CleanUpSourceName(picker),
				main.SelectedTitle.Title.Index,
				main.SelectedTitle.Duration,
				main.RangeType,
				main.SelectedStartChapter?.ChapterNumber ?? 0,
				main.SelectedEndChapter?.ChapterNumber ?? 0,
				main.SelectedTitle.ChapterList.Count,
				main.TimeRangeStart,
				main.TimeRangeEnd,
				main.FramesRangeStart,
				main.FramesRangeEnd,
				nameFormat,
				multipleTitlesOnSource: main.ScanInstance.Titles.TitleList.Count > 1,
				picker: picker);

			string extension = this.GetOutputExtension();

			string outputPathCandidate = this.BuildOutputPath(fileName, extension, sourcePath: main.SourcePath);
			this.OutputPath = this.ResolveOutputPathConflicts(outputPathCandidate, main.SourcePath, false, picker, allowConflictDialog: false, allowQueueRemoval: false);

			// If we've pushed a new name into the destination text box, we need to update the "baseline" name so the
			// auto-generated name doesn't get mistakenly labeled as manual when focus leaves it
			if (this.EditingDestination)
			{
				this.OldOutputPath = this.OutputPath;
			}
		}

		// Gets the output folder from the selected picker, falling back to My Videos if null.
		private string SelectedPickerOutputFolder
		{
			get
			{
				return GetOutputFolderForPicker(null);
			}
		}

		// Gets the output folder from the picker, falling back to My Videos if null.
		private string GetOutputFolderForPicker(Picker picker)
		{
			Picker nonNullPicker = picker ?? this.PickersService.SelectedPicker.Picker;

			if (nonNullPicker.OutputDirectory != null)
			{
				return nonNullPicker.OutputDirectory;
			}

			return Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
		}

		public string GetOutputFolder(string sourcePath, string sourceParentFolder = null, Picker picker = null)
		{
			bool usedSourceDirectory = false;

			if (picker == null)
			{
				picker = this.PickersService.SelectedPicker.Picker;
			}

			string outputFolder = this.GetOutputFolderForPicker(picker);

			if (picker.OutputToSourceDirectory)
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

			bool preserveFolderStructure = picker.PreserveFolderStructureInBatch;
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
				return Path.Combine(outputFolder, fileName + extension);
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

		/// <summary>
		///	Update the source name based on the picker settings.
		/// </summary>
		/// <param name="picker">The picker.</param>
		/// <returns>The cleaned up source name</returns>
		public string CleanUpSourceName(Picker picker)
		{
			MainViewModel main = this.mainViewModel.Value;
			return this.CleanUpSourceName(picker, main.SourceName);
		}

		/// <summary>
		/// Update the given source name based on the picker settings.
		/// </summary>
		/// <param name="picker">The picker.</param>
		/// <param name="sourceName">The source name to convert.</param>
		/// <returns>The cleaned up source name</returns>
		public string CleanUpSourceName(Picker picker, string sourceName)
		{
			if (sourceName == null || !picker.ChangeWordSeparator && !picker.ChangeTitleCaptialization)
			{
				return sourceName;
			}

			var wordBreakCharacterSet = new HashSet<char>(picker.WordBreakCharacters.Select(character => character[0]));

			// Make a pass to see if it's all uppercase or all lowercase
			bool isAllLowercase = true;
			bool isAllUppercase = true;

			if (picker.ChangeTitleCaptialization && picker.OnlyChangeTitleCapitalizationWhenAllSame)
			{
				foreach (char c in sourceName)
				{
					if (char.IsUpper(c))
					{
						isAllLowercase = false;
					}
					else if (char.IsLower(c))
					{
						isAllUppercase = false;
					}
				}
			}

			bool isCasingAllSame = isAllLowercase || isAllUppercase;
			bool isFirstWord = true;

			// Build the output
			var output = new StringBuilder();
			var currentWord = new StringBuilder();
			foreach (char c in sourceName)
			{
				if (wordBreakCharacterSet.Contains(c))
				{
					// Write out the current word
					if (currentWord.Length > 0)
					{
						this.WriteTitleWord(output, currentWord, picker, isCasingAllSame, isFirstWord);
						isFirstWord = false;
						currentWord.Clear();
					}

					// Write out the word break character
					char wordBreakCharacter = picker.ChangeWordSeparator ? picker.WordSeparator[0] : c;
					output.Append(wordBreakCharacter);
				}
				else
				{
					currentWord.Append(c);
				}
			}

			if (currentWord.Length > 0)
			{
				this.WriteTitleWord(output, currentWord, picker, isCasingAllSame, isFirstWord);
			}

			return output.ToString();
		}

		private void WriteTitleWord(StringBuilder output, StringBuilder word, Picker picker, bool isCasingAllSame, bool isFirstWord)
		{
			if (picker.ChangeTitleCaptialization && (isCasingAllSame || !picker.OnlyChangeTitleCapitalizationWhenAllSame))
			{
				string inputWord = word.ToString();
				string outputWord;
				if (picker.TitleCapitalization == TitleCapitalizationChoice.EveryWord || isFirstWord)
				{
					outputWord = char.ToUpper(inputWord[0]) + inputWord.Substring(1).ToLower();
				}
				else
				{
					outputWord = inputWord.ToLower();
				}

				output.Append(outputWord);
			}
			else
			{
				output.Append(word);
			}
		}

		/// <summary>
		/// Replace arguments with the currently loaded source.
		/// </summary>
		/// <param name="nameFormat">The name format to use.</param>
		/// <param name="picker">The picker.</param>
		/// <returns>The new name with arguments replaced.</returns>
		public string ReplaceArguments(string nameFormat, Picker picker = null)
		{
			MainViewModel main = this.mainViewModel.Value;

			return this.ReplaceArguments(
				main.SourcePath,
				this.CleanUpSourceName(picker),
				main.SelectedTitle.Index,
				main.SelectedTitle.Duration,
				main.RangeType,
				main.SelectedStartChapter.ChapterNumber,
				main.SelectedEndChapter.ChapterNumber,
				main.SelectedTitle.ChapterList.Count,
				main.TimeRangeStart,
				main.TimeRangeEnd,
				main.FramesRangeStart,
				main.FramesRangeEnd,
				nameFormat,
				multipleTitlesOnSource: main.ScanInstance.Titles.TitleList.Count > 1,
				picker: picker);
		}

		/// <summary>
		/// Replace arguments with the given job information.
		/// </summary>
		/// <param name="nameFormat">The name format to use.</param>
		/// <param name="picker">The picker.</param>
		/// <param name="jobViewModel">The job to pick information from.</param>
		/// <returns>The string with arguments replaced.</returns>
		public string ReplaceArguments(string nameFormat, Picker picker, EncodeJobViewModel jobViewModel)
		{
			// The jobViewModel might have null VideoSource and VideoSourceMetadata from an earlier version < 4.17.

			VCJob job = jobViewModel.Job;
			SourceTitle title = jobViewModel.VideoSource?.Titles.Single(t => t.Index == job.Title);

			string sourceName = jobViewModel.VideoSourceMetadata != null ? jobViewModel.VideoSourceMetadata.Name : string.Empty;
			TimeSpan titleDuration = title?.Duration.ToSpan() ?? TimeSpan.Zero;
			int chapterCount = title?.ChapterList.Count ?? 0;
			bool hasMultipleTitles = jobViewModel.VideoSource != null && jobViewModel.VideoSource.Titles.Count > 1;

			return this.ReplaceArguments(
				job.SourcePath,
				sourceName,
				job.Title,
				titleDuration,
				job.RangeType,
				job.ChapterStart,
				job.ChapterEnd,
				chapterCount,
				TimeSpan.FromSeconds(job.SecondsStart),
				TimeSpan.FromSeconds(job.SecondsEnd),
				job.FramesStart,
				job.FramesEnd,
				nameFormat,
				hasMultipleTitles,
				picker);
		}

		private string ReplaceArguments(
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

			if (!string.IsNullOrWhiteSpace(nameFormatOverride) || picker.UseCustomFileNameFormat)
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
				else if (!string.IsNullOrWhiteSpace(picker.OutputFileNameFormat))
				{
					fileName = picker.OutputFileNameFormat;
				}
				else
				{
					fileName = "{source}";
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
			return fileName;
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
			
			return FileUtilities.CleanFileName(
				ReplaceArguments(sourcePath, sourceName, title, titleDuration, rangeType, startChapter,endChapter, totalChapters, startTime, endTime, startFrame, endFrame, nameFormatOverride, multipleTitlesOnSource, picker), 
				allowBackslashes: true);
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

				int digits = int.Parse(capture.Value, CultureInfo.InvariantCulture);

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

				inputString = inputString.Substring(0, replaceIndex) + FindParent(path, int.Parse(capture.Value, CultureInfo.InvariantCulture)) + inputString.Substring(replaceIndex + replaceLength);
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
