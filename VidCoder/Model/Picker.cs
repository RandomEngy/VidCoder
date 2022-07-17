using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Text.Json.Serialization;
using ReactiveUI;
using VidCoder.Resources;
using VidCoderCommon.Model;

namespace VidCoder.Model
{
    public class Picker : ReactiveObject
    {
		public Picker()
		{
			this.WhenAnyValue(x => x.Name)
				.Select(name =>
				{
					if (this.IsDefault)
					{
						return CommonRes.Default;
					}

					return name;
				})
				.ToProperty(this, x => x.DisplayName, out this.displayName);
		}

        /// <summary>
        /// Gets or sets a value indicating whether this preset is the special "None" preset
        /// </summary>
		public bool IsDefault { get; set; }

	    private bool isModified;

		public bool IsModified
		{
			get => this.isModified;
			set => this.RaiseAndSetIfChanged(ref this.isModified, value);
		}

		private string name;

		public string Name
		{
			get => this.name;
			set => this.RaiseAndSetIfChanged(ref this.name, value);
		}

		public string OutputDirectory { get; set; }

		public bool UseCustomFileNameFormat { get; set; }

		public string OutputFileNameFormat { get; set; } 

		// Used to be nullable
		[JsonPropertyName("OutputToSourceDirectory2")]
		public bool OutputToSourceDirectory { get; set; }

		// Used to be nullable
		[JsonPropertyName("PreserveFolderStructureInBatch2")]
		public bool PreserveFolderStructureInBatch { get; set; }

		public WhenFileExists WhenFileExistsSingle { get; set; } = WhenFileExists.Prompt;

		public WhenFileExists WhenFileExistsBatch { get; set; } = WhenFileExists.AutoRename;

		/// <summary>
		/// True if we want to update the word separator character in the title.
		/// </summary>
		public bool ChangeWordSeparator { get; set; } = true;

		/// <summary>
		/// The character to insert between words in titles.
		/// </summary>
		public string WordSeparator { get; set; } = " ";

		private List<string> wordBreakCharacters = new List<string> { " ", "_" };

		/// <summary>
		/// The characters to use to separate words in titles.
		/// </summary>
		public List<string> WordBreakCharacters
		{
			get => this.wordBreakCharacters;
			set => this.RaiseAndSetIfChanged(ref this.wordBreakCharacters, value);
		}

		/// <summary>
		/// True to update the title capitalization.
		/// </summary>
		public bool ChangeTitleCaptialization { get; set; } = true;

		/// <summary>
		/// True if we should only change the title capitalization if it is ALL UPPERCASE or all lowercase.
		/// </summary>
		public bool OnlyChangeTitleCapitalizationWhenAllSame { get; set; } = true;

		public TitleCapitalizationChoice TitleCapitalization { get; set; } = TitleCapitalizationChoice.EveryWord;

		/// <summary>
		/// The extensions to include when adding video files from a folder.
		/// </summary>
		public string VideoFileExtensions { get; set; } = "avi, mkv, mp4, m4v, mpg, mpeg, mov, webm, wmv";

		public bool IgnoreFilesBelowMbEnabled { get; set; }

		public int IgnoreFilesBelowMb { get; set; } = 30;

		private bool titleRangeSelectEnabled;
		public bool TitleRangeSelectEnabled
		{
			get => this.titleRangeSelectEnabled;
			set => this.RaiseAndSetIfChanged(ref this.titleRangeSelectEnabled, value);
		}

		private int titleRangeSelectStartMinutes = 40;
		public int TitleRangeSelectStartMinutes
		{
			get => this.titleRangeSelectStartMinutes;
			set => this.RaiseAndSetIfChanged(ref this.titleRangeSelectStartMinutes, value);
		}

		private int titleRangeSelectEndMinutes = 50;
		public int TitleRangeSelectEndMinutes
		{
			get => this.titleRangeSelectEndMinutes;
			set => this.RaiseAndSetIfChanged(ref this.titleRangeSelectEndMinutes, value);
		}

		private PickerTimeRangeMode pickerTimeRangeMode;
		public PickerTimeRangeMode PickerTimeRangeMode
		{
			get => this.pickerTimeRangeMode;
			set => this.RaiseAndSetIfChanged(ref this.pickerTimeRangeMode, value);
		}

		private int? chapterRangeStart;
		public int? ChapterRangeStart
		{
			get => this.chapterRangeStart;
			set => this.RaiseAndSetIfChanged(ref this.chapterRangeStart, value);
		}

