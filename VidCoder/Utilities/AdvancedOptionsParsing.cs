using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VidCoder
{
	public static class AdvancedOptionsParsing
	{
		public static Dictionary<string, string> ParseOptions(string optionsString)
		{
			var options = new Dictionary<string, string>();

			if (optionsString != null)
			{
				string[] optionsParts = optionsString.Split(new[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
				foreach (string optionsPart in optionsParts)
				{
					int equalsIndex = optionsPart.IndexOf('=');
					if (equalsIndex > 0)
					{
						string key = optionsPart.Substring(0, equalsIndex);
						if (!options.ContainsKey(key))
						{
							options.Add(key, optionsPart.Substring(equalsIndex + 1, optionsPart.Length - equalsIndex - 1));
						}
					}
				}
			}

			return options;
		}
	}
}
