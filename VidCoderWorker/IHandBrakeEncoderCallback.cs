using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VidCoderWorker
{
	using System.ServiceModel;

	public interface IHandBrakeEncoderCallback
	{
		[OperationContract(IsOneWay = true)]
		void OnEncodeStarted();

		[OperationContract(IsOneWay = true)]
		void OnEncodeProgress(float averageFrameRate, float currentFrameRate, TimeSpan estimatedTimeLeft, float fractionComplete, int pass);

		[OperationContract(IsOneWay = true)]
		void OnEncodeComplete(bool error);

		[OperationContract(IsOneWay = true)]
		void OnMessageLogged(string message);

		[OperationContract(IsOneWay = true)]
		void OnErrorLogged(string message);

		[OperationContract(IsOneWay = true)]
		void OnException(string exceptionString);
	}
}
