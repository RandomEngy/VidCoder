using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VidCoderCommon.Model;

namespace VidCoder.Model;

/// <summary>
/// A resolved preset associated with an encoding job.
/// </summary>
public class JobPreset
{
	public string Name { get; set; }

	public VCProfile Profile { get; set; }
}
