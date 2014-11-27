using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VidCoder.Model
{
	using HandBrake.Interop.Model;

	/// <summary>
	/// A pointer to a video source with any relevant metadata
	/// </summary>
	public class SourcePath
	{
		/// <summary>
		/// The absolute path to the video source.
		/// </summary>
		public string Path { get; set; }

		/// <summary>
		/// Gets or sets the folder this item was found in.
		/// </summary>
		public string ParentFolder { get; set; }

		/// <summary>
		/// Gets or sets the type of the source.
		/// </summary>
		public SourceType SourceType { get; set; }
	}
}
