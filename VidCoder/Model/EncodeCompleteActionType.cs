using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;

namespace VidCoder.Model
{
	using LocalResources;

	public enum EncodeCompleteActionType
	{
		[Display(ResourceType = typeof(EnumsRes), Name = "EncodeCompleteActionType_DoNothing")]
		DoNothing = 0,

		[Display(ResourceType = typeof(EnumsRes), Name = "EncodeCompleteActionType_EjectDisc")]
		EjectDisc,

		[Display(ResourceType = typeof(EnumsRes), Name = "EncodeCompleteActionType_Sleep")]
		Sleep,

		[Display(ResourceType = typeof(EnumsRes), Name = "EncodeCompleteActionType_LogOff")]
		LogOff,

		[Display(ResourceType = typeof(EnumsRes), Name = "EncodeCompleteActionType_Shutdown")]
		Shutdown
	}
}