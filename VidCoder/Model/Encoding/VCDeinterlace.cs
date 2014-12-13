using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using VidCoder.Resources;

namespace VidCoder.Model.Encoding
{
    public enum VCDeinterlace
    {
        [Display(ResourceType = typeof(EnumsRes), Name = "Off")]
		Off = 0,

		[Display(ResourceType = typeof(EnumsRes), Name = "Deinterlace_Fast")]
		Fast,

		[Display(ResourceType = typeof(EnumsRes), Name = "Deinterlace_Slow")]
		Slow,

		[Display(ResourceType = typeof(EnumsRes), Name = "Deinterlace_Slower")]
		Slower,

		[Display(ResourceType = typeof(EnumsRes), Name = "Deinterlace_Bob")]
		Bob,

		[Display(ResourceType = typeof(EnumsRes), Name = "Custom")]
		Custom
    }
}
