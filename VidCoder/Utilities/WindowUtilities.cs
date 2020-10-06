using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VidCoder
{
	public static class WindowUtilities
	{
		public static IntPtr HookProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
		{
			if (msg == 0x0084 /*WM_NCHITTEST*/ )
			{
				// This prevents a crash in WindowChromeWorker._HandleNCHitTest
				try
				{
					lParam.ToInt32();
				}
				catch (OverflowException)
				{
					handled = true;
				}
			}

			return IntPtr.Zero;
		}
	}
}
