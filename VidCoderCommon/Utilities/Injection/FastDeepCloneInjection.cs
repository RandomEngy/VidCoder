using System.Reflection;
using FastMember;

namespace VidCoderCommon.Utilities.Injection
{
	public class FastDeepCloneInjection : DeepCloneInjection
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
	}
}
