using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Threading;
using System.Collections;
using Microsoft.Practices.Unity;

namespace VidCoder.Services
{
	public static class DispatchService
	{
		private static Dispatcher dispatchObject = Unity.Container.Resolve<Dispatcher>();

		public static void Invoke(Action action)
		{
			if (dispatchObject == null || dispatchObject.CheckAccess())
			{
				action();
			}
			else
			{
				dispatchObject.Invoke(action);
			}
		}

		public static void BeginInvoke(Action action)
		{
			dispatchObject.BeginInvoke(action);
		}
	}
}
