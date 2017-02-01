using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using Newtonsoft.Json;
using VidCoderCommon.JsonConverters;

namespace VidCoderCommon.Model
{
	/// <summary>
	/// An analogue for HBInterop's EncodingJob.
	/// </summary>
	public class VCJob
	{
		[JsonIgnore]
		[XmlElement("SourceType")]
		public string XmlSourceType
		{
			get { return this.SourceType.ToString(); }
			set { this.SourceType = SourceTypeConverter.ParseSourceTypeString(value); }
		}

		[XmlIgnore]
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

		/// <summary>
		/// Gets or sets the list of chosen audio tracks (1-based)
		/// </summary>
		public List<int> ChosenAudioTracks { get; set; }
		public VCSubtitles Subtitles { get; set; }
		public bool UseDefaultChapterNames { get; set; }
		public List<string> CustomChapterNames { get; set; }

		public string OutputPath { get; set; }

		public VCProfile EncodingProfile { get; set; }

		// The length of video to encode.
		[XmlIgnore]
		public TimeSpan Length { get; set; }

		[JsonIgnore]
		[XmlElement("Length")]
		public string XmlLength
		{
			get { return this.Length.ToString(); }
			set { this.Length = TimeSpan.Parse(value); }
		}
	}
}
