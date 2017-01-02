namespace VidCoderCLI
{
	using System.ServiceModel;

	[ServiceContract]
	public interface IVidCoderAutomation
	{
		[OperationContract]
		[FaultContract(typeof(AutomationError))]
		void Encode(string source, string destination, string preset, string picker);

		[OperationContract]
		[FaultContract(typeof(AutomationError))]
		void Scan(string source);

		[OperationContract]
		[FaultContract(typeof (AutomationError))]
		void ImportPreset(string filePath);

		[OperationContract]
		[FaultContract(typeof(AutomationError))]
		void ImportQueue(string filePath);
	}
}