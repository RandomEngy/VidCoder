using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using HandBrake.Interop.Model;

namespace VidCoder.Model
{
	public class SourceOption
	{
		public SourceType Type { get; set; }
		public DriveInformation DriveInfo { get; set; }
	}
}
