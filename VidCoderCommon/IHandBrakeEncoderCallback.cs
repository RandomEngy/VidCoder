using System;

namespace VidCoderCommon
{
	using System.ServiceModel;

	public interface IHandBrakeEncoderCallback
	{
		[OperationContract(IsOneWay = true)]
		void OnEncodeStarted();

		[OperationContract(IsOneWay = true)]
		void OnEncodeProgress(float averageFrameRate, float currentFrameRate, TimeSpan estimatedTimeLeft, float fractionComplete, int passId, int pass, int passCount);

		[OperationContract(IsOneWay = true)]
		void OnEncodeComplete(bool error);

		[OperationContract(IsOneWay = true)]
		void OnMessageLogged(string message);

		[OperationContract(IsOneWay = true)]
		void OnVidCoderMessageLogged(string message);

		[OperationContract(IsOneWay = true)]
		void OnErrorLogged(string message);

		[OperationContract(IsOneWay = true)]
		void OnException(string exceptionString);
	}
}
