using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VidCoderCommon.Model
{
	public class VCEncodeCompletedEventArgs : EventArgs
	{
		public VCEncodeCompletedEventArgs(VCEncodeResultCode result)
		{
			this.Result = result;
		}

		public VCEncodeResultCode Result { get; }
	}
}
