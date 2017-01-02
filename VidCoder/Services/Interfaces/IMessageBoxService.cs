using System.Windows;

namespace VidCoder.Services
{
	public interface IMessageBoxService
	{
		MessageBoxResult Show(string messageBoxText);
		MessageBoxResult Show(object ownerViewModel, string messageBoxText);
		MessageBoxResult Show(string messageBoxText, string caption);
		MessageBoxResult Show(object ownerViewModel, string messageBoxText, string caption);
		MessageBoxResult Show(string messageBoxText, string caption, MessageBoxButton button);
		MessageBoxResult Show(object ownerViewModel, string messageBoxText, string caption, MessageBoxButton button);
		MessageBoxResult Show(string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon);
		MessageBoxResult Show(object ownerViewModel, string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon);
	}
}
