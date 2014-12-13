using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VidCoder.Model.Encoding
{
    public enum VCScaleMethod
    {
        /// <summary>
        /// Standard software scaling. Highest quality.
        /// </summary>
        Lanczos,

        /// <summary>
        /// OpenCL-assisted bicubic scaling.
        /// </summary>
        Bicubic
    }
}
