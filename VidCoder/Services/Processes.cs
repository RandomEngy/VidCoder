using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace VidCoder.Services;

public class Processes : IProcesses
{
	public Process[] GetProcesses()
	{
		return Process.GetProcesses();
	}

	public Process[] GetProcessesByName(string processName)
	{
		return Process.GetProcessesByName(processName);
	}
}
