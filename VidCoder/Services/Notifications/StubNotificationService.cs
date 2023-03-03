using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VidCoder.Services.Notifications;

public class StubNotificationService : IToastNotificationService
{
	public bool ToastEnabled => false;

	public void Clear()
	{
	}

	public void Remove(string tag)
	{
	}

	public void Remove(string tag, string group)
	{
	}

	public void RemoveGroup(string group)
	{
	}

	public void ShowToast(string toastContent)
	{
	}
}
