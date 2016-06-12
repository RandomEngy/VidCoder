using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VidCoderCommon.Services
{
	public interface ILogger
	{
		void Log(string message);

		void LogError(string message);
	}
}
