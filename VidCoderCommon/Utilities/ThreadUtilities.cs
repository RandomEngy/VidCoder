using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace VidCoderCommon.Utilities;

public static class ThreadUtilities
{
	public static Action Debounce(this Action func, int milliseconds)
	{
		CancellationTokenSource cancelTokenSource = null;

		return () =>
		{
			cancelTokenSource?.Cancel();
			cancelTokenSource = new CancellationTokenSource();

			Task.Delay(milliseconds, cancelTokenSource.Token)
				.ContinueWith(t =>
				{
					if (t.IsCompletedSuccessfully)
					{
						func();
					}
				}, TaskScheduler.Default);
		};
	}
}
