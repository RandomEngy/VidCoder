using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HandBrake.ApplicationServices.Interop;
using HandBrake.ApplicationServices.Interop.Model;

namespace VidCoder
{
	public static class LanguageUtilities
	{
		private static List<string> popularLanguages = new List<string>
		{
			"eng",
			"zho",
			"spa",
			"hin",
			"ara",
			"por",
			"ben",
			"rus",
			"jpn",
			"pan",
			"deu"
		}; 

		public static string GetDefaultLanguageCode(IList<string> alreadyExistingCodes = null)
		{
			string currentLanguageCode = CultureInfo.CurrentUICulture.ThreeLetterISOLanguageName;

			if (Languages.AllLanguages.Any(l => l.Code == currentLanguageCode) && (alreadyExistingCodes == null || !alreadyExistingCodes.Contains(currentLanguageCode)))
			{
				return currentLanguageCode;
			}

			if (alreadyExistingCodes == null)
			{
				return popularLanguages[0];
			}

			foreach (string popularLanguage in popularLanguages)
			{
				if (!alreadyExistingCodes.Contains(popularLanguage))
				{
					return popularLanguage;
				}
			}

			foreach (Language language in Languages.AllLanguages)
			{
				if (!alreadyExistingCodes.Contains(language.Code) && language.Code != "und")
				{
					return language.Code;
				}
			}

			return "und";
		}
	}
}
