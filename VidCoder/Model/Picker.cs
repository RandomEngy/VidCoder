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

		[JsonProperty]
		public bool TitleRangeSelectEnabled { get; set; }

		[JsonProperty]
		public int TitleRangeSelectStartMinutes { get; set; }

		[JsonProperty]
		public int TitleRangeSelectEndMinutes { get; set; }

		[JsonProperty]
		public AudioSelectionMode AudioSelectionMode { get; set; }

        // Default "und"
		[JsonProperty]
		public string AudioLanguageCode { get; set; }

        // Applies only with AutoAudioType.Language
		[JsonProperty]
		public bool AudioLanguageAll { get; set; }

		[JsonProperty]
		public SubtitleSelectionMode SubtitleSelectionMode { get; set; }

        // Applies only with AutoSubtitleType.ForeignAudioSearch
		[JsonProperty]
		public bool SubtitleForeignBurnIn { get; set; }

        // Applies only with AutoSubtitleType.Language
        // Default "und"
		[JsonProperty]
		public string SubtitleLanguageCode { get; set; }

        // Applies only with AutoSubtitleType.Language
		[JsonProperty]
		public bool SubtitleLanguageAll { get; set; }

        // Applies only with AutoSubtitleType.Language
        // Default true
		[JsonProperty]
		public bool SubtitleLanguageOnlyIfDifferent { get; set; }

        // Applies only with AutoSubtitleType.Language
		[JsonProperty]
		public bool SubtitleLanguageDefault { get; set; }

        // Applies only with AutoSubtitleType.Language
		[JsonProperty]
		public bool SubtitleLanguageBurnIn { get; set; }

		[JsonProperty]
		public bool UseEncodingPreset { get; set; }

		[JsonProperty]
		public string EncodingPreset { get; set; }

		[JsonProperty]
		public bool AutoQueueOnScan { get; set; }

		[JsonProperty]
		public bool AutoEncodeOnScan { get; set; }

		private ObservableAsPropertyHelper<string> displayName;
		public string DisplayName
		{
			get { return this.displayName.Value; }
		}

		//public static string GetDisplayName(string pickerName, bool isNone)
		//{
		//	if (isNone)
		//	{
		//		return CommonRes.None;
		//	}

		//	return pickerName;
		//}
    }
}
