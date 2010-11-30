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
		public static void Invoke(Action action)
		{
			Dispatcher dispatchObject = VidCoder.View.MainWindow.TheWindow.Dispatcher;

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
			Dispatcher dispatchObject = VidCoder.View.MainWindow.TheWindow.Dispatcher;

			dispatchObject.BeginInvoke(action);
		}
	}
}
