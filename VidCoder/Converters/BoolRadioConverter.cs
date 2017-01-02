using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace VidCoder.Converters
{
	/// <summary>
	/// Works around a bug in radio button binding that causes it to fire "unset" when loaded.
	/// Allows two radio buttons to be bound to a single bool.
	/// </summary>
	public class BoolRadioConverter : IValueConverter
	{
		public bool Inverse { get; set; }

		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			bool boolValue = (bool) value;

			return this.Inverse ? !boolValue : boolValue;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			bool boolValue = (bool)value;

			if (!boolValue)
			{
				// We only care when the user clicks a radio button to select it.
				return null;
			}

			return !this.Inverse;
		}
	}
}
