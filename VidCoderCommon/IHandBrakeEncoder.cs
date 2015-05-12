using System.ServiceModel;
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
			int previewNumber,
			int previewSeconds,
			int verbosity, 
			int previewCount, 
			bool useDvdNav,
			bool dxvaDecoding,
			double minTitleDurationSeconds,
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
