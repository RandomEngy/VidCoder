using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace VidCoder.DragDropUtils;

public class MoveToFolderAdorner : Adorner
{
	private static readonly Brush AdornerBrush;

	private AdornerLayer adornerLayer;

	static MoveToFolderAdorner()
	{
		AdornerBrush = new SolidColorBrush(Color.FromArgb(0x22, 0, 0xaa, 0xff));
		AdornerBrush.Freeze();
	}

	public MoveToFolderAdorner(UIElement adornedElement, AdornerLayer adornerLayer)
		: base(adornedElement)
	{
		this.adornerLayer = adornerLayer;
		this.IsHitTestVisible = false;

		this.adornerLayer.Add(this);
	}

	protected override void OnRender(DrawingContext drawingContext)
	{
		drawingContext.DrawRectangle(AdornerBrush, null, new Rect(new Point(0, 0), new Size(this.AdornedElement.RenderSize.Width, this.AdornedElement.RenderSize.Height)));
	}

	public void Detach()
	{
		this.adornerLayer.Remove(this);
	}
}
