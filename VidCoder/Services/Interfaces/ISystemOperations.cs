namespace VidCoder.Services
{
	public interface ISystemOperations
	{
		void Sleep();
		void LogOff();
		void ShutDown();
		void Eject(string driveLetter);
	}
}