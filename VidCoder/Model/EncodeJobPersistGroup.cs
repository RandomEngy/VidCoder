using System.Collections.Generic;

namespace VidCoder.Model
{
	// This is only used with the old XML serialization
	public class EncodeJobPersistGroup
	{
		public EncodeJobPersistGroup()
		{
			this.EncodeJobs = new List<EncodeJobWithMetadata>();
		}

		public List<EncodeJobWithMetadata> EncodeJobs { get; set; }
	}
}
