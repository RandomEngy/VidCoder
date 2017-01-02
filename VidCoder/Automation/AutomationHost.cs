using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VidCoderCommon;

namespace VidCoder.Automation
{
	using System.ServiceModel;
	using VidCoderCLI;

	public static class AutomationHost
	{
		private static ServiceHost host;

		public static void StartListening()
		{
			Task.Run(async () =>
			{
				host = new ServiceHost(typeof(VidCoderAutomation));

				host.AddServiceEndpoint(
					typeof(IVidCoderAutomation),
					new NetNamedPipeBinding(),
					"net.pipe://localhost/VidCoderAutomation" + (CommonUtilities.Beta ? "Beta" : string.Empty));

				await Task.Factory.FromAsync(host.BeginOpen, host.EndOpen, null);
			});
		}

		public static void StopListening()
		{
			host?.Close();
		}
	}
}
