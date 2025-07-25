using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VidCoder.Model;

public enum PartFileNaming
{
	/// <summary>
	/// Name partial files as "filename.part.ext"
	/// </summary>
	PartInMiddle,

	/// <summary>
	/// Name partial files as "filename.ext.part" 
	/// </summary>
	PartAtEnd
}