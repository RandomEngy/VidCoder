using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using VidCoder.ViewModel;

namespace VidCoder.Services
{
	public class MessageBoxService : IMessageBoxService
	{
		public MessageBoxResult Show(string messageBoxText)
		{
			return MessageBox.Show(messageBoxText);
		}

		public MessageBoxResult Show(ViewModelBase owner, string messageBoxText)
		{
			Window ownerWindow = WindowManager.GetView(owner);
			return MessageBox.Show(ownerWindow, messageBoxText);
		}

		public MessageBoxResult Show(string messageBoxText, string caption)
		{
			return MessageBox.Show(messageBoxText, caption);
		}

		public MessageBoxResult Show(ViewModelBase owner, string messageBoxText, string caption)
		{
			Window ownerWindow = WindowManager.GetView(owner);
			return MessageBox.Show(ownerWindow, messageBoxText, caption);
		}

		public MessageBoxResult Show(string messageBoxText, string caption, MessageBoxButton button)
		{
			return MessageBox.Show(messageBoxText, caption, button);
		}

		public MessageBoxResult Show(ViewModelBase owner, string messageBoxText, string caption, MessageBoxButton button)
		{
			Window ownerWindow = WindowManager.GetView(owner);
			return MessageBox.Show(ownerWindow, messageBoxText, caption, button);
		}

		public MessageBoxResult Show(string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon)
		{
			return MessageBox.Show(messageBoxText, caption, button, icon);
		}

		public MessageBoxResult Show(ViewModelBase owner, string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon)
		{
			Window ownerWindow = WindowManager.GetView(owner);
			return MessageBox.Show(ownerWindow, messageBoxText, caption, button, icon);
		}
	}
}
