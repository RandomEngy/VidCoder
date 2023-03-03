using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace VidCoder.Services;

    public class PreviewImageLoadInfo
    {
	public BitmapSource PreviewImage { get; set; }

	/// <summary>
	/// Gets or sets the 0-based preview index that this applies to.
	/// </summary>
	public int PreviewIndex { get; set; }
    }
