using System.Windows;
using VidCoder.Services.Windows;

namespace VidCoder.Services
{
	public class MessageBoxService : IMessageBoxService
	{
		private IWindowManager windowManager;

		public MessageBoxService(IWindowManager windowManager)
		{
			this.windowManager = windowManager;
		}

		public MessageBoxResult Show(string messageBoxText)
		{
			return MessageBox.Show(messageBoxText);
		}

		public MessageBoxResult Show(object ownerViewModel, string messageBoxText)
		{
			Window ownerWindow = this.windowManager.GetView(ownerViewModel);
			return MessageBox.Show(ownerWindow, messageBoxText);
		}

		public MessageBoxResult Show(string messageBoxText, string caption)
		{
			return MessageBox.Show(messageBoxText, caption);
		}

		public MessageBoxResult Show(object ownerViewModel, string messageBoxText, string caption)
		{
			Window ownerWindow = this.windowManager.GetView(ownerViewModel);
			return MessageBox.Show(ownerWindow, messageBoxText, caption);
		}

		public MessageBoxResult Show(string messageBoxText, string caption, MessageBoxButton button)
		{
			return MessageBox.Show(messageBoxText, caption, button);
		}

		public MessageBoxResult Show(object ownerViewModel, string messageBoxText, string caption, MessageBoxButton button)
		{
			Window ownerWindow = this.windowManager.GetView(ownerViewModel);
			return MessageBox.Show(ownerWindow, messageBoxText, caption, button);
		}

		public MessageBoxResult Show(string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon)
		{
			return MessageBox.Show(messageBoxText, caption, button, icon);
		}

		public MessageBoxResult Show(object ownerViewModel, string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon)
		{
			Window ownerWindow = this.windowManager.GetView(ownerViewModel);
			return MessageBox.Show(ownerWindow, messageBoxText, caption, button, icon);
		}
	}
}
