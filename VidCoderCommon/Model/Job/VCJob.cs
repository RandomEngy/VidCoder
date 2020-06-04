using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using VidCoderCommon.JsonConverters;

namespace VidCoderCommon.Model
{
	/// <summary>
	/// An analogue for HBInterop's EncodingJob.
	/// </summary>
	public class VCJob
	{
		public SourceType SourceType { get; set; }
		public string SourcePath { get; set; }

		/// <summary>
		/// Gets or sets the 1-based index of the title to encode.
		/// </summary>
		public int Title { get; set; }

		/// <summary>
		/// Gets or sets the angle to encode. 0 for default, 1+ for specified angle.
		/// </summary>
		public int Angle { get; set; }

		public VideoRangeType RangeType { get; set; }
		public int ChapterStart { get; set; }
		public int ChapterEnd { get; set; }

		public double SecondsStart { get; set; }
		public double SecondsEnd { get; set; }

		public int FramesStart { get; set; }
		public int FramesEnd { get; set; }

		[Obsolete("Use AudioTracks instead")]
		[DeserializeOnly]
		public List<int> ChosenAudioTracks { get; set; }

		/// <summary>
		/// Gets or sets the list of chosen audio tracks, with optional names.
		/// </summary>
		public List<ChosenAudioTrack> AudioTracks { get; set; }

		public VCSubtitles Subtitles { get; set; }
		public bool UseDefaultChapterNames { get; set; }
		public List<string> CustomChapterNames { get; set; }

		[Obsolete("Use FinalOutputPath instead")]
		[DeserializeOnly]
		public string OutputPath { get; set; }

		/// <summary>
		/// Gets or sets the final output path for the result.
		/// </summary>
		public string FinalOutputPath { get; set; }

		/// <summary>
		/// Gets or sets the ".part" output path variant to use. Null if we are directly using the final output path to encode.
		/// </summary>
		public string PartOutputPath { get; set; }

		/// <summary>
		/// Gets the output path used for the in-progress encode.
		/// </summary>
		public string InProgressOutputPath => this.PartOutputPath ?? this.FinalOutputPath;

		public VCProfile EncodingProfile { get; set; }

		// The length of video to encode.
		public TimeSpan Length { get; set; }

		[OnDeserialized]
		internal void OnDeserializedMethod(StreamingContext context)
		{
#pragma warning disable 618
			if (this.OutputPath != null && this.FinalOutputPath == null)
			{
				this.FinalOutputPath = this.OutputPath;
			}

			if (this.ChosenAudioTracks != null && this.AudioTracks == null)
			{
				this.AudioTracks = this.ChosenAudioTracks.Select(t => new ChosenAudioTrack { TrackNumber = t }).ToList();
			}
#pragma warning restore 618
		}
	}
}
