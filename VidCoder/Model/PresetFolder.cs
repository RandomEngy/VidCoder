using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VidCoder.Model;

public class PresetFolder
{
	public long Id { get; set; }

	public string Name { get; set; }

	public long ParentId { get; set; }

	public bool IsExpanded { get; set; }
}
