using System;
namespace VidCoder.Services;

public interface IProcesses
{
	System.Diagnostics.Process[] GetProcesses();

	System.Diagnostics.Process[] GetProcessesByName(string processName);
}
