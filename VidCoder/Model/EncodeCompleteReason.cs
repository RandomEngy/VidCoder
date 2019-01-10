namespace VidCoder.Model
{
	public enum EncodeCompleteReason
	{
		/// <summary>
		/// User hit stop button
		/// </summary>
		Manual,

		/// <summary>
		/// User exited app
		/// </summary>
		AppExit,

		/// <summary>
		/// The encode finished (successfully or unsuccessfully)
		/// </summary>
		Finished
	}
}
