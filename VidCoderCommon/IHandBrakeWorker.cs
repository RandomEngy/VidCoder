using System.ServiceModel;
using HandBrake.ApplicationServices.Interop.Json.Scan;
using VidCoderCommon.Model;

namespace VidCoderCommon
{
	[ServiceContract(SessionMode = SessionMode.Required,
		CallbackContract = typeof(IHandBrakeWorkerCallback))]
	public interface IHandBrakeWorker
	{
		[OperationContract]
		void SetUpWorker(
			int verbosity,
			int previewCount,
			bool useDvdNav,
			double minTitleDurationSeconds,
			double cpuThrottlingFraction,
			string tempFolder);

		[OperationContract]
		void StartEncode(
			VCJob job,
			int previewNumber,
			int previewSeconds,
			bool dxvaDecoding,
			string defaultChapterNameFormat);

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
