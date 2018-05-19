using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Omu.ValueInjecter;
using Omu.ValueInjecter.Injections;

namespace VidCoderCommon.Utilities.Injection
{
	public class CloneInjection : PropertyInjection
	{
		protected override void Execute(PropertyInfo sourceProperty, object source, object target)
		{
			var targetProperty = target.GetType().GetProperty(sourceProperty.Name);
			if (targetProperty == null)
			{
				return;
			}

			var val = sourceProperty.GetValue(source);
			if (val == null)
			{
				return;
			}

			if (sourceProperty.DeclaringType != null && sourceProperty.DeclaringType.Name == "ReactiveObject")
			{
				// Skip any properties on ReactiveObject.
				return;
			}

			if (!targetProperty.CanWrite)
			{
				return;
			}

			targetProperty.SetValue(target, GetClone(sourceProperty, val));
		}

		private static object GetClone(PropertyInfo sourceProperty, object val)
		{
			if (sourceProperty.PropertyType.IsValueType || sourceProperty.PropertyType == typeof(string))
			{
				return val;
			}

			if (sourceProperty.PropertyType.IsArray)
			{
				var arr = val as Array;
				var arrClone = arr.Clone() as Array;

				for (int index = 0; index < arr.Length; index++)
				{
					var a = arr.GetValue(index);
					if (a.GetType().IsValueType || a is string) continue;

					arrClone.SetValue(Activator.CreateInstance(a.GetType()).InjectFrom<CloneInjection>(a), index);
				}

				return arrClone;
			}

			if (sourceProperty.PropertyType.IsGenericType)
			{
				// Handle IEnumerable<> also ICollection<> IList<> List<>
				if (sourceProperty.PropertyType.GetGenericTypeDefinition().GetInterfaces().Contains(typeof(IEnumerable)))
				{
					var genericType = sourceProperty.PropertyType.GetGenericArguments()[0];

					var listType = typeof(List<>).MakeGenericType(genericType);
					var list = Activator.CreateInstance(listType);

					var addMethod = listType.GetMethod("Add");
					foreach (var o in val as IEnumerable)
					{
						var listItem = genericType.IsValueType || genericType == typeof(string) ? o : Activator.CreateInstance(genericType).InjectFrom<CloneInjection>(o);
						addMethod.Invoke(list, new[] { listItem });
					}

					return list;
				}

				//unhandled generic type, you could also return null or throw
				return val;
			}

			// For simple object types create a new instance and apply the clone injection on it
			return Activator.CreateInstance(sourceProperty.PropertyType)
				.InjectFrom<CloneInjection>(val);
		}
	}
}
