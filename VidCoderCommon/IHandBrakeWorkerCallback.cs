using System;

namespace VidCoderCommon;

public interface IHandBrakeWorkerCallback
{
	void OnMessageLogged(string message);

	void OnVidCoderMessageLogged(string message);

	void OnErrorLogged(string message);

	void OnException(string exceptionString);
}
