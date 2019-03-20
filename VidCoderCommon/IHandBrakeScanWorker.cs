using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VidCoderCommon
{
	public interface IHandBrakeScanWorker : IHandBrakeWorker
	{
		void StartScan(
			string path);
	}
}
