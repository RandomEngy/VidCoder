using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VidCoderCommon.Model
{
	/// <summary>
	/// Represents a folder being watched for source files.
	/// </summary>
	public class WatchedFolder
	{
		/// <summary>
		/// The path of the folder to watch.
		/// </summary>
		public string Path { get; set; }

		/// <summary>
		/// The name of the picker to use.
		/// </summary>
		public string Picker { get; set; }

		/// <summary>
		/// The name of the encoding preset to use.
		/// </summary>
		public string Preset { get; set; }
	}
}
