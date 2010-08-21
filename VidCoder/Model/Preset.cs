using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HandBrake.Interop;

namespace VidCoder.Model
{
	public class Preset
	{
		public string Name { get; set; }
		public bool IsBuiltIn { get; set; }
		public bool IsModified { get; set; }
		public EncodingProfile EncodingProfile { get; set; }
	}
}
