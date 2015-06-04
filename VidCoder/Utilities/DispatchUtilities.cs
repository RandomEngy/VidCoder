using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Collections;
using System.Windows;

namespace VidCoder
{
	public static class DispatchUtilities
	{
		public static void Invoke(Action action)
		{
			Dispatcher dispatchObject = Application.Current.Dispatcher;

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
			Dispatcher dispatchObject = Application.Current.Dispatcher;

			dispatchObject.BeginInvoke(action);
		}

		public static Task InvokeAsync(Action action)
		{
			Dispatcher dispatchObject = Application.Current.Dispatcher;
			var tcs = new TaskCompletionSource<object>();

			if (dispatchObject == null || dispatchObject.CheckAccess())
			{
				action();
				tcs.SetResult(null);
				return tcs.Task;
			}

			dispatchObject.BeginInvoke(new Action(() =>
			{
				try
				{
					action();
					tcs.SetResult(null);
				}
				catch (Exception exception)
				{
					tcs.SetException(exception);
				}
			}));

			return tcs.Task;
		}
	}
}
