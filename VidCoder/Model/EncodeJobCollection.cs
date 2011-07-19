using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using HandBrake.Interop.Model;

namespace VidCoder.Model
{
	// Called ArrayOfEncodeJob for backwards compatibility.
	// This wrapper class makes this collection of encode jobs compile with sgen.
	[XmlType(TypeName = "ArrayOfEncodeJob")]
	public class EncodeJobCollection
	{
		[XmlElement(ElementName = "EncodeJob")]
		public List<EncodeJob> EncodeJobs { get; set; }
	}
}
