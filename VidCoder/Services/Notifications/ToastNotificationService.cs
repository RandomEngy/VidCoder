using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Xml.Dom;
using VidCoderCommon;
using Microsoft.Toolkit.Uwp.Notifications;
using Windows.UI.Notifications;
using Windows.Foundation.Metadata;
using Windows.ApplicationModel.Calls;

namespace VidCoder.Services.Notifications
{
	public class ToastNotificationService : IToastNotificationService
	{
		private readonly IAppLogger logger;
		private readonly ToastNotificationHistoryCompat history;

		public ToastNotificationService(IAppLogger logger)
		{
			this.logger = logger;
			this.history = ToastNotificationManagerCompat.History;
		}

		public bool ToastEnabled => true;

		/// <summary>
		/// Shows the given toast content.
		/// </summary>
		public void ShowToast(string toastContent)
		{
			try
			{
				// Create the XML document
				var doc = new XmlDocument();
				doc.LoadXml(toastContent);

				// And create the toast notification
				var toast = new ToastNotification(doc);

				// And then show it
				ToastNotificationManagerCompat.CreateToastNotifier().Show(toast);
			}
			catch (Exception exception)
			{
				this.logger.LogError("Could not show notification:" + Environment.NewLine + exception);
			}
		}

		/// <summary>
		/// Removes all notifications sent by this app from action center.
		/// </summary>
		public void Clear()
		{
			try
			{
				this.history.Clear();
			}
			catch (Exception exception)
			{
				this.logger.LogError("Could not clear notifications:" + Environment.NewLine + exception);
			}
		}

		/// <summary>
		/// Gets all notifications sent by this app that are currently still in Action Center.
		/// </summary>
		/// <returns>A collection of toasts.</returns>
		public IReadOnlyList<ToastNotification> GetHistory()
		{
			return this.history.GetHistory();
		}

		/// <summary>
		/// Removes an individual toast, with the specified tag label, from action center.
		/// </summary>
		/// <param name="tag">The tag label of the toast notification to be removed.</param>
		public void Remove(string tag)
		{
			try
			{
				this.history.Remove(tag);
			}
			catch (Exception exception)
			{
				this.logger.LogError("Could not remove notification:" + Environment.NewLine + exception);
			}
		}

		/// <summary>
		/// Removes a toast notification from the action using the notification's tag and group labels.
		/// </summary>
		/// <param name="tag">The tag label of the toast notification to be removed.</param>
		/// <param name="group">The group label of the toast notification to be removed.</param>
		public void Remove(string tag, string group)
		{
			try
			{
				this.history.Remove(tag, group);
			}
			catch (Exception exception)
			{
				this.logger.LogError("Could not remove notification:" + Environment.NewLine + exception);
			}
		}

		/// <summary>
		/// Removes a group of toast notifications, identified by the specified group label, from action center.
		/// </summary>
		/// <param name="group">The group label of the toast notifications to be removed.</param>
		public void RemoveGroup(string group)
		{
			try
			{
				this.history.RemoveGroup(group);
			}
			catch (Exception exception)
			{
				this.logger.LogError("Could not remove notification group:" + Environment.NewLine + exception);
			}
		}
	}
}