		private int? chapterRangeEnd;
		public int? ChapterRangeEnd
		{
			get => this.chapterRangeEnd;
			set => this.RaiseAndSetIfChanged(ref this.chapterRangeEnd, value);
		}

		private TimeSpan timeRangeStart;
	    public TimeSpan TimeRangeStart
		{
			get => this.timeRangeStart;
			set => this.RaiseAndSetIfChanged(ref this.timeRangeStart, value);
		}

		private TimeSpan timeRangeEnd = TimeSpan.FromMinutes(10);
	    public TimeSpan TimeRangeEnd
		{
			get => this.timeRangeEnd;
			set => this.RaiseAndSetIfChanged(ref this.timeRangeEnd, value);
		}

		public AudioSelectionMode AudioSelectionMode { get; set; } = AudioSelectionMode.Disabled;

	    public string AudioIndices { get; set; } = "1";

		// Applies only with AutoAudioType.Language
		private List<string> audioLanguageCodes;
		public List<string> AudioLanguageCodes
		{
			get => this.audioLanguageCodes;
			set => this.RaiseAndSetIfChanged(ref this.audioLanguageCodes, value);
		}

		// Applies only with AutoAudioType.Language
		public bool AudioLanguageAll { get; set; }

		public bool UseCustomAudioTrackNames { get; set; }

		public List<string> AudioTrackNames { get; set; }

		public SubtitleSelectionMode SubtitleSelectionMode { get; set; } = SubtitleSelectionMode.Disabled;

		public bool SubtitleAddForeignAudioScan { get; set; } = true;

	    public string SubtitleIndices { get; set; } = "1";

		public int? SubtitleDefaultIndex { get; set; }

		// Applies only with AutoSubtitleType.Language
		private List<string> subtitleLanguageCodes;
		public List<string> SubtitleLanguageCodes
		{
			get => this.subtitleLanguageCodes;
			set => this.RaiseAndSetIfChanged(ref this.subtitleLanguageCodes, value);
		}

		// Applies only with AutoSubtitleType.Language
		public bool SubtitleLanguageAll { get; set; }

        // Applies only with AutoSubtitleType.Language
        // Default true
		public bool SubtitleLanguageOnlyIfDifferent { get; set; } = true;

		// Applies when at least 1 subtitle can be picked.
		public bool SubtitleDefault { get; set; }

        // Applies when any subtitles can be picked.
        public bool SubtitleForcedOnly { get; set; }

		public SubtitleBurnInSelection SubtitleBurnInSelection { get; set; } = SubtitleBurnInSelection.ForeignAudioTrack;

		public bool UseCustomSubtitleTrackNames { get; set; }

		public List<string> SubtitleTrackNames { get; set; }

		public bool EnableExternalSubtitleImport { get; set; }

		public string ExternalSubtitleImportLanguage { get; set; } = "eng";

		public bool ExternalSubtitleImportDefault { get; set; }

		public bool ExternalSubtitleImportBurnIn { get; set; }

		/// <summary>
		/// True to pass through video metadata like actors, release date, director.
		/// </summary>
		public bool PassThroughMetadata { get; set; } = true;

		private bool useEncodingPreset;
		public bool UseEncodingPreset
		{
			get => this.useEncodingPreset;
			set => this.RaiseAndSetIfChanged(ref this.useEncodingPreset, value);
		}

		private string encodingPreset;
		public string EncodingPreset
		{
			get => this.encodingPreset;
			set => this.RaiseAndSetIfChanged(ref this.encodingPreset, value);
		}

		public bool AutoQueueOnScan { get; set; }

		public bool AutoEncodeOnScan { get; set; }

		public bool PostEncodeActionEnabled { get; set; }

		public string PostEncodeExecutable { get; set; }

		public string PostEncodeArguments { get; set; } = "\"{file}\"";

		public SourceFileRemoval SourceFileRemoval { get; set; } = SourceFileRemoval.Disabled;

		public SourceFileRemovalTiming SourceFileRemovalTiming { get; set; } = SourceFileRemovalTiming.AfterClearingCompletedItems;

		public bool SourceFileRemovalConfirmation { get; set; } = true;

		private ObservableAsPropertyHelper<string> displayName;

		[JsonIgnore]
		public string DisplayName => this.displayName.Value;

		/// <summary>
		/// Obsolete properties will be put in here.
		/// </summary>
		[JsonExtensionData]
		public Dictionary<string, object> ExtensionData { get; set; }
	}
}
