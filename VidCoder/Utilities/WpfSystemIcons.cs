using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Media;
using System.Drawing;
using System.Windows.Interop;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Runtime.InteropServices;

namespace VidCoder
{
	public static class WpfSystemIcons
	{
		private static ImageSource error;

		public static ImageSource Error
		{
			get
			{
				if (error == null)
				{
					error = Imaging.CreateBitmapSourceFromHIcon(
						SystemIcons.Error.Handle,
						Int32Rect.Empty,
						BitmapSizeOptions.FromEmptyOptions());
				}

				return error;
			}
		}
	}
}
