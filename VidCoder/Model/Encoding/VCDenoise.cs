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

		// This is obsolete but we cannot mark it as such
        NlMeans,
    }
}
