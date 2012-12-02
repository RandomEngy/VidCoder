using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HandBrake.Interop.Model.Encoding;

namespace VidCoder.Model
{
	using System.Resources;
	using LocalResources;

	public class Preset
	{
		private static ResourceManager manager = new ResourceManager(typeof(MainRes));

		public string Name { get; set; }
		public bool IsBuiltIn { get; set; }
		public bool IsModified { get; set; }
		public bool IsQueue { get; set; }
		public VCProfile EncodingProfile { get; set; }

		public string DisplayName
		{
			get
			{
				if (!this.IsBuiltIn)
				{
					return this.Name;
				}

				string displayName = manager.GetString("Preset_" + this.Name);
				if (displayName == null)
				{
					return this.Name;
				}

				return displayName;
			}
		}
	}
}
