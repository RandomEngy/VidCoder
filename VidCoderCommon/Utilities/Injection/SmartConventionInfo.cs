using System;
using System.ComponentModel;
using System.Reflection;

namespace VidCoderCommon.Utilities.Injection
{
	public class SmartConventionInfo
	{
		public Type SourceType { get; set; }
		public Type TargetType { get; set; }

		public PropertyInfo SourceProp { get; set; }
		public PropertyInfo TargetProp { get; set; }
	}
}
