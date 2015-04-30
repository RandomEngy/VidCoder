namespace VidCoderCommon.Model
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
