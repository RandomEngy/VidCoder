using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;

namespace HandBrake.Interop
{
	public enum AudioEncoder
	{
		[Display(Name = "AAC (faac)")]
		Faac = 0,

		[Display(Name = "MP3 (lame)")]
		Lame,

		[Display(Name = "AC3 (ffmpeg)")]
		Ac3,

		[Display(Name = "AC3 Passthrough")]
		Ac3Passthrough,

		[Display(Name = "DTS Passthrough")]
		DtsPassthrough,

		[Display(Name = "Vorbis (vorbis)")]
		Vorbis
	}
}
