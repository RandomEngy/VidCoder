using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VidCoder.Model
{
	public class EncodeResult
	{
		public string Destination { get; set; }
		public EncodeResultStatus Status { get; set; }
		public TimeSpan EncodeTime { get; set; }
		public string LogPath { get; set; }

		public bool Succeeded
		{
			get
			{
				return this.Status != EncodeResultStatus.Failed;
			}
		}
	}
}
