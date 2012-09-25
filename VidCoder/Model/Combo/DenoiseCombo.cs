using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VidCoder.Model
{
	using System.ComponentModel.DataAnnotations;
	using LocalResources;

	public enum DenoiseCombo
	{
		[Display(ResourceType = typeof(EnumsRes), Name = "Off")]
		Off = 0,

		[Display(ResourceType = typeof(EnumsRes), Name = "Denoise_Weak")]
		Weak,

		[Display(ResourceType = typeof(EnumsRes), Name = "Denoise_Medium")]
		Medium,

		[Display(ResourceType = typeof(EnumsRes), Name = "Denoise_Strong")]
		Strong,

		[Display(ResourceType = typeof(EnumsRes), Name = "Custom")]
		Custom
	}
}
