using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using VidCoder.Resources;

namespace VidCoder.Model.Encoding
{
    public enum VCDetelecine
    {
        [Display(ResourceType = typeof(EnumsRes), Name = "Off")]
        Off = 0,

        [Display(ResourceType = typeof(EnumsRes), Name = "Default")]
        Default,

        [Display(ResourceType = typeof(EnumsRes), Name = "Custom")]
        Custom
    }
}
