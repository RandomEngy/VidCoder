using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VidCoder.Model.Encoding
{
    public enum VCDenoise
    {
        Off,
        hqdn3d,
        NLMeans,

        [Obsolete("HandBrake had it named like this at one point and some presets got generated with it.")]
        NlMeans,
    }
}
