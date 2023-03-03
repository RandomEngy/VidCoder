using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VidCoderCommon.Model;

namespace VidCoderCommon.Services;

public interface ILogger : IBasicLogger
{
	void LogDebug(string message);

	void LogError(string message);
}
