using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VidCoder.Messages
{
	public class ContainerChangedMessage
	{
		public ContainerChangedMessage(string containerName)
		{
			this.ContainerName = containerName;
		}

		public string ContainerName { get; set; }
	}
}
