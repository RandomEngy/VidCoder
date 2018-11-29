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
		public bool OutputDirectoryOverrideEnabled { get; set; }

		[JsonProperty]
		public string OutputDirectoryOverride { get; set; }

		[JsonProperty]
		public bool NameFormatOverrideEnabled { get; set; }

		[JsonProperty]
		public string NameFormatOverride { get; set; }

		[JsonProperty]
		public TitleCapitalizationChoice TitleCapitalization { get; set; } = TitleCapitalizationChoice.EveryWord;

		[JsonProperty]
		public bool? OutputToSourceDirectory { get; set; }

		[JsonProperty]
		public bool? PreserveFolderStructureInBatch { get; set; }

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

	    private bool timeRangeSelectEnabled;
	    [JsonProperty]
	    public bool TimeRangeSelectEnabled
	    {
		    get { return this.timeRangeSelectEnabled; }
		    set { this.RaiseAndSetIfChanged(ref this.timeRangeSelectEnabled, value); }
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

		// Obsolete. use AudioLanguageCodes instead.
		[JsonProperty]
		public string AudioLanguageCode { get; set; } = "und";

		// Obsolete. Use SubtitleLanguageCodes instead.
		[JsonProperty]
		public string SubtitleLanguageCode { get; set; } = "und";

		// Obsolete. Use SubtitleDefault instead.
		[JsonProperty]
		public bool SubtitleLanguageDefault { get; set; }

		// Obsolete. Use SubtitleBurnIn instead.
		[JsonProperty]
		public bool SubtitleForeignBurnIn { get; set; }

		// Obsolete. Use SubtitleBurnIn instead.
		[JsonProperty]
		public bool SubtitleLanguageBurnIn { get; set; }

		#endregion
	}
}
