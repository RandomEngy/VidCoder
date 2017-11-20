using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace VidCoder
{
	public class BitmapUtilities
	{
		/// <summary>
		/// Convert a Bitmap to a BitmapImagetype.
		/// </summary>
		/// <param name="bitmap">
		/// The bitmap.
		/// </param>
		/// <returns>
		/// The <see cref="BitmapImage"/>.
		/// </returns>
		public static BitmapImage ConvertToBitmapImage(Bitmap bitmap)
		{
			// Create a Bitmap Image for display.
			using (var memoryStream = new MemoryStream())
			{
				try
				{
					bitmap.Save(memoryStream, ImageFormat.Bmp);
				}
				finally
				{
					bitmap.Dispose();
				}

				var wpfBitmap = new BitmapImage();
				wpfBitmap.BeginInit();
				wpfBitmap.CacheOption = BitmapCacheOption.OnLoad;
				wpfBitmap.StreamSource = memoryStream;
				wpfBitmap.EndInit();
				wpfBitmap.Freeze();

				return wpfBitmap;
			}
		}
	}
}
