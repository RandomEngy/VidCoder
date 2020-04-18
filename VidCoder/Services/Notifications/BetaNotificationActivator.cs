using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace VidCoder.Services.Notifications
{
	[ClassInterface(ClassInterfaceType.None)]
	[ComSourceInterfaces(typeof(NotificationActivator.INotificationActivationCallback))]
	[Guid("03edc9b6-81c9-4763-bb56-8da8f88a1ef6")]
	[ComVisible(true)]
	public class BetaNotificationActivator : NotificationActivator
	{
	}
}
