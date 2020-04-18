using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using HandBrake.Interop.Interop.Model.Preview;

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

		public static Bitmap ConvertByteArrayToBitmap(RawPreviewData previewData)
		{
			var bitmap = new Bitmap(previewData.Width, previewData.Height);

			BitmapData bitmapData = bitmap.LockBits(new Rectangle(0, 0, previewData.Width, previewData.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppRgb);

			IntPtr ptr = bitmapData.Scan0; // Pointer to the first pixel.
			for (int i = 0; i < previewData.Height; i++)
			{
				try
				{
					Marshal.Copy(previewData.RawBitmapData, i * previewData.StrideWidth, ptr, previewData.StrideWidth);
					ptr = IntPtr.Add(ptr, previewData.Width * 4);
				}
				catch (Exception exc)
				{
					Debug.WriteLine(exc); // In theory, this will allow a partial image display if this happens. TODO add better logging of this.
				}
			}

			bitmap.UnlockBits(bitmapData);

			return bitmap;
		}
	}
}
