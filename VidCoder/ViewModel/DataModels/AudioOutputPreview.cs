using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VidCoder.ViewModel
{
	public class AudioOutputPreview
	{
		/// <summary>
		/// Gets or sets the 1-based output track label.
		/// </summary>
		public string TrackNumber { get; set; }

		public string Name { get; set; }

		public string Encoder { get; set; }

		public string Bitrate { get; set; }

		public string Mixdown { get; set; }

		public string SampleRate { get; set; }

		public string Gain { get; set; }

		public string Drc { get; set; }
	}
}
