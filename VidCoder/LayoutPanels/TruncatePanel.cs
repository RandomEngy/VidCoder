using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace VidCoder.LayoutPanels;

public class TruncatePanel : Panel
{
	protected override Size MeasureOverride(Size availableSize)
	{
		foreach (UIElement child in this.InternalChildren)
		{
			child.Measure(new Size(availableSize.Width, double.PositiveInfinity));
		}

		double currentHeight = 0;
		foreach (UIElement child in this.InternalChildren)
		{
			double newHeight = currentHeight + child.DesiredSize.Height;

			if (newHeight > availableSize.Height)
			{
				break;
			}
			else
			{
				currentHeight = newHeight;
			}
		}

		return new Size(availableSize.Width, currentHeight);
	}

	protected override Size ArrangeOverride(Size finalSize)
	{
		double currentHeight = 0;
		foreach (UIElement child in this.InternalChildren)
		{
			if (currentHeight + child.DesiredSize.Height <= finalSize.Height + .0001)
			{
				child.Arrange(new Rect(new Point(0, currentHeight), child.DesiredSize));
			}
			else
			{
				child.Arrange(new Rect(new Point(0, -100000), child.DesiredSize));
			}

			currentHeight += child.DesiredSize.Height;
		}

		return finalSize;
	}
}
