using System.ServiceModel;
using HandBrake.ApplicationServices.Interop.Json.Scan;
using VidCoderCommon.Model;

namespace VidCoderCommon
{
	[ServiceContract(SessionMode = SessionMode.Required,
		CallbackContract = typeof(IHandBrakeEncoderCallback))]
	public interface IHandBrakeEncoder
	{
		[OperationContract]
		void StartEncode(
			VCJob job,
			SourceTitle encodeTitle,
			int previewNumber,
			int previewSeconds,
			int verbosity, 
			int previewCount, 
			bool useDvdNav,
			bool dxvaDecoding,
			double minTitleDurationSeconds,
			string defaultChapterNameFormat,
			double cpuThrottlingFraction);

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
