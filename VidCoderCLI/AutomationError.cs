using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VidCoderCLI
{
	using System.Runtime.Serialization;

	[DataContract]
	public class AutomationError
	{
		[DataMember]
		public string Message { get; set; }
	}
}
