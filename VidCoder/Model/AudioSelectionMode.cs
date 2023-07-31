using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VidCoder.Model;

public enum AudioSelectionMode
{
	// Now called "Default", picks last selected track if match is found, otherwise first.
	Disabled = 0,
	None = 5,
	First = 3,
	ByIndex = 4,
	Language = 1,
	All = 2,
}
