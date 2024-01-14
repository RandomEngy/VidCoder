namespace VidCoder.Model;

public enum EncodeCompleteReason
{
	/// <summary>
	/// User hit the general stop button
	/// </summary>
	ManualStopAll,

	/// <summary>
	/// User stopped one of multiple encoding items
	/// </summary>
	ManualStopSingle,

	/// <summary>
	/// User exited app
	/// </summary>
	AppExitStop,

	/// <summary>
	/// The encode finished (successfully or unsuccessfully)
	/// </summary>
	Finished
}
