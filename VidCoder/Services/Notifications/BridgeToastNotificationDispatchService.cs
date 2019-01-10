using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Notifications;

namespace VidCoder.Services.Notifications
{
	public class BridgeToastNotificationDispatchService : IToastNotificationDispatchService
	{
		private readonly ToastNotificationHistory history;

		public BridgeToastNotificationDispatchService()
		{
			this.history = ToastNotificationManager.History;
		}

		public ToastNotifier CreateToastNotifier()
		{
			return ToastNotificationManager.CreateToastNotifier();
		}

		public void Clear()
		{
			this.history.Clear();
		}

		public IReadOnlyList<ToastNotification> GetHistory()
		{
			return this.history.GetHistory();
		}

		public void Remove(string tag)
		{
			this.history.Remove(tag);
		}

		public void Remove(string tag, string group)
		{
			this.history.Remove(tag, group);
		}

		public void RemoveGroup(string group)
		{
			this.history.RemoveGroup(group);
		}
	}
}
