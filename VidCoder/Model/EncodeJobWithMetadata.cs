using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HandBrake.Interop.Model;

namespace VidCoder.Model
{
	public class EncodeJobWithMetadata
	{
		public VCJob Job { get; set; }

		public bool ManualOutputPath { get; set; }

		public string NameFormatOverride { get; set; }

		public string PresetName { get; set; }
	}
}
