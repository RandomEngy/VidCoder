using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Runtime.Serialization;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using ReactiveUI;
using VidCoder.Resources;
using VidCoderCommon.Model;

namespace VidCoder.Model
{
	[JsonObject]
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
		[JsonProperty]
		public bool IsDefault { get; set; }

	    private bool isModified;

		[JsonProperty]
		public bool IsModified
	    {
		    get { return this.isModified; }
			set { this.RaiseAndSetIfChanged(ref this.isModified, value); }
	    }

		private string name;

		[JsonProperty]
		public string Name
		{
			get { return this.name; }
			set { this.RaiseAndSetIfChanged(ref this.name, value); }
		}

		[JsonProperty]
		public string OutputDirectory { get; set; }

		[JsonProperty]
		public bool UseCustomFileNameFormat { get; set; }

		[JsonProperty]
		public string OutputFileNameFormat { get; set; }

		[JsonProperty]
		public TitleCapitalizationChoice TitleCapitalization { get; set; } = TitleCapitalizationChoice.EveryWord;

		// Used to be nullable
		[JsonProperty("OutputToSourceDirectory2")]
		public bool OutputToSourceDirectory { get; set; }

		// Used to be nullable
		[JsonProperty("PreserveFolderStructureInBatch2")]
		public bool PreserveFolderStructureInBatch { get; set; }

		[JsonProperty]
		public WhenFileExists WhenFileExistsSingle { get; set; } = WhenFileExists.Prompt;

		[JsonProperty]
		public WhenFileExists WhenFileExistsBatch { get; set; } = WhenFileExists.AutoRename;

		private bool titleRangeSelectEnabled;
		[JsonProperty]
		public bool TitleRangeSelectEnabled
		{
			get { return this.titleRangeSelectEnabled; }
			set { this.RaiseAndSetIfChanged(ref this.titleRangeSelectEnabled, value); }
		}

		private int titleRangeSelectStartMinutes = 40;
		[JsonProperty]
		public int TitleRangeSelectStartMinutes
		{
			get { return this.titleRangeSelectStartMinutes; }
			set { this.RaiseAndSetIfChanged(ref this.titleRangeSelectStartMinutes, value); }
		}

		private int titleRangeSelectEndMinutes = 50;
		[JsonProperty]
		public int TitleRangeSelectEndMinutes
		{
			get { return this.titleRangeSelectEndMinutes; }
			set { this.RaiseAndSetIfChanged(ref this.titleRangeSelectEndMinutes, value); }
		}

		private PickerTimeRangeMode pickerTimeRangeMode;
	    [JsonProperty]
		public PickerTimeRangeMode PickerTimeRangeMode
		{
			get { return this.pickerTimeRangeMode; }
			set { this.RaiseAndSetIfChanged(ref this.pickerTimeRangeMode, value); }
		}

		private int? chapterRangeStart;
	    [JsonProperty]
		public int? ChapterRangeStart
		{
			get { return this.chapterRangeStart; }
			set { this.RaiseAndSetIfChanged(ref this.chapterRangeStart, value); }
		}

		private int? chapterRangeEnd;
	    [JsonProperty]
		public int? ChapterRangeEnd
		{
			get { return this.chapterRangeEnd; }
			set { this.RaiseAndSetIfChanged(ref this.chapterRangeEnd, value); }
		}

	    private TimeSpan timeRangeStart;
	    [JsonProperty]
	    public TimeSpan TimeRangeStart
	    {
		    get { return this.timeRangeStart; }
		    set { this.RaiseAndSetIfChanged(ref this.timeRangeStart, value); }
		}

	    private TimeSpan timeRangeEnd = TimeSpan.FromMinutes(10);
	    [JsonProperty]
	    public TimeSpan TimeRangeEnd
	    {
		    get { return this.timeRangeEnd; }
		    set { this.RaiseAndSetIfChanged(ref this.timeRangeEnd, value); }
	    }

		[JsonProperty]
		public AudioSelectionMode AudioSelectionMode { get; set; } = AudioSelectionMode.Disabled;

	    [JsonProperty]
	    public string AudioIndices { get; set; } = "1";

