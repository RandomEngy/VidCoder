using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FastMember;
using Omu.ValueInjecter;

namespace VidCoderCommon.Utilities.Injection
{
	public class FastDeepCloneInjection : SmartConventionInjection
	{
		protected override void SetValue(PropertyInfo prop, object component, object value)
		{
			var a = TypeAccessor.Create(component.GetType());
			a[component, prop.Name] = value;
		}

		protected override object GetValue(PropertyInfo prop, object component)
		{
			var a = TypeAccessor.Create(component.GetType(), true);
			return a[component, prop.Name];
		}

		protected override bool Match(SmartConventionInfo c)
		{
			return c.SourceProp.Name == c.TargetProp.Name;
		}

		protected override void ExecuteMatch(SmartMatchInfo mi)
		{
			var sourceVal = this.GetValue(mi.SourceProp, mi.Source);
			if (sourceVal == null) return;

			if (mi.SourceProp.DeclaringType != null && mi.SourceProp.DeclaringType.Name == "ReactiveObject")
			{
				// Skip any properties on ReactiveObject.
				return;
			}

			// For value types and string just return the value as is
			if (mi.SourceProp.PropertyType.IsValueType || mi.SourceProp.PropertyType == typeof(string))
			{
				if (mi.SourceProp.CanWrite)
				{
					this.SetValue(mi.TargetProp, mi.Target, sourceVal);
				}

				return;
			}

			// Handle arrays
			if (mi.SourceProp.PropertyType.IsArray)
			{
				var arr = sourceVal as Array;
				var arrayClone = arr.Clone() as Array;

				for (var index = 0; index < arr.Length; index++)
				{
					var arriVal = arr.GetValue(index);
					if (arriVal.GetType().IsValueType || arriVal.GetType() == typeof(string)) continue;
					arrayClone.SetValue(Activator.CreateInstance(arriVal.GetType()).InjectFrom<FastDeepCloneInjection>(arriVal), index);
				}
				this.SetValue(mi.TargetProp, mi.Target, arrayClone);
				return;
			}

			if (mi.SourceProp.PropertyType.IsGenericType)
			{
				// Handle IEnumerable<> also ICollection<> IList<> List<>
				if (mi.SourceProp.PropertyType.GetGenericTypeDefinition().GetInterfaces().Contains(typeof(IEnumerable)))
				{
					var genericArgument = mi.TargetProp.PropertyType.GetGenericArguments()[0];

					var tlist = typeof(List<>).MakeGenericType(genericArgument);

					var list = Activator.CreateInstance(tlist);

					if (genericArgument.IsValueType || genericArgument == typeof(string))
					{
						var addRange = tlist.GetMethod("AddRange");
						addRange.Invoke(list, new[] { sourceVal });
					}
					else
					{
						var addMethod = tlist.GetMethod("Add");
						foreach (var o in sourceVal as IEnumerable)
						{
							addMethod.Invoke(list, new[] { Activator.CreateInstance(genericArgument).InjectFrom<FastDeepCloneInjection>(o) });
						}
					}
					this.SetValue(mi.TargetProp, mi.Target, list);
					return;
				}

				throw new NotImplementedException(string.Format("Deep cloning for generic type {0} is not implemented", mi.SourceProp.Name));
			}

			// For simple object types create a new instace and apply the clone injection on it
			this.SetValue(mi.TargetProp, mi.Target, Activator.CreateInstance(mi.TargetProp.PropertyType).InjectFrom<FastDeepCloneInjection>(sourceVal));
		}
	}
}
