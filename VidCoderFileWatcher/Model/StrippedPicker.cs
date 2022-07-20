using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VidCoderCommon.Utilities;

namespace VidCoderFileWatcher.Model
{
	/// <summary>
	/// Model for the Picker with only fields needed to pick files.
	/// </summary>
	public class StrippedPicker
	{
		public string VideoFileExtensions { get; set; } = PickerDefaults.VideoFileExtensions;

		public bool IgnoreFilesBelowMbEnabled { get; set; }

		public int IgnoreFilesBelowMb { get; set; } = PickerDefaults.IgnoreFilesBelowMb;
	}
}
