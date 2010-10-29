using System;
namespace VidCoder.Services
{
	public interface IProcesses
	{
		System.Diagnostics.Process[] GetProcesses();
	}
}
