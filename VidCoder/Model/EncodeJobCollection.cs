using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using VidCoder.Model.Encoding;

namespace VidCoder.Model
{
	// Called ArrayOfEncodeJob for backwards compatibility.
	// This wrapper class makes this collection of encode jobs compile with sgen.
	[XmlType(TypeName = "ArrayOfEncodeJob")]
	public class EncodeJobCollection
	{
		[XmlElement(ElementName = "EncodeJob")]
		public List<VCJob> EncodeJobs { get; set; }
	}
}
