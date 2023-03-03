using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;

namespace VidCoder.Model.WindowPlacer;

public class WindowPosition
{
	public Rect Position { get; set; }

	public Type ViewModelType { get; set; }
}
