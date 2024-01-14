using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VidCoderCommon.Model;

namespace VidCoder.Model;

public class PendingScan
{
	public SourcePathWithMetadata SourcePath { get; set; }

	public JobInstructions JobInstructions { get; set; }
}
