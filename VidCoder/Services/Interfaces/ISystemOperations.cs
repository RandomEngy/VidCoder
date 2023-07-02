namespace VidCoder.Services;

public interface ISystemOperations
{
	void Sleep();
	void LogOff();
	void ShutDown();
	void Restart();
	void Eject(string driveLetter);
	void Hibernate();
}