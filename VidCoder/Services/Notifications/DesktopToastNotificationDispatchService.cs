using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Notifications;

namespace VidCoder.Services.Notifications
{
	public class DesktopToastNotificationDispatchService : IToastNotificationDispatchService
	{
		private readonly string aumid;
		private readonly ToastNotificationHistory history;

		public DesktopToastNotificationDispatchService(string aumid)
		{
			this.aumid = aumid;
			this.history = ToastNotificationManager.History;
		}

		public ToastNotifier CreateToastNotifier()
		{
			return ToastNotificationManager.CreateToastNotifier(this.aumid);
		}

		public void Clear()
		{
			this.history.Clear(this.aumid);
		}

		public IReadOnlyList<ToastNotification> GetHistory()
		{
			return this.history.GetHistory(this.aumid);
		}

		public void Remove(string tag)
		{
			this.history.Remove(tag, string.Empty, this.aumid);
		}

		public void Remove(string tag, string group)
		{
			this.history.Remove(tag, group, this.aumid);
		}

		public void RemoveGroup(string group)
		{
			this.history.RemoveGroup(group, this.aumid);
		}
	}
}
