using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HandBrake.Interop.Interop;
using HandBrake.Interop.Utilities;
using Microsoft.AnyContainer;
using Squirrel;
using VidCoder.Extensions;
using VidCoder.Resources;
using VidCoder.Services;

namespace VidCoder.ViewModel
{
	public class AboutDialogViewModel : OkCancelDialogViewModel
	{
		public AboutDialogViewModel()
		{
			Task.Run(async () =>
			{
				try
				{
					using var updateManager = new UpdateManager(Utilities.SquirrelUpdateUrl);
					await updateManager.UpdateApp();
				}
				catch (Exception exception)
				{
					StaticResolver.Resolve<IAppLogger>().LogError(exception.ToString());
				}
			});
		}

		public string Version => Utilities.VersionString;

		public string BasedOnHandBrake
		{
			get
			{
				// We don't need to initialize or dispose the HandBrakeInstance because the Version actually doesn't use the hb_handle_t that's passed in so it can stay as IntPtr.Zero.
				// Need to find out why it's being done this way
				var tempInstance = new HandBrakeInstance();
				string version = tempInstance.Version;

				return string.Format(MiscRes.BasedOnHandBrake, version);
			}
		}

		public string Copyright
		{
			get
			{
				return string.Format(MiscRes.Copyright, "2010-2021");
			}
		}
	}
}
