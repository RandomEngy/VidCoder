using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VidCoderCommon;

public interface IHandBrakeScanWorkerCallback : IHandBrakeWorkerCallback
{
	void OnScanComplete(string scanJson);

	void OnScanProgress(float fractionComplete);
}
