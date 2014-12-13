using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using VidCoder.Resources;

namespace VidCoder.Model.Encoding
{
    public enum VCAnamorphic
    {
        [Display(ResourceType = typeof(EnumsRes), Name = "Anamorphic_None")]
		None = 0,
		[Display(ResourceType = typeof(EnumsRes), Name = "Anamorphic_Strict")]
		Strict,
		[Display(ResourceType = typeof(EnumsRes), Name = "Anamorphic_Loose")]
		Loose,
		[Display(ResourceType = typeof(EnumsRes), Name = "Anamorphic_Custom")]
		Custom
    }
}