		// Applies only with AutoAudioType.Language
		private List<string> audioLanguageCodes;
		[JsonProperty]
		public List<string> AudioLanguageCodes
		{
			get { return this.audioLanguageCodes; }
			set { this.RaiseAndSetIfChanged(ref this.audioLanguageCodes, value); }
		}

		// Applies only with AutoAudioType.Language
		[JsonProperty]
		public bool AudioLanguageAll { get; set; }

		[JsonProperty]
		public SubtitleSelectionMode SubtitleSelectionMode { get; set; } = SubtitleSelectionMode.Disabled;

	    [JsonProperty]
	    public string SubtitleIndices { get; set; } = "1";

	    [JsonProperty]
		public int? SubtitleDefaultIndex { get; set; }

		// Applies only with AutoSubtitleType.Language
		private List<string> subtitleLanguageCodes;
		[JsonProperty]
		public List<string> SubtitleLanguageCodes
		{
			get { return this.subtitleLanguageCodes; }
			set { this.RaiseAndSetIfChanged(ref this.subtitleLanguageCodes, value); }
		}

		// Applies only with AutoSubtitleType.Language
		[JsonProperty]
		public bool SubtitleLanguageAll { get; set; }

        // Applies only with AutoSubtitleType.Language
        // Default true
		[JsonProperty]
		public bool SubtitleLanguageOnlyIfDifferent { get; set; } = true;

		// Applies when at least 1 subtitle can be picked.
		[JsonProperty]
		public bool SubtitleDefault { get; set; }

        // Applies when any subtitles can be picked.
        [JsonProperty]
        public bool SubtitleForcedOnly { get; set; }

        // Applies when 0-1 subtitles can be picked.
        [JsonProperty]
		public bool SubtitleBurnIn { get; set; }

		private bool useEncodingPreset;
		[JsonProperty]
		public bool UseEncodingPreset
		{
			get { return this.useEncodingPreset; }
			set { this.RaiseAndSetIfChanged(ref this.useEncodingPreset, value); }
		}

		private string encodingPreset;
		[JsonProperty]
		public string EncodingPreset
		{
			get { return this.encodingPreset; }
			set { this.RaiseAndSetIfChanged(ref this.encodingPreset, value); }
		}

		[JsonProperty]
		public bool AutoQueueOnScan { get; set; }

		[JsonProperty]
		public bool AutoEncodeOnScan { get; set; }

		[JsonProperty]
		public bool PostEncodeActionEnabled { get; set; }

		[JsonProperty]
		public string PostEncodeExecutable { get; set; }

		[JsonProperty]
		public string PostEncodeArguments { get; set; } = "\"{file}\"";

		private ObservableAsPropertyHelper<string> displayName;
		public string DisplayName => this.displayName.Value;

		#region Obsolete

		// Made obsolete before v6.13
		[Obsolete("Use PickerTimeRangeMode instead.")]
		[DeserializeOnly]
		[JsonProperty]
		public bool TimeRangeSelectEnabled { get; set; }

		// Made obsolete on v6.13
		[Obsolete("Output directory is no longer an override.")]
		[DeserializeOnly]
		[JsonProperty]
		public bool OutputDirectoryOverrideEnabled { get; set; }

		// Made obsolete on v6.13
		[Obsolete("Use OutputDirectory instead.")]
		[DeserializeOnly]
		[JsonProperty]
		public string OutputDirectoryOverride { get; set; }

		// Made obsolete on v6.13
		[Obsolete("Name format is no longer an override.")]
		[DeserializeOnly]
		[JsonProperty]
		public bool NameFormatOverrideEnabled { get; set; }

		// Made obsolete on v6.13
		[Obsolete("Use NameFormat instead.")]
		[DeserializeOnly]
		[JsonProperty]
		public string NameFormatOverride { get; set; }

		[Obsolete("Use OutputToSourceDirectory instead.")]
		[DeserializeOnly]
		[JsonProperty("OutputToSourceDirectory")]
		public bool? OutputToSourceDirectoryNullable { get; set; }

		[Obsolete("Use PreserveFolderStructureInBatch instead.")]
		[DeserializeOnly]
		[JsonProperty("PreserveFolderStructureInBatch")]
		public bool? PreserveFolderStructureInBatchNullable { get; set; }

		#endregion
	}
}
