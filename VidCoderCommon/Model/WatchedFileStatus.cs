using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VidCoderCommon.Model
{
	/// <summary>
	/// Status of a watched file as persisted in the SQLite database.
	/// </summary>
	public enum WatchedFileStatus
	{
		/// <summary>
		/// VidCoder is planning on encoding this file but hasn't finished it yet.
		/// </summary>
		Planned,

		/// <summary>
		/// VidCoder has successfully encoded this file.
		/// </summary>
		Succeeded,

		/// <summary>
		/// VidCoder tried to encode the file but it failed.
		/// </summary>
		Failed,

		/// <summary>
		/// The user canceled the encode, so we won't retry until asked.
		/// </summary>
		Canceled,

		/// <summary>
		/// The file was skipped by the system due to filtering rules.
		/// </summary>
		Skipped,

		/// <summary>
		/// The file is an output file from VidCoder and does not need to be re-encoded.
		/// </summary>
		Output
	}
}
