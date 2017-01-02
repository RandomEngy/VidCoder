using System;

namespace VidCoder.Services
{
	public class StatusService
	{
		public event EventHandler<EventArgs<string>> MessageShown;

		public void Show(string message)
		{
			this.MessageShown?.Invoke(this, new EventArgs<string>(message));
		}
	}
}
