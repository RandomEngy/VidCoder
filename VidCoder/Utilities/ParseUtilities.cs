using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VidCoder;

public static class ParseUtilities
{
	public static IList<int> ParseCommaSeparatedListToPositiveIntegers(string list)
	{
		var result = new List<int>();

		if (list != null)
		{
			string[] components = list.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
			foreach (string component in components)
			{
				string trimmedComponent = component.Trim();
				if (int.TryParse(trimmedComponent, out int parsedIndex))
				{
					if (parsedIndex > 0)
					{
						result.Add(parsedIndex);
					}
				}
			}
		}

		return result;
	}
}
