using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace VidCoderCommon.Utilities.Injection
{
	public class SmartMatchInfo
	{
		public PropertyInfo SourceProp { get; set; }
		public PropertyInfo TargetProp { get; set; }
		public object Source { get; set; }
		public object Target { get; set; }
	}
}
