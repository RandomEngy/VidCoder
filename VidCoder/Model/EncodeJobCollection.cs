using System.Collections.Generic;
using System.Xml.Serialization;
using VidCoderCommon.Model;

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
