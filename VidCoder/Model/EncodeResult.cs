using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VidCoder.Model;

public class EncodeResult
{
	/// <summary>
	/// Where the file was originally supposed to go.
	/// </summary>
	public string Destination { get; set; }

	/// <summary>
	/// The name of the failed file, if KeepFailedFiles is enabled.
	/// </summary>
	public string FailedFilePath { get; set; }

	public EncodeResultStatus Status { get; set; }

	public TimeSpan EncodeTime { get; set; }

	public TimeSpan PauseTime { get; set; }

	public string LogPath { get; set; }
	public long SizeBytes { get; set; }

	public bool Succeeded
	{
		get
		{
			return this.Status != EncodeResultStatus.Failed;
		}
	}
}
