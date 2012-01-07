using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;

namespace VidCoder.Model
{
	public enum EncodeCompleteActionType
	{
		[Display(Name = "Do nothing")]
		DoNothing = 0,

		[Display(Name = "Eject {0}:")]
		EjectDisc,

		[Display(Name = "Sleep")]
		Sleep,

		[Display(Name = "Log off")]
		LogOff,

		[Display(Name = "Shut down")]
		Shutdown
	}
}