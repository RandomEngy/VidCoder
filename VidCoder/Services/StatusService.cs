using System;

namespace VidCoder.Services;

public class StatusService
{
	private readonly ActivityService activityService;

	public event EventHandler<EventArgs<string>> MessageShown;

	public StatusService(ActivityService activityService)
	{
		this.activityService = activityService;
	}

	public async void Show(string message)
	{
		await activityService.WaitForActiveAsync();

		this.MessageShown?.Invoke(this, new EventArgs<string>(message));
	}
}
