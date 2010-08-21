using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using VidCoder.ViewModel;

namespace VidCoder.Services
{
	public interface IMessageBoxService
	{
		MessageBoxResult Show(string messageBoxText);
		MessageBoxResult Show(ViewModelBase owner, string messageBoxText);
		MessageBoxResult Show(string messageBoxText, string caption);
		MessageBoxResult Show(ViewModelBase owner, string messageBoxText, string caption);
		MessageBoxResult Show(string messageBoxText, string caption, MessageBoxButton button);
		MessageBoxResult Show(ViewModelBase owner, string messageBoxText, string caption, MessageBoxButton button);
		MessageBoxResult Show(string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon);
		MessageBoxResult Show(ViewModelBase owner, string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon);
	}
}
