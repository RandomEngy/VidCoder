using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VidCoderCommon.Model
{
	public enum VCEncodeResultCode
	{
		// common.h
		// typedef enum
		// {
		//   HB_ERROR_NONE = 0,
		//   HB_ERROR_CANCELED = 1,
		//   HB_ERROR_WRONG_INPUT = 2,
		//   HB_ERROR_INIT = 3,
		//   HB_ERROR_UNKNOWN = 4,
		//   HB_ERROR_READ = 5
		// }
		// hb_error_code;

		#region HandBrake error codes
		/// <summary>
		/// Encode succeeded.
		/// </summary>
		Succeeded = 0,

		/// <summary>
		/// The encode was canceled.
		/// </summary>
		Canceled = 1,

		/// <summary>
		/// There was a problem with the format of the input video file.
		/// </summary>
		ErrorWrongInput = 2,

		/// <summary>
		/// There was an error initializing the encode.
		/// </summary>
		ErrorInit = 3,

		/// <summary>
		/// There was an unknown error.
		/// </summary>
		ErrorUnknown = 4,

		/// <summary>
		/// There was an I/O issue reading the file.
		/// </summary>
		ErrorRead = 5,
		#endregion

		#region VidCoder error codes
		/// <summary>
		/// The process hosting the encode crashed.
		/// </summary>
		ErrorHandBrakeProcessCrashed = 100,

		/// <summary>
		/// There was an issue with the cross-process communication.
		/// </summary>
		ErrorProcessCommunication = 101,

		/// <summary>
		/// Unable to create the output directory.
		/// </summary>
		ErrorCouldNotCreateOutputDirectory = 102,

		/// <summary>
		/// The scan phase failed.
		/// </summary>
		ErrorScanFailed = 103
		#endregion
	}
}
