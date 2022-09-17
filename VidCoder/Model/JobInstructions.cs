using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VidCoderCommon.Model;

namespace VidCoder.Model
{
	/// <summary>
	/// All the settings needed to encode jobs for a given source path: encode settings, picker, any overrides, etc.
	/// </summary>
	public class JobInstructions
	{
		public VCProfile Profile { get; set; }

		public Picker Picker { get; set; }

		public string DestinationOverride { get; set; }

		public bool IsBatch { get; set; }

		public bool Start { get; set; }
	}
}
