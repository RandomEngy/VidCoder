using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;

namespace VidCoder.Model.WindowPlacer;

public class WindowPlacerLocation
{
	public Side AnchorSide { get; set; }

	public Side Alignment { get; set; }

	public Rect Position { get; set; }

	public Rect AnchorPosition { get; set; }

	public double DistanceToMain { get; set; }

	public double AnchorSimilarityFraction { get; set; }
}
