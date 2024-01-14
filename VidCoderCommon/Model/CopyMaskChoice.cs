using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VidCoderCommon.Model;

public class CopyMaskChoice
{
	/// <summary>
	/// The codec.
	/// </summary>
	/// <remarks>This is the short encoder name without the "copy:" prefix.</remarks>
	public string Codec { get; set; }

	public bool Enabled { get; set; }
}
