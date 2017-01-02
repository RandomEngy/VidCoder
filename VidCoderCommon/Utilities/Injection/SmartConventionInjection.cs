using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using Omu.ValueInjecter;
using Omu.ValueInjecter.Injections;
using Omu.ValueInjecter.Utils;

namespace VidCoderCommon.Utilities.Injection
{
	public class SmartConventionInjection : ValueInjection
	{
		private class Path
		{
			public IDictionary<string, string> MatchingProps { get; set; }
		}

		private static readonly ConcurrentDictionary<Type, ConcurrentDictionary<KeyValuePair<Type, Type>, Path>> WasLearned = new ConcurrentDictionary<Type, ConcurrentDictionary<KeyValuePair<Type, Type>, Path>>();

		protected virtual void SetValue(PropertyInfo prop, object component, object value)
		{
			prop.SetValue(component, value);
		}

		protected virtual object GetValue(PropertyInfo prop, object component)
		{
			return prop.GetValue(component);
		}

		protected virtual bool Match(SmartConventionInfo c)
		{
			return c.SourceProp.Name == c.TargetProp.Name && c.SourceProp.PropertyType == c.TargetProp.PropertyType;
		}

		protected virtual void ExecuteMatch(SmartMatchInfo mi)
		{
			this.SetValue(mi.TargetProp, mi.Target, this.GetValue(mi.SourceProp, mi.Source));
		}

		private Path Learn(object source, object target)
		{
			Path path = null;
			var sourceProps = source.GetProps();
			var targetProps = target.GetProps();
			var smartConventionInfo = new SmartConventionInfo
			{
				SourceType = source.GetType(),
				TargetType = target.GetType()
			};

			for (var i = 0; i < sourceProps.Length; i++)
			{
				var sourceProp = sourceProps[i];
				smartConventionInfo.SourceProp = sourceProp;

				for (var j = 0; j < targetProps.Length; j++)
				{
					var targetProp = targetProps[j];
					smartConventionInfo.TargetProp = targetProp;

					if (!this.Match(smartConventionInfo)) continue;
					if (path == null)
						path = new Path
						{
							MatchingProps = new Dictionary<string, string> { { smartConventionInfo.SourceProp.Name, smartConventionInfo.TargetProp.Name } }
						};
					else path.MatchingProps.Add(smartConventionInfo.SourceProp.Name, smartConventionInfo.TargetProp.Name);
				}
			}
			return path;
		}

		protected override void Inject(object source, object target)
		{
			var sourceProps = source.GetProps();
			var targetProps = target.GetProps();

			var cacheEntry = WasLearned.GetOrAdd(this.GetType(), new ConcurrentDictionary<KeyValuePair<Type, Type>, Path>());

			var path = cacheEntry.GetOrAdd(new KeyValuePair<Type, Type>(source.GetType(), target.GetType()), pair => this.Learn(source, target));

			if (path == null) return;

			foreach (var pair in path.MatchingProps)
			{
				var sourceProp = sourceProps.First(p => p.Name == pair.Key);
				var targetProp = targetProps.First(p => p.Name == pair.Value);
				this.ExecuteMatch(new SmartMatchInfo
				{
					Source = source,
					Target = target,
					SourceProp = sourceProp,
					TargetProp = targetProp
				});
			}
		}
	}
}
