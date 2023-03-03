using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VidCoder.Model;

/// <summary>
/// Used when exporting/importing queues with XML files.
/// </summary>
public class EncodeJobsXml
{
	public int Version { get; set; }
	public IList<EncodeJobWithMetadata> Jobs { get; set; }
}
