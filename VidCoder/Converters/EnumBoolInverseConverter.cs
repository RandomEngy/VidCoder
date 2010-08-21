using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Windows.Data;

namespace VidCoder.Converters
{
	public class EnumBoolInverseConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (targetType.IsAssignableFrom(typeof(bool)) && targetType.IsAssignableFrom(typeof(string)))
			{
				throw new ArgumentException("EnumBoolInverseConverter can only convert to boolean or string.");

			}

			if (targetType == typeof(string))
			{
				return value.ToString();
			}

			return string.Compare(value.ToString(), (string)parameter, StringComparison.InvariantCultureIgnoreCase) != 0;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new InvalidOperationException("Converting back is not supported.");
		}
	}
}
