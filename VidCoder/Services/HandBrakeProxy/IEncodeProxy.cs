using System;
using HandBrake.Interop.Interop.EventArgs;
using VidCoder.Services;
using VidCoderCommon.Model;

namespace VidCoder
{
	/// <summary>
	/// Abstraction for dealing with either a worker process or a local encoder.
	/// </summary>
	public interface IEncodeProxy
	{
		event EventHandler EncodeStarted;

		/// <summary>
		/// Fires for progress updates when encoding.
		/// </summary>
		event EventHandler<EncodeProgressEventArgs> EncodeProgress;

		/// <summary>
		/// Fires when an encode has completed.
		/// </summary>
		event EventHandler<EncodeCompletedEventArgs> EncodeCompleted;

		void StartEncode(
			VCJob job,
			IAppLogger logger,
			bool preview, 
			int previewNumber, 
			int previewSeconds, 
			double overallSelectedLengthSeconds);

		void PauseEncode();

		void ResumeEncode();

		void StopEncode();

		void StopAndWait();
		void StartEncode(string encodeJson, IAppLogger logger);
	}
}