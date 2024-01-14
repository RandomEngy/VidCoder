using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using VidCoder.Model;

namespace VidCoder.Extensions;

public static class RectExtensions
{
	public static double GetSide(this Rect rect, Side side)
	{
		switch (side)
		{
			case Side.Top:
			case Side.Bottom:
				return rect.Width;
			case Side.Left:
			case Side.Right:
				return rect.Height;
		}

		return 0;
	}
}
