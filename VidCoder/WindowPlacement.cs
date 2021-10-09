using System;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;

namespace VidCoder
{
	// RECT structure required by WINDOWPLACEMENT structure
	[Serializable]
	[StructLayout(LayoutKind.Sequential)]
	public struct RECT
	{
		public int Left;
		public int Top;
		public int Right;
		public int Bottom;
	}

	// POINT structure required by WINDOWPLACEMENT structure
	[Serializable]
	[StructLayout(LayoutKind.Sequential)]
	public struct POINT
	{
		public int X;
		public int Y;
	}

	// WINDOWPLACEMENT stores the position, size, and state of a window
	[Serializable]
	[StructLayout(LayoutKind.Sequential)]
	public struct WINDOWPLACEMENT
	{
		public int length;
		public int flags;
		public int showCmd;
		public POINT minPosition;
		public POINT maxPosition;
		public RECT normalPosition;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct MONITORINFO
	{
		public int cbSize;
		public RECT rcMonitor;
		public RECT rcWork;
		public uint dwFlags;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct MINMAXINFO
	{
		public POINT ptReserved;
		public POINT ptMaxSize;
		public POINT ptMaxPosition;
		public POINT ptMinTrackSize;
		public POINT ptMaxTrackSize;
	}

	public static class WindowPlacement
	{
		// Need wrapper classes for the WINDOWPLACEMENT because System.Text.Json does not support JSON serialization of structs yet.
		private class RectClass
		{
			public RectClass()
			{
			}

			public RectClass(RECT rectStruct)
			{
				this.Left = rectStruct.Left;
				this.Top = rectStruct.Top;
				this.Right = rectStruct.Right;
				this.Bottom = rectStruct.Bottom;
			}

			public int Left { get; set; }
			public int Top { get; set; }
			public int Right { get; set; }
			public int Bottom { get; set; }

			public RECT ToStruct()
			{
				return new RECT
				{
					Left = this.Left,
					Top = this.Top,
					Right = this.Right,
					Bottom = this.Bottom
				};
			}
		}

		private class PointClass
		{
			public PointClass()
			{
			}

			public PointClass(POINT pointStruct)
			{
				this.X = pointStruct.X;
				this.Y = pointStruct.Y;
			}

			public int X { get; set; }
			public int Y { get; set; }

			public POINT ToStruct()
			{
				return new POINT
				{
					X = this.X,
					Y = this.Y
				};
			}
		}

		private class WindowPlacementClass
		{
			public WindowPlacementClass()
			{
			}

			public WindowPlacementClass(WINDOWPLACEMENT placementStruct)
			{
				this.length = placementStruct.length;
				this.flags = placementStruct.flags;
				this.showCmd = placementStruct.showCmd;
				this.minPosition = new PointClass(placementStruct.minPosition);
				this.maxPosition = new PointClass(placementStruct.maxPosition);
				this.normalPosition = new RectClass(placementStruct.normalPosition);
			}

			public int length { get; set; }
			public int flags { get; set; }
			public int showCmd { get; set; }
			public PointClass minPosition { get; set; }
			public PointClass maxPosition { get; set; }
			public RectClass normalPosition { get; set; }

			public WINDOWPLACEMENT ToStruct()
			{
				return new WINDOWPLACEMENT
				{
					length = this.length,
					flags = this.flags,
					showCmd = this.showCmd,
					minPosition = this.minPosition.ToStruct(),
					maxPosition = this.maxPosition.ToStruct(),
					normalPosition = this.normalPosition.ToStruct()
				};
			}
		}

		[DllImport("user32.dll")]
		private static extern bool SetWindowPlacement(IntPtr hWnd, [In] ref WINDOWPLACEMENT lpwndpl);

		[DllImport("user32.dll")]
		private static extern bool GetWindowPlacement(IntPtr hWnd, out WINDOWPLACEMENT lpwndpl);

		[DllImport("user32.dll")]
		private static extern IntPtr MonitorFromRect([In] ref RECT lprc, uint dwFlags);

		[DllImport("user32.dll")]
		private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

		[DllImport("user32.dll")]
		private static extern IntPtr MonitorFromWindow(IntPtr handle, uint flags);

		public const int WM_GETMINMAXINFO = 0x0024;
		public const int WM_NCHITTEST = 0x0084;

		private const int SW_SHOWNORMAL = 1;
		private const int SW_SHOWMINIMIZED = 2;
		private const int SW_SHOWNOACTIVATE = 4;

		private const uint MONITOR_DEFAULTTONULL = 0x00000000;
		private const uint MONITOR_DEFAULTTOPRIMARY = 0x00000001;
		private const uint MONITOR_DEFAULTTONEAREST = 0x00000002;

		public static void SetPlacement(IntPtr windowHandle, string placementJson)
		{
			if (string.IsNullOrEmpty(placementJson))
			{
				return;
			}

			try
			{
				WINDOWPLACEMENT placement = ParsePlacementJson(placementJson);

				placement.length = Marshal.SizeOf(typeof(WINDOWPLACEMENT));
				placement.flags = 0;
				placement.showCmd = placement.showCmd == SW_SHOWMINIMIZED ? SW_SHOWNORMAL : placement.showCmd;

				IntPtr closestMonitorPtr = MonitorFromRect(ref placement.normalPosition, MONITOR_DEFAULTTONEAREST);
				MONITORINFO closestMonitorInfo = new MONITORINFO();
				closestMonitorInfo.cbSize = Marshal.SizeOf(typeof (MONITORINFO));
				bool getInfoSucceeded = GetMonitorInfo(closestMonitorPtr, ref closestMonitorInfo);

				if (getInfoSucceeded && !RectanglesIntersect(placement.normalPosition, closestMonitorInfo.rcMonitor))
				{
					placement.normalPosition = PlaceOnScreen(closestMonitorInfo.rcMonitor, placement.normalPosition);
				}

				SetWindowPlacement(windowHandle, ref placement);
			}
			catch (Exception)
			{
				// Parsing placement JSON failed. Fail silently.
			}
		}

		public static WINDOWPLACEMENT ParsePlacementJson(string placementJson)
		{
			WindowPlacementClass placementClass = JsonSerializer.Deserialize<WindowPlacementClass>(placementJson);
			return placementClass.ToStruct();
		}

		public static string GetPlacement(IntPtr windowHandle)
		{
			WINDOWPLACEMENT placement = new WINDOWPLACEMENT();
			GetWindowPlacement(windowHandle, out placement);

			WindowPlacementClass placementClass = new WindowPlacementClass(placement);

			return JsonSerializer.Serialize(placementClass);
		}

		private static bool RectanglesIntersect(RECT a, RECT b)
		{
			if (a.Left > b.Right || a.Right < b.Left)
			{
				return false;
			}

			if (a.Top > b.Bottom || a.Bottom < b.Top)
			{
				return false;
			}

			return true;
		}

		private static RECT PlaceOnScreen(RECT monitorRect, RECT windowRect)
		{
			int monitorWidth = monitorRect.Right - monitorRect.Left;
			int monitorHeight = monitorRect.Bottom - monitorRect.Top;

			if (windowRect.Right < monitorRect.Left)
			{
				// Off left side
				int width = windowRect.Right - windowRect.Left;
				if (width > monitorWidth)
				{
					width = monitorWidth;
				}

				windowRect.Left = monitorRect.Left;
				windowRect.Right = windowRect.Left + width;
			}
			else if (windowRect.Left > monitorRect.Right)
			{
				// Off right side
				int width = windowRect.Right - windowRect.Left;
				if (width > monitorWidth)
				{
					width = monitorWidth;
				}

				windowRect.Right = monitorRect.Right;
				windowRect.Left = windowRect.Right - width;
			}

			if (windowRect.Bottom < monitorRect.Top)
			{
				// Off top
				int height = windowRect.Bottom - windowRect.Top;
				if (height > monitorHeight)
				{
					height = monitorHeight;
				}

				windowRect.Top = monitorRect.Top;
				windowRect.Bottom = windowRect.Top + height;
			}
			else if (windowRect.Top > monitorRect.Bottom)
			{
				// Off bottom
				int height = windowRect.Bottom - windowRect.Top;
				if (height > monitorHeight)
				{
					height = monitorHeight;
				}

				windowRect.Bottom = monitorRect.Bottom;
				windowRect.Top = windowRect.Bottom - height;
			}

			return windowRect;
		}

		public static Rect ToRect(this WINDOWPLACEMENT placement)
		{
			RECT pos = placement.normalPosition;
			return new Rect(pos.Left, pos.Top, pos.Right - pos.Left, pos.Bottom - pos.Top);
		}

		/// <summary>
		/// Handles WndProc messages
		/// </summary>
		/// <param name="hwnd">The window handle.</param>
		/// <param name="msg">The message ID.</param>
		/// <param name="wParam">The message's wParam value.</param>
		/// <param name="lParam">The message's lParam value.</param>
		/// <param name="handled"></param>
		/// <returns>A value that indicates whether the message was handled. Set the value to true if the message was handled; otherwise, false.</returns>
		public static IntPtr HookProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
		{
			switch (msg)
			{
				case WM_NCHITTEST:
					// Works around a Logitech mouse driver bug, code from https://developercommunity.visualstudio.com/content/problem/167357/overflow-exception-in-windowchrome.html
					// This prevents a crash in WindowChromeWorker._HandleNCHitTest
					try
					{
						lParam.ToInt32();
					}
					catch (OverflowException)
					{
						handled = true;
					}

					break;
				case WM_GETMINMAXINFO:
					// We need to tell the system what our size should be when maximized. Otherwise it will cover the whole screen,
					// including the task bar.
					MINMAXINFO mmi = (MINMAXINFO)Marshal.PtrToStructure(lParam, typeof(MINMAXINFO));

					// Adjust the maximized size and position to fit the work area of the correct monitor
					IntPtr monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);

					if (monitor != IntPtr.Zero)
					{
						MONITORINFO monitorInfo = new MONITORINFO();
						monitorInfo.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
						GetMonitorInfo(monitor, ref monitorInfo);
						RECT rcWorkArea = monitorInfo.rcWork;
						RECT rcMonitorArea = monitorInfo.rcMonitor;
						mmi.ptMaxPosition.X = Math.Abs(rcWorkArea.Left - rcMonitorArea.Left);
						mmi.ptMaxPosition.Y = Math.Abs(rcWorkArea.Top - rcMonitorArea.Top);
						mmi.ptMaxSize.X = Math.Abs(rcWorkArea.Right - rcWorkArea.Left);
						mmi.ptMaxSize.Y = Math.Abs(rcWorkArea.Bottom - rcWorkArea.Top);
					}

					Marshal.StructureToPtr(mmi, lParam, true);

					break;
				default:
					break;
			}

			return IntPtr.Zero;
		}
	}
}
