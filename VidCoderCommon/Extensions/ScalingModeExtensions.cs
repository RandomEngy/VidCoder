using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VidCoderCommon.Model;

namespace VidCoderCommon.Extensions
{
	public static class ScalingModeExtensions
	{
		public static bool UpscalingAllowed(this VCScalingMode scalingMode)
		{
			return scalingMode != VCScalingMode.DownscaleOnly;
		}

		public static int UpscalingMultipleCap(this VCScalingMode scalingMode)
		{
			if (scalingMode == VCScalingMode.DownscaleOnly)
			{
				return 1;
			}

			string enumString = scalingMode.ToString();
			if (!enumString.EndsWith("X"))
			{
				return 0;
			}

			return int.Parse(enumString.Substring(enumString.Length - 2, 1), CultureInfo.InvariantCulture);
		}
	}
}
