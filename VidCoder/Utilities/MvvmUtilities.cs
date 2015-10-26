using System;
using System.Linq.Expressions;
using System.Reactive.Linq;
using System.Reflection;

namespace VidCoder
{
	public static class MvvmUtilities
	{
		public static string GetPropertyName<T>(Expression<Func<T>> propertyExpression)
		{
			if (propertyExpression == null)
			{
				throw new ArgumentNullException("propertyExpression");
			}

			var body = propertyExpression.Body as MemberExpression;

			if (body == null)
			{
				throw new ArgumentException("Invalid argument", "propertyExpression");
			}

			var property = body.Member as PropertyInfo;

			if (property == null)
			{
				throw new ArgumentException("Argument is not a property", "propertyExpression");
			}

			return property.Name;
		}

		public static IObservable<T> CreateConstantObservable<T>(T value)
		{
			return Observable.Create<T>(observer =>
			{
				observer.OnNext(value);
				return () => { };
			});
		} 
	}
}
