﻿using HandBrake.Interop.Interop;
using HandBrake.Interop.Interop.Json.Scan;
using VidCoderCommon.Model;

namespace VidCoder.Model
{
	public class PreviewImageJob
	{
		public HandBrakeInstance ScanInstance { get; set; }

		public int UpdateVersion { get; set; }

		/// <summary>
		/// Gets or sets the 0-based preview index for the job.
		/// </summary>
		public int PreviewIndex { get; set; }

		/// <summary>
		/// Gets or sets the object to lock on before accessing the file cache image.
		/// </summary>
		public object ImageFileSync { get; set; }

		public VCJob Job { get; set; }

		public SourceTitle Title { get; set; }
	}
}
