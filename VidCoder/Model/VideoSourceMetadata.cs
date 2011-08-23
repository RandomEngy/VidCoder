using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HandBrake.Interop.Model;

namespace VidCoder.Model
{
	public class VideoSourceMetadata
	{
		public string Name { get; set; }

		public DriveInformation DriveInfo { get; set; }
	}
}
