using HandBrake.ApplicationServices.Interop.Json.Encode;

namespace VidCoderWorker
{
	using System;
	using System.ServiceModel;

	[ServiceContract(SessionMode = SessionMode.Required,
		CallbackContract = typeof(IHandBrakeEncoderCallback))]
	public interface IHandBrakeEncoder
	{
		[OperationContract]
		void StartEncode(JsonEncodeObject encodeObject, int verbosity, int previewCount, bool useDvdNav, double minTitleDurationSeconds);

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
