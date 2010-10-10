using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using HandBrake.Interop;

namespace VidCoder.Model
{
	public class SourceOption : DependencyObject
	{
		public SourceType Type { get; set; }
		public DriveInformation DriveInfo { get; set; }
	}
}
