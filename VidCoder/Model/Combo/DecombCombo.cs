using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VidCoder.Model
{
	using System.ComponentModel.DataAnnotations;
	using LocalResources;

	public enum DecombCombo
	{
		[Display(ResourceType = typeof(EnumsRes), Name= "Off")]
		Off = 0,

		[Display(ResourceType = typeof(EnumsRes), Name = "Default")]
		Default,

		[Display(ResourceType = typeof(EnumsRes), Name = "Decomb_Fast")]
		Fast,

		[Display(ResourceType = typeof(EnumsRes), Name = "Decomb_Bob")]
		Bob,

		[Display(ResourceType = typeof(EnumsRes), Name = "Custom")]
		Custom
	}
}
