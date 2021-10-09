using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VidCoder.Services
{
	/// <summary>
	/// Tracks if the user is active on the application
	/// </summary>
	public class ActivityService
	{
		private bool active = true;

		private TaskCompletionSource<object> activeTcs;

		/// <summary>
		/// Waits for the program to become active.
		/// </summary>
		public Task WaitForActiveAsync()
		{
			bool hasInputRecently = SystemInputTracker.GetTimeSinceLastInput() < TimeSpan.FromSeconds(30);
			if (active && hasInputRecently)
			{
				return Task.CompletedTask;
			}

			if (!active)
			{
				// If the app is not active, we just need to wait until we're reactivated.
				if (activeTcs == null)
				{
					activeTcs = new TaskCompletionSource<object>();
				}

				return activeTcs.Task;
			}

			// The app is active, but we have not seen user input in a while. We need to poll for it.
			return this.WaitForInput();
		}

		public void ReportActivated()
		{
			active = true;
			this.activeTcs?.TrySetResult(null);
		}

		public void ReportDeactivated()
		{
			active = false;
		}

		private async Task WaitForInput()
		{
			while (true)
			{
				await Task.Delay(2000);
				if (SystemInputTracker.GetTimeSinceLastInput() < TimeSpan.FromSeconds(30))
				{
					return;
				}
			}
		}
	}
}
