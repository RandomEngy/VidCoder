namespace VidCoder.Messages
{
	public class PauseChangedMessage
	{
		public PauseChangedMessage(bool isPaused)
		{
			this.IsPaused = isPaused;
		}

		public bool IsPaused { get; private set; }
	}
}
