namespace VidCoder.Model
{
	public enum EncodeCompleteReason
	{
		// User hit stop button
		Manual,

		// User exited app
		AppExit,

		// The encode finished successfully
		Succeeded
	}
}
