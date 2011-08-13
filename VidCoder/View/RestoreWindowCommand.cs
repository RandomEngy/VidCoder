using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using VidCoder.View;
using Microsoft.Practices.Unity;

namespace VidCoder
{
	public class RestoreWindowCommand : ICommand
	{
		public void Execute(object parameter)
		{
			var mainWindow = Unity.Container.Resolve<MainWindow>();
			mainWindow.Show();

			mainWindow.Dispatcher.BeginInvoke(new Action(() =>
			    {
					mainWindow.WindowState = mainWindow.RestoredWindowState;
			    }));
		}

		public bool CanExecute(object parameter)
		{
			return true;
		}

		public event EventHandler CanExecuteChanged;
	}
}
