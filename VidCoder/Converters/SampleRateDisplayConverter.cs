using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Data;
using System.Globalization;
using HandBrake.Interop.Interop;

namespace VidCoder.Converters
{
	using Resources;

	public class SampleRateDisplayConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value == null)
			{
				return string.Empty;
			}

			if (!(value is int))
			{
				return string.Empty;
			}

			int sampleRateHz = (int)value;
			if (sampleRateHz == 0)
			{
				return EncodingRes.SameAsSource;
			}

			double sampleRateKHz = sampleRateHz / 1000.0;
			return sampleRateKHz + " kHz";
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
