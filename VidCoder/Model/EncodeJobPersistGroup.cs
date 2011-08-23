using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VidCoder.Model
{
	public class EncodeJobPersistGroup
	{
		public EncodeJobPersistGroup()
		{
			this.EncodeJobs = new List<EncodeJobWithMetadata>();
		}

		public List<EncodeJobWithMetadata> EncodeJobs { get; set; }
	}
}
