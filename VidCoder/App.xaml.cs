using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Windows;
using VidCoder.ViewModel;
using System.IO.Pipes;
using System.IO;
using System.ComponentModel;
using HandBrake.Interop;
using VidCoder.View;
using Microsoft.Practices.Unity;
using Microsoft.Practices.Unity.Configuration;

namespace VidCoder
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application
	{
		protected override void OnStartup(StartupEventArgs e)
		{
			Unity.Container = new UnityContainer().LoadConfiguration();
#if !DEBUG
			this.DispatcherUnhandledException += this.OnDispatcherUnhandledException;
#endif
			base.OnStartup(e);

			var mainVM = new MainViewModel();
			WindowManager.OpenWindow(mainVM);
			mainVM.OnLoaded();
		}

		private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
		{
			var exceptionDialog = new ExceptionDialog(e.Exception);
			exceptionDialog.ShowDialog();
		}
	}
}
