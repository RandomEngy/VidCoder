using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace VidCoder
{
	public static class ColorUtilities
	{
		public static Color ToWindowsColor(string color)
		{
			if (string.IsNullOrEmpty(color))
			{
				return Colors.Black;
			}

			if (!color.StartsWith("#", StringComparison.Ordinal))
			{
				color = "#" + color;
			}

			try
			{
				return (Color)ColorConverter.ConvertFromString(color);
			}
			catch (Exception)
			{
				return Colors.Black;
			}
		}

		public static string ToHexString(Color color)
		{
			return string.Format(
				CultureInfo.InvariantCulture,
				"{0:X2}{1:X2}{2:X2}", color.R, color.G, color.B);
		}
	}
}
