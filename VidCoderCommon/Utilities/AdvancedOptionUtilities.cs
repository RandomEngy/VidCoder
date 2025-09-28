using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VidCoderCommon.Utilities;

public static class AdvancedOptionUtilities
{
	public static string Prepend(string option, string existingOptions)
	{
		if (string.IsNullOrEmpty(existingOptions))
		{
			return option;
		}
		else
		{
			return option + ":" + existingOptions;
		}
	}
}
