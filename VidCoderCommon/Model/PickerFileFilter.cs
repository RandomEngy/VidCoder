using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VidCoderCommon.Model
{
	public class PickerFileFilter
	{
		public ISet<string> Extensions { get; set; }

		public int IgnoreFilesBelowMb { get; set; }
	}
}
