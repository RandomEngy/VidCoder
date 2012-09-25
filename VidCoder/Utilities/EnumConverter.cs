using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VidCoder
{
	public static class EnumConverter
	{
		public static TDest Convert<TSource, TDest>(TSource value)
		{
			return (TDest) Enum.Parse(typeof(TDest), value.ToString());
		}
	}
}
