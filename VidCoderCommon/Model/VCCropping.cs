using HandBrake.ApplicationServices.Interop.Model;
using Omu.ValueInjecter;

namespace VidCoderCommon.Model
{
    /// <summary>
    /// The Cropping Model
    /// </summary>
    public class VCCropping
	{
        /// <summary>
        /// Initializes a new instance of the <see cref="VCCropping"/> class. 
        /// </summary>
        public VCCropping()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="VCCropping"/> class.
        /// Copy Constructor
        /// </summary>
        public VCCropping(VCCropping cropping)
        {
            this.Top = cropping.Top;
            this.Bottom = cropping.Bottom;
            this.Left = cropping.Left;
            this.Right = cropping.Right;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="VCCropping"/> class. 
        /// </summary>
        /// <param name="top">
        /// The Top Value
        /// </param>
        /// <param name="bottom">
        /// The Bottom Value
        /// </param>
        /// <param name="left">
        /// The Left Value
        /// </param>
        /// <param name="right">
        /// The Right Value
        /// </param>
		public VCCropping(int top, int bottom, int left, int right)
        {
            this.Top = top;
            this.Bottom = bottom;
            this.Left = left;
            this.Right = right;
        }

        public int Top { get; set; }

        public int Bottom { get; set; }

        public int Left { get; set; }

        public int Right { get; set; }
	}
}
