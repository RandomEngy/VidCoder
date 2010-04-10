using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Documents;
using System.Windows;
using System.Windows.Media;

namespace VidCoder.DragDropUtils
{
	public class InsertionAdorner : Adorner
	{
		private bool isSeparatorHorizontal;
		public bool IsInFirstHalf { get; set; }
		private AdornerLayer adornerLayer;
		private static Pen pen;
		private static PathGeometry triangle;

		// Create the pen and triangle in a static constructor and freeze them to improve performance.
		static InsertionAdorner()
		{
			pen = new Pen { Brush = Brushes.Gray, Thickness = 2 };
			pen.Freeze();

			LineSegment firstLine = new LineSegment(new Point(0, -5), false);
			firstLine.Freeze();
			LineSegment secondLine = new LineSegment(new Point(0, 5), false);
			secondLine.Freeze();

			PathFigure figure = new PathFigure { StartPoint = new Point(5, 0) };
			figure.Segments.Add(firstLine);
			figure.Segments.Add(secondLine);
			figure.Freeze();

			triangle = new PathGeometry();
			triangle.Figures.Add(figure);
			triangle.Freeze();
		}

		public InsertionAdorner(bool isSeparatorHorizontal, bool isInFirstHalf, UIElement adornedElement, AdornerLayer adornerLayer)
			: base(adornedElement)
		{
			this.isSeparatorHorizontal = isSeparatorHorizontal;
			this.IsInFirstHalf = isInFirstHalf;
			this.adornerLayer = adornerLayer;
			this.IsHitTestVisible = false;

			this.adornerLayer.Add(this);
		}

		// This draws one line and two triangles at each end of the line.
		protected override void OnRender(DrawingContext drawingContext)
		{
			Point startPoint;
			Point endPoint;

			CalculateStartAndEndPoint(out startPoint, out endPoint);
			drawingContext.DrawLine(pen, startPoint, endPoint);

			if (this.isSeparatorHorizontal)
			{
				DrawTriangle(drawingContext, startPoint, 0);
				DrawTriangle(drawingContext, endPoint, 180);
			}
			else
			{
				DrawTriangle(drawingContext, startPoint, 90);
				DrawTriangle(drawingContext, endPoint, -90);
			}
		}

		private void DrawTriangle(DrawingContext drawingContext, Point origin, double angle)
		{
			drawingContext.PushTransform(new TranslateTransform(origin.X, origin.Y));
			drawingContext.PushTransform(new RotateTransform(angle));

			drawingContext.DrawGeometry(pen.Brush, null, triangle);

			drawingContext.Pop();
			drawingContext.Pop();
		}

		private void CalculateStartAndEndPoint(out Point startPoint, out Point endPoint)
		{
			startPoint = new Point();
			endPoint = new Point();
			
			double width = this.AdornedElement.RenderSize.Width;
			double height = this.AdornedElement.RenderSize.Height;

			if (this.isSeparatorHorizontal)
			{
				endPoint.X = width;
				if (!this.IsInFirstHalf)
				{
					startPoint.Y = height;
					endPoint.Y = height;
				}
			}
			else
			{
				endPoint.Y = height;
				if (!this.IsInFirstHalf)
				{
					startPoint.X = width;
					endPoint.X = width;
				}
			}		
		}

		public void Detach()
		{
			this.adornerLayer.Remove(this);
		}

	}
}
