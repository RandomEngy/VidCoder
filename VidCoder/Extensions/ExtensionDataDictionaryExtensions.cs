using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace VidCoder.Extensions;

public static class ExtensionDataDictionaryExtensions
{
	public static bool GetBool(this Dictionary<string, object> dictionary, string key)
	{
		if (dictionary == null)
		{
			return false;
		}

		if (dictionary.TryGetValue(key, out object value))
		{
			if (value is JsonElement)
			{
				var element = (JsonElement)value;
				return element.GetBoolean();
			}
			else
			{
				return false;
			}
		}
		else
		{
			return false;
		}
	}

	public static bool? GetNullableBool(this Dictionary<string, object> dictionary, string key)
	{
		if (dictionary == null)
		{
			return null;
		}

		if (dictionary.TryGetValue(key, out object value))
		{
			if (value is JsonElement)
			{
				var element = (JsonElement)value;
				return element.GetBoolean();
			}
			else
			{
				return null;
			}
		}
		else
		{
			return null;
		}
	}

	public static string GetString(this Dictionary<string, object> dictionary, string key)
	{
		if (dictionary == null)
		{
			return null;
		}

		if (dictionary.TryGetValue(key, out object value))
		{
			if (value is JsonElement)
			{
				var element = (JsonElement)value;
				return element.GetString();
			}
			else
			{
				return null;
			}
		}
		else
		{
			return null;
		}
	}
}
