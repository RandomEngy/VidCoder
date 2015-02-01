using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VidCoder.Resources;

namespace VidCoder.Model
{
    public class Picker
    {
        /// <summary>
        /// Gets or sets a value indicating whether this preset is the special "None" preset
        /// </summary>
        public bool IsNone { get; set; }

        public bool IsModified { get; set; }

        public string Name { get; set; }

		public bool OutputDirectoryOverrideEnabled { get; set; }

		public string OutputDirectoryOverride { get; set; }

		public bool NameFormatOverrideEnabled { get; set; }

		public string NameFormatOverride { get; set; }

		public bool? OutputToSourceDirectory { get; set; }

		public bool? PreserveFolderStructureInBatch { get; set; }

		public bool TitleRangeSelectEnabled { get; set; }

		public int TitleRangeSelectStartMinutes { get; set; }

		public int TitleRangeSelectEndMinutes { get; set; }

        public AudioSelectionMode AudioSelectionMode { get; set; }

        // Default "und"
        public string AudioLanguageCode { get; set; }

        // Applies only with AutoAudioType.Language
        public bool AudioLanguageAll { get; set; }

        public SubtitleSelectionMode SubtitleSelectionMode { get; set; }

        // Applies only with AutoSubtitleType.ForeignAudioSearch
        public bool SubtitleForeignBurnIn { get; set; }

        // Applies only with AutoSubtitleType.Language
        // Default "und"
        public string SubtitleLanguageCode { get; set; }

        // Applies only with AutoSubtitleType.Language
        public bool SubtitleLanguageAll { get; set; }

        // Applies only with AutoSubtitleType.Language
        // Default true
        public bool SubtitleLanguageOnlyIfDifferent { get; set; }

        // Applies only with AutoSubtitleType.Language
        public bool SubtitleLanguageDefault { get; set; }

        // Applies only with AutoSubtitleType.Language
        public bool SubtitleLanguageBurnIn { get; set; }

		public bool UseEncodingPreset { get; set; }

		public string EncodingPreset { get; set; }

		public bool AutoQueueOnScan { get; set; }

		public bool AutoEncodeOnScan { get; set; }

        public string DisplayName
        {
            get
            {
                if (!this.IsNone)
                {
                    return this.Name;
                }

                return CommonRes.None;
            }
        }
    }
}
