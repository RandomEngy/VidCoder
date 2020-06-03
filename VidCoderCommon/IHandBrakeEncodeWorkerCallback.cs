using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VidCoderCommon.Model;

namespace VidCoderCommon
{
	public interface IHandBrakeEncodeWorkerCallback : IHandBrakeWorkerCallback
	{
		void OnEncodeStarted();

		void OnEncodeProgress(float averageFrameRate, float currentFrameRate, TimeSpan estimatedTimeLeft, float fractionComplete, int passId, int pass, int passCount, string stateCode);

		void OnEncodeComplete(VCEncodeResultCode result);
	}
}
