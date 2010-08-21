using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Threading;
using System.Collections;

namespace VidCoder.Services
{
	public static class DispatchService
	{
		public static Dispatcher DispatchObject { get; set; }

		public static void Invoke(Action action)
		{
			if (DispatchObject == null || DispatchObject.CheckAccess())
			{
				action();
			}
			else
			{
				DispatchObject.Invoke(action);
			}
		}

		public static void BeginInvoke(Action action)
		{
			DispatchObject.BeginInvoke(action);
		}
	}
}
