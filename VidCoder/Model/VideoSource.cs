using System.Collections.Generic;
using System.IO;
using System.Windows;
using HandBrake.ApplicationServices.Interop.Json.Scan;
using VidCoder.Services;

namespace VidCoder.Model
{
	/// <summary>
	/// An object representing a video source (DVD or input file).
	/// </summary>
	public class VideoSource
	{
		/// <summary>
		/// Default constructor for this object
		/// </summary>
		public VideoSource()
		{
			this.Titles = new List<SourceTitle>();
		}

		/// <summary>
		/// Collection of Titles associated with this DVD
		/// </summary>
		public List<SourceTitle> Titles { get; set; }

		/// <summary>
		/// The feature title.
		/// </summary>
		public int FeatureTitle { get; set; }
	}
}