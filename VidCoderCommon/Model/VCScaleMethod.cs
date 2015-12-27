namespace VidCoderCommon.Model
{
    public enum VCScaleMethod
    {
        /// <summary>
        /// Standard software scaling. Highest quality.
        /// </summary>
        Lanczos = 0,

        /// <summary>
        /// OpenCL-assisted bicubic scaling.
        /// </summary>
        Bicubic = 1
    }
}
