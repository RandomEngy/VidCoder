namespace VidCoderCLI
{
	using System.ServiceModel;

	[ServiceContract]
	public interface IVidCoderAutomation
	{
		[OperationContract]
		[FaultContract(typeof(AutomationError))]
		void Encode(string source, string destination, string preset);

		[OperationContract]
		[FaultContract(typeof(AutomationError))]
		void Scan(string source);
	}
}