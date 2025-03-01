using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VidCoder.Model
{
    public class EncodeCompleteActionPersisted
    {
		public EncodeCompleteActionType ActionType { get; set; }

		public string DriveLetter { get; set; }
	}
}
