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
					if (this.IsNone)
					{
						return CommonRes.None;
					}

					return name;
				})
				.ToProperty(this, x => x.DisplayName, out this.displayName);
		}

        /// <summary>
        /// Gets or sets a value indicating whether this preset is the special "None" preset
        /// </summary>
		[JsonProperty]
		public bool IsNone { get; set; }

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

		[JsonProperty]
		public AudioSelectionMode AudioSelectionMode { get; set; } = AudioSelectionMode.Disabled;

        // Default "und"
		[JsonProperty]
		public string AudioLanguageCode { get; set; } = "und";

        // Applies only with AutoAudioType.Language
		[JsonProperty]
		public bool AudioLanguageAll { get; set; }

		[JsonProperty]
		public SubtitleSelectionMode SubtitleSelectionMode { get; set; } = SubtitleSelectionMode.Disabled;

		// Applies only with AutoSubtitleType.ForeignAudioSearch
		[JsonProperty]
		public bool SubtitleForeignBurnIn { get; set; }

        // Applies only with AutoSubtitleType.Language
        // Default "und"
		[JsonProperty]
		public string SubtitleLanguageCode { get; set; } = "und";

        // Applies only with AutoSubtitleType.Language
		[JsonProperty]
		public bool SubtitleLanguageAll { get; set; }

        // Applies only with AutoSubtitleType.Language
        // Default true
		[JsonProperty]
		public bool SubtitleLanguageOnlyIfDifferent { get; set; } = true;

        // Applies only with AutoSubtitleType.Language
		[JsonProperty]
		public bool SubtitleLanguageDefault { get; set; }

        // Applies only with AutoSubtitleType.Language
		[JsonProperty]
		public bool SubtitleLanguageBurnIn { get; set; }

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
    }
}
