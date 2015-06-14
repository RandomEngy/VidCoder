using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;

namespace VidCoderCommon.Model
{
	public class PresetCollection
	{
		[XmlElement(ElementName = "Preset")]
		public List<Preset> Presets { get; set; } 
	}
}
