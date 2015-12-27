using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Windows.Data;

namespace VidCoder.Converters
{
	public class EnumBoolConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (targetType.IsAssignableFrom(typeof(bool)) && targetType.IsAssignableFrom(typeof(string)))
			{
				throw new ArgumentException("EnumBoolConverter can only convert to boolean or string.");
			}

			if (targetType == typeof(string))
			{
				return value.ToString();
			}

			return string.Compare(value.ToString(), parameter.ToString(), StringComparison.InvariantCultureIgnoreCase) == 0;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (targetType.IsAssignableFrom(typeof(bool)) && targetType.IsAssignableFrom(typeof(string)))
			{
				throw new ArgumentException("EnumBoolConverter can only convert back value from a string or a boolean.");
			}

			if (!targetType.IsEnum)
			{
				throw new ArgumentException("EnumBoolConverter can only convert value to an Enum Type.");
			}

			if (!(bool)value)
			{
				return null;
			}

			return parameter;
		}
	}
}
