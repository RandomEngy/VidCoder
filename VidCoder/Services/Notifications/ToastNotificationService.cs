using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;
using Microsoft.Toolkit.Uwp.Notifications;
using VidCoderCommon;

namespace VidCoder.Services.Notifications
{
	public class ToastNotificationService : IToastNotificationService
	{
		public const string ToastActivatedLaunchArg = "-ToastActivated";

		private const string BetaAumid = "VidCoder.VidCoderBeta";
		private const string StableAumid = "VidCoder.VidCoder";

		private IToastNotificationDispatchService dispatch;

		public ToastNotificationService()
		{
			Type notificationActivatorType = CommonUtilities.Beta ? typeof(BetaNotificationActivator) : typeof(StableNotificationActivator);

			if (Utilities.IsRunningAsAppx)
			{
				this.dispatch = new BridgeToastNotificationDispatchService();
			}
			else
			{
				RegisterComServer(notificationActivatorType);

				this.dispatch = new DesktopToastNotificationDispatchService(CommonUtilities.Beta ? BetaAumid : StableAumid);
			}

			RegisterActivator(notificationActivatorType);
		}

		private static void RegisterComServer(Type activatorType)
		{
			// We register the EXE to start up when the notification is activated
			string regString = string.Format(CultureInfo.InvariantCulture, @"SOFTWARE\Classes\CLSID\{{{0}}}\LocalServer32", activatorType.GUID);
			var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(regString);

			// Include a flag so we know this was a toast activation and should wait for COM to process
			// We also wrap EXE path in quotes for extra security
			key.SetValue(null, '"' + Process.GetCurrentProcess().MainModule.FileName + '"' + " " + ToastActivatedLaunchArg);
		}

		private static void RegisterActivator(Type activatorType)
		{
			// Register type
			var regService = new RegistrationServices();
			regService.RegisterTypeForComClients(
				activatorType,
				RegistrationClassContext.LocalServer,
				RegistrationConnectionType.MultipleUse);
		}

		/// <summary>
		/// Shows the given toast content.
		/// </summary>
		public void ShowToast(ToastContent toastContent)
		{
			// Create the XML document
			var doc = new XmlDocument();
			doc.LoadXml(toastContent.GetContent());

			// And create the toast notification
			var toast = new ToastNotification(doc);

			// And then show it
			this.dispatch.CreateToastNotifier().Show(toast);
		}

		/// <summary>
		/// Removes all notifications sent by this app from action center.
		/// </summary>
		public void Clear()
		{
			this.dispatch.Clear();
		}

		/// <summary>
		/// Gets all notifications sent by this app that are currently still in Action Center.
		/// </summary>
		/// <returns>A collection of toasts.</returns>
		public IReadOnlyList<ToastNotification> GetHistory()
		{
			return this.dispatch.GetHistory();
		}

		/// <summary>
		/// Removes an individual toast, with the specified tag label, from action center.
		/// </summary>
		/// <param name="tag">The tag label of the toast notification to be removed.</param>
		public void Remove(string tag)
		{
			this.dispatch.Remove(tag);
		}

		/// <summary>
		/// Removes a toast notification from the action using the notification's tag and group labels.
		/// </summary>
		/// <param name="tag">The tag label of the toast notification to be removed.</param>
		/// <param name="group">The group label of the toast notification to be removed.</param>
		public void Remove(string tag, string group)
		{
			this.dispatch.Remove(tag, group);
		}

		/// <summary>
		/// Removes a group of toast notifications, identified by the specified group label, from action center.
		/// </summary>
		/// <param name="group">The group label of the toast notifications to be removed.</param>
		public void RemoveGroup(string group)
		{
			this.dispatch.RemoveGroup(group);
		}
	}
}
