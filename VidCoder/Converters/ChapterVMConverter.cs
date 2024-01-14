using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Data;
using VidCoder.ViewModel;

namespace VidCoder.Converters;

// Helper class to convert IsHighlighted for ChapterViewModel
public class ChapterVMConverter : IMultiValueConverter
{
	public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
	{
		if (values != null && values[0] is bool && values[1] is ChapterViewModel)
		{
			((ChapterViewModel)values[1]).IsHighlighted = (bool)values[0];
			return (bool)values[0];
		}

		return DependencyProperty.UnsetValue;
	}

	public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
	{
		throw new NotSupportedException();
	}
}
