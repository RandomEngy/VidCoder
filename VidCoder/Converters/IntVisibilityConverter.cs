namespace VidCoder.Converters
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Linq;
	using System.Text;
	using System.Windows;
	using System.Windows.Data;

	public class IntVisibilityConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value == null || parameter == null)
			{
				return Visibility.Collapsed;
			}

			int intValue = (int) value;
			string stringParam = (string)parameter;
			int intParam = int.Parse(stringParam, CultureInfo.InvariantCulture);

			return intValue == intParam ? Visibility.Visible : Visibility.Collapsed;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
