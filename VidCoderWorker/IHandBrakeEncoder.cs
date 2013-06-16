

namespace VidCoderWorker
{
	using System;
	using System.ServiceModel;
	using HandBrake.Interop.Model;

	[ServiceContract(SessionMode = SessionMode.Required,
		CallbackContract = typeof(IHandBrakeEncoderCallback))]
	public interface IHandBrakeEncoder
	{
		[OperationContract]
		void StartEncode(EncodeJob job, bool preview, int previewNumber, int previewSeconds, double overallSelectedLengthSeconds, int verbosity, int previewCount, bool useDvdNav);

		[OperationContract]
		void PauseEncode();

		[OperationContract]
		void ResumeEncode();

		[OperationContract]
		void StopEncode();

		[OperationContract]
		string Ping();
	}
}
