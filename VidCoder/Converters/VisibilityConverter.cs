using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Data;
using System.Windows;

namespace VidCoder.Converters
{
	/// <summary>
	/// Converts a boolean to a WPF Visibility enum.
	/// </summary>
	public class VisibilityConverter : IValueConverter
	{
		/// <summary>
		/// Converts a boolean to a Visibility enum.
		/// </summary>
		/// <param name="value">The value to convert.</param>
		/// <param name="targetType">The type to convert to.</param>
		/// <param name="parameter">The parameter passed in for conversion.</param>
		/// <param name="culture">The parameter is not used.</param>
		/// <returns>The converted Visibility enum.</returns>
		/// <remarks>If a parameter is not supplied, the conversion goes false->Collapsed, true->Visible. When a parameter is supplied,
		/// the conversion is reversed and goes false->Visible, true->Collapsed.</remarks>
		public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			bool visibility = (bool)value;

			// If we are given a parameter, reverse it
			if (parameter != null)
			{
				visibility = !visibility;
			}

			return visibility ? Visibility.Visible : Visibility.Collapsed;
		}

		/// <summary>
		/// Converts a Visibility enum to a boolean.
		/// </summary>
		/// <param name="value">The value to convert.</param>
		/// <param name="targetType">The type to convert to.</param>
		/// <param name="parameter">The parameter passed in for conversion.</param>
		/// <param name="culture">The parameter is not used.</param>
		/// <returns>The converted boolean.</returns>
		/// <remarks>If a parameter is not supplied the conversion goes Collapsed->false, Visible->true. When a parameter is supplied,
		/// the conversion is reversed and goes Collapsed->true, Visible->false.</remarks>
		public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			Visibility visibility = (Visibility)value;
			bool result = visibility == Visibility.Visible;

			if (parameter != null)
			{
				result = !result;
			}

			return result;
		}
	}

}
