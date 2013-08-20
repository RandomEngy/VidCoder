using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VidCoder.Automation
{
	using System.ServiceModel;
	using VidCoderCLI;

	public static class AutomationHost
	{
		private static ServiceHost host;

		public static void StartListening()
		{
			host = new ServiceHost(typeof (VidCoderAutomation));

			host.AddServiceEndpoint(
				typeof (IVidCoderAutomation),
				new NetNamedPipeBinding(),
				"net.pipe://localhost/VidCoderAutomation" + (Utilities.Beta ? "Beta" : string.Empty));

			host.Open();
		}

		public static void StopListening()
		{
			if (host != null)
			{
				host.Close();
			}
		}
	}
}
