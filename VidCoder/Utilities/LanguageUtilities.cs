using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HandBrake.Interop.Interop;
using HandBrake.Interop.Interop.Model;

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

			IList<Language> allLanguages = HandBrakeLanguagesHelper.AllLanguages;

			if (allLanguages.Any(l => l.Code == currentLanguageCode) && (alreadyExistingCodes == null || !alreadyExistingCodes.Contains(currentLanguageCode)))
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

			foreach (Language language in allLanguages)
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
