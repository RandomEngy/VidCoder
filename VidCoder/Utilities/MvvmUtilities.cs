using System;
using System.Linq.Expressions;
using System.Reactive.Linq;
using System.Reflection;

namespace VidCoder
{
	public static class MvvmUtilities
	{
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
