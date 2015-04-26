using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HandBrake.ApplicationServices.Interop;
using HandBrake.ApplicationServices.Interop.Json.Scan;
using VidCoder.Model.Encoding;

namespace VidCoder.Model
{
	public class PreviewImageJob
	{
		public HandBrakeInstance ScanInstance { get; set; }

		public int UpdateVersion { get; set; }

		public int PreviewNumber { get; set; }

		/// <summary>
		/// Gets or sets the object to lock on before accessing the file cache image.
		/// </summary>
		public object ImageFileSync { get; set; }

		public VCProfile Profile { get; set; }

		public SourceTitle Title { get; set; }
	}
}
