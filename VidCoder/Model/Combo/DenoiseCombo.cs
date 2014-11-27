using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using VidCoder.Resources;

namespace VidCoder.Model
{
	public enum DenoiseCombo
	{
		[Display(ResourceType = typeof(CommonRes), Name = "Off")]
		Off,

		[Display(ResourceType = typeof(EnumsRes), Name = "Denoise_HQDN3D")]
		hqdn3d,

		[Display(ResourceType = typeof(EnumsRes), Name = "Denoise_NLMeans")]
		NlMeans
	}
}
