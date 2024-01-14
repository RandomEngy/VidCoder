using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VidCoder.Model;

public static class CharCode
{
	private static List<string> charCodes;

	public static IList<string> Codes
	{
		get
		{
			if (charCodes == null)
			{
				charCodes = new List<string>
				{
					"ANSI_X3.4-1968",
					"ANSI_X3.4-1986",
					"ANSI_X3.4",
					"ANSI_X3.110-1983",
					"ANSI_X3.110",
					"ASCII",
					"ECMA-114",
					"ECMA-118",
					"ECMA-128",
					"ECMA-CYRILLIC",
					"IEC_P27-1",
					"ISO-8859-1",
					"ISO-8859-2",
					"ISO-8859-3",
					"ISO-8859-4",
					"ISO-8859-5",
					"ISO-8859-6",
					"ISO-8859-7",
					"ISO-8859-8",
					"ISO-8859-9",
					"ISO-8859-9E",
					"ISO-8859-10",
					"ISO-8859-11",
					"ISO-8859-13",
					"ISO-8859-14",
					"ISO-8859-15",
					"ISO-8859-16",
					"UTF-7",
					"UTF-8",
					"UTF-16",
					"UTF-32"
				};
			}

			return charCodes;
		}
	}

	public static string FromUtfUnknownCode(string charset)
	{
		switch (charset)
		{
			case "ascii":
			case "utf-8":
			case "iso-8859-2":
			case "iso-8859-5":
			case "iso-8859-7":
			case "iso-8859-8":
				return charset.ToUpperInvariant();
			case "utf-16le":
			case "utf-16be":
				return "UTF-16";
			case "utf-32le":
			case "utf-32be":
				return "UTF-32";
			default:
				return null;
		}
	}
}
