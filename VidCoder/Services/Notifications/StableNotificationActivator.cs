using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using VidCoder.Services.Notifications;
using VidCoder.View;

namespace VidCoder
{
	[ClassInterface(ClassInterfaceType.None)]
	[ComSourceInterfaces(typeof(NotificationActivator.INotificationActivationCallback))]
	[Guid("c689b172-b676-48b1-88ca-d0dd09096054")]
	[ComVisible(true)]
	public class StableNotificationActivator : NotificationActivator
	{
	}
}
