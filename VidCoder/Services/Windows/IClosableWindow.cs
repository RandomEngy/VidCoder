namespace VidCoder.Services.Windows;

public interface IClosableWindow
{
	/// <summary>
	/// Fires when the window is closing.
	/// </summary>
	/// <returns>True if the window close can continue, false if the window close should be canceled.</returns>
	bool OnClosing();
}
