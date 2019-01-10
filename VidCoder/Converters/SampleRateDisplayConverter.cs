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
		private static Dictionary<int, string> rateDisplayTable;

		static SampleRateDisplayConverter()
		{
			rateDisplayTable = new Dictionary<int, string> { { 0, EncodingRes.SameAsSource } };
			foreach (var audioSampleRate in HandBrakeEncoderHelpers.AudioSampleRates)
			{
				rateDisplayTable.Add(audioSampleRate.Rate, audioSampleRate.Name + " kHz");
			}
		}

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

			int sampleRate = (int)value;

			return rateDisplayTable[sampleRate];
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
