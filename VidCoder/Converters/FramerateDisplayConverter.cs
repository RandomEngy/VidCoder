using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Data;
using System.Globalization;

namespace VidCoder.Converters
{
	public class FramerateDisplayConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value == null || value as string == string.Empty)
			{
				return string.Empty;
			}

			double framerate = (double)value;

			if (framerate == 0)
			{
				return "Same as source";
			}

			return framerate.ToString();
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
