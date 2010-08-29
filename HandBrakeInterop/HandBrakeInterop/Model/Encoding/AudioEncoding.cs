using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HandBrake.Interop
{
	public class AudioEncoding
	{
		public int InputNumber { get; set; }
		public AudioEncoder Encoder { get; set; }
		public int Bitrate { get; set; }
		public Mixdown Mixdown { get; set; }

		/// <summary>
		/// Obsolete. Use SampleRateRaw instead.
		/// </summary>
		[Obsolete("This property is ignored and only exists for backwards compatibility. Use SampleRateRaw instead.")]
		public string SampleRate { get; set; }

		/// <summary>
		/// Gets or sets the sample rate in Hz.
		/// </summary>
		public int SampleRateRaw { get; set; }
		public double Drc { get; set; }
	}
}
