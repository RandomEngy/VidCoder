using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Velopack;

namespace VidCoder;

public static class Program
{
	[STAThread]
	public static void Main(string[] args)
	{
		// Should be a no-op when not called with Velopack command line arguments.
		VelopackApp.Build()
			.WithFirstRun(VidCoderInstall.OnInitialInstall)
			.WithAfterUpdateFastCallback(VidCoderInstall.OnAppUpdate)
			.WithBeforeUninstallFastCallback(VidCoderInstall.OnAppUninstall)
			.Run();

		var application = new App();
		application.InitializeComponent();
		application.Run();
	}
}
