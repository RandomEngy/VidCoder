using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using VidCoder.View;

namespace VidCoder
{
	public class RestoreWindowCommand : ICommand
	{
		public void Execute(object parameter)
		{
			var mainWindow = Ioc.Container.GetInstance<MainWindow>();
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

		// We need this to implement ICommand but don't use it.
#pragma warning disable 67
		public event EventHandler CanExecuteChanged;
#pragma warning restore 67
	}
}
