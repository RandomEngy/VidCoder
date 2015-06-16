using System;
using System.Runtime.InteropServices;
using System.Windows;
using Newtonsoft.Json;

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

		public RECT(int left, int top, int right, int bottom)
		{
			this.Left = left;
			this.Top = top;
			this.Right = right;
			this.Bottom = bottom;
		}
	}

	// POINT structure required by WINDOWPLACEMENT structure
	[Serializable]
	[StructLayout(LayoutKind.Sequential)]
	public struct POINT
	{
		public int X;
		public int Y;

		public POINT(int x, int y)
		{
			this.X = x;
			this.Y = y;
		}
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

	public static class WindowPlacement
	{
		[DllImport("user32.dll")]
		private static extern bool SetWindowPlacement(IntPtr hWnd, [In] ref WINDOWPLACEMENT lpwndpl);

		[DllImport("user32.dll")]
		private static extern bool GetWindowPlacement(IntPtr hWnd, out WINDOWPLACEMENT lpwndpl);

		[DllImport("user32.dll")]
		private static extern IntPtr MonitorFromRect([In] ref RECT lprc, uint dwFlags);

		[DllImport("user32.dll")]
		private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

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
				placement.showCmd = (placement.showCmd == SW_SHOWMINIMIZED ? SW_SHOWNORMAL : placement.showCmd);

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
			catch (JsonException)
			{
				// Parsing placement JSON failed. Fail silently.
			}
		}

		public static WINDOWPLACEMENT ParsePlacementJson(string placementJson)
		{
			return JsonConvert.DeserializeObject<WINDOWPLACEMENT>(placementJson);
		}

		public static string GetPlacement(IntPtr windowHandle)
		{
			WINDOWPLACEMENT placement = new WINDOWPLACEMENT();
			GetWindowPlacement(windowHandle, out placement);

			return JsonConvert.SerializeObject(placement);
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
	}
}
