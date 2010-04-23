using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using HandBrake.Interop;
using System.Collections;
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
                DisplayStringAttribute[] a = (DisplayStringAttribute[])field.GetCustomAttributes(typeof(DisplayStringAttribute), false);

                string displayString = GetDisplayStringValue(a);
                T enumValue = (T)field.GetValue(null);

                displayValues.Add(enumValue, displayString);
            }
        }

        public string Convert(T enumValue)
        {
            return displayValues[enumValue];
        }

        private string GetDisplayStringValue(DisplayStringAttribute[] a)
        {
            if (a == null || a.Length == 0) return null;
            DisplayStringAttribute dsa = a[0];
            if (!string.IsNullOrEmpty(dsa.ResourceKey))
            {
                ResourceManager rm = new ResourceManager(typeof(T));
                return rm.GetString(dsa.ResourceKey);
            }
            return dsa.Value;
        }
    }
}
