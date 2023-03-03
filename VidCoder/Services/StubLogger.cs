using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VidCoderCommon.Services;

namespace VidCoder.Services;

public class StubLogger : ILogger
{
	public void Log(string message)
	{
	}

	public void LogDebug(string message)
	{
	}

	public void LogError(string message)
	{
	}
}
