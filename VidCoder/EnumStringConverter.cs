using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Resources;

namespace VidCoder
{
	public class EnumStringConverter<T>
	{
		private Dictionary<T, string> displayValues;

		public EnumStringConverter()
		{
			this.displayValues = new Dictionary<T, string>();

			Type type = typeof(T);
			var fields = type.GetFields(BindingFlags.Public | BindingFlags.Static);
			foreach (var field in fields)
			{
				DisplayAttribute[] a = (DisplayAttribute[])field.GetCustomAttributes(typeof(DisplayAttribute), false);

				string displayString = GetDisplayStringValue(a);
				T enumValue = (T)field.GetValue(null);

				displayValues.Add(enumValue, displayString);
			}
		}

		public string Convert(T enumValue)
		{
			return displayValues[enumValue];
		}

		private string GetDisplayStringValue(DisplayAttribute[] a)
		{
			if (a == null || a.Length == 0) return null;
			DisplayAttribute displayAttribute = a[0];

			if (displayAttribute.ResourceType != null)
			{
				ResourceManager resourceManager = new ResourceManager(displayAttribute.ResourceType);
				return resourceManager.GetString(displayAttribute.Name);
			}

			return displayAttribute.Name;
		}
	}
}
