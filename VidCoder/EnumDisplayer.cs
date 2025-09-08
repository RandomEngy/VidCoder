using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Windows.Data;
using VidCoder.Resources;

namespace VidCoder;

public class EnumDisplayer : IValueConverter
{
	private Type type;
	private IDictionary displayValues;
	private IDictionary reverseValues;
	private static Dictionary<string, string> overriddenDisplayEntries = new()
	{
		{ "Off", EnumsRes.Off },
		{ "Default", EnumsRes.Default },
		{ "Custom", EnumsRes.Custom },
	};
	private static Dictionary<string, string> resourceAliases = new()
	{
		{ "VCAnamorphic", "Anamorphic" },
		{ "VCDeinterlace", "Deinterlace" },
		{ "VCDecomb", "Decomb" },
	};

	private static ResourceManager enumResourceManager = new(typeof(EnumsRes));

	public EnumDisplayer()
	{
	}

	public EnumDisplayer(Type type)
	{
		this.Type = type;
	}

	public Type Type
	{
		get
		{
			return type;
		}
		set
		{
			if (!value.IsEnum)
			{
				throw new ArgumentException("parameter is not an Enumermated type", "value");
			}

			this.type = value;

			Type displayValuesType = typeof(Dictionary<,>)
				.GetGenericTypeDefinition().MakeGenericType(type, typeof(string));
			this.displayValues = (IDictionary)Activator.CreateInstance(displayValuesType);

			Type reverseValuesType = typeof(Dictionary<,>)
				.GetGenericTypeDefinition().MakeGenericType(typeof(string), type);
			this.reverseValues = (IDictionary)Activator.CreateInstance(reverseValuesType);

			var fields = type.GetFields(BindingFlags.Public | BindingFlags.Static);
			foreach (var field in fields)
			{
				DisplayAttribute[] a = (DisplayAttribute[])
											field.GetCustomAttributes(typeof(DisplayAttribute), false);

				string displayString = GetAttributeDisplayString(a);
				object enumValue = field.GetValue(null);
				string enumValueString = null;

				if (displayString == null)
				{
					enumValueString = Enum.GetName(this.type, enumValue);
					displayString = GetResourceDisplayString(this.type.Name, enumValueString);
				}

				if (displayString == null)
				{
					displayString = GetBackupDisplayString(enumValueString);
				}

				if (displayString != null)
				{
					displayValues.Add(enumValue, displayString);
					reverseValues.Add(displayString, enumValue);
				}
			}
		}
	}

	public ReadOnlyCollection<string> DisplayNames
	{
		get
		{
			return new List<string>((IEnumerable<string>)displayValues.Values).AsReadOnly();
		}
	}

	private string GetAttributeDisplayString(DisplayAttribute[] attributes)
	{
		if (attributes == null || attributes.Length == 0) return null;
		DisplayAttribute displayAttribute = attributes[0];
		if (displayAttribute.ResourceType != null)
		{
			ResourceManager resourceManager = new(displayAttribute.ResourceType);
			return resourceManager.GetString(displayAttribute.Name);
		}

		return displayAttribute.Name;
	}

	private string GetResourceDisplayString(string enumType, string enumValue)
	{
		if (resourceAliases.ContainsKey(enumType))
		{
			enumType = resourceAliases[enumType];
		}

		return enumResourceManager.GetString(enumType + "_" + enumValue);
	}

	private string GetBackupDisplayString(string enumValueString)
	{
		if (overriddenDisplayEntries != null && overriddenDisplayEntries.ContainsKey(enumValueString))
		{
			return overriddenDisplayEntries[enumValueString];
		}

		return enumValueString;
	}

	object IValueConverter.Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return displayValues[value];
	}

	object IValueConverter.ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return reverseValues[value];
	}
}
