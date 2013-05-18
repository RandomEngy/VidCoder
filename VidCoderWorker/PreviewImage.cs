using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VidCoderWorker
{
	using System.Runtime.Serialization;

	[DataContract]
	public class PreviewImage
	{
		[DataMember]
		public int Width { get; set; }

		[DataMember]
		public int Height { get; set; }

		[DataMember]
		public byte[] Data { get; set; }
	}
}
