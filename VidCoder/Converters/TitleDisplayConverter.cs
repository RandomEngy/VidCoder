using System;
using System.Globalization;
using System.Windows.Data;
using HandBrake.Interop.Interop.Json.Scan;
using VidCoder.Extensions;

namespace VidCoder.Converters
{
	public class TitleDisplayConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is string)
			{
				return string.Empty;
			}

			var title = (SourceTitle) value;
			return title.GetDisplayString();
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
