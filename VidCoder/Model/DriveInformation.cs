using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VidCoder.Model
{
	public enum DiscType
	{
		Dvd,
		BluRay
	}

	public class DriveInformation
	{
		public bool Empty { get; set; }
		public string RootDirectory { get; set; }
		public string VolumeLabel { get; set; }
		public DiscType DiscType { get; set; }
	}
}
