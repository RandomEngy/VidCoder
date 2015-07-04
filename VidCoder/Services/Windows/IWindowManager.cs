using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using VidCoder.Model.WindowPlacer;

namespace VidCoder.Services.Windows
{
	public interface IWindowManager
	{
		/// <summary>
		/// Opens the viewmodel as a window.
		/// </summary>
		/// <param name="viewModel">The window's viewmodel.</param>
		/// <param name="ownerViewModel">The viewmodel of the owner window.</param>
		void OpenWindow(object viewModel, object ownerViewModel = null);

		/// <summary>
		/// Opens the viewmodel as a dialog.
		/// </summary>
		/// <param name="viewModel">The dialog's viewmodel.</param>
		/// <param name="ownerViewModel">The viewmodel of the owner window.</param>
		void OpenDialog(object viewModel, object ownerViewModel = null);

		/// <summary>
		/// Opens all tracked windows that are open according to config.
		/// </summary>
		/// <remarks>Call at app startup.</remarks>
		void OpenTrackedWindows();

		/// <summary>
		/// Closes all tracked windows.
		/// </summary>
		/// <remarks>Call on app exit.</remarks>
		void CloseTrackedWindows();

		/// <summary>
		/// Opens or focuses the viewmodel type's window.
		/// </summary>
		/// <param name="viewModelType">The type of the window viewmodel.</param>
		/// <param name="ownerViewModel">The owner view model (main view model).</param>
		void OpenOrFocusWindow(Type viewModelType, object ownerViewModel = null);

		/// <summary>
		/// Focuses the window.
		/// </summary>
		/// <param name="viewModel">The viewmodel of the window to focus.</param>
		void Focus(object viewModel);

		/// <summary>
		/// Activates the window.
		/// </summary>
		/// <param name="viewModel">The viewmodel of the window to activate.</param>
		void Activate(object viewModel);

		/// <summary>
		/// Closes the window.
		/// </summary>
		/// <param name="viewModel">The viewmodel of the window to close.</param>
		void Close(object viewModel);

		/// <summary>
		/// Creates a command to open a window.
		/// </summary>
		/// <param name="viewModelType">The type of window viewmodel to open.</param>
		/// <returns>The command.</returns>
		ICommand CreateOpenCommand(Type viewModelType);

		/// <summary>
		/// Closes the window of the given type.
		/// </summary>
		/// <typeparam name="T">The viewmodel type of the window to close.</typeparam>
		/// <param name="userInitiated">True if the user specifically asked this window to close.</param>
		void Close<T>(bool userInitiated) where T : class;

		/// <summary>
		/// Gets the view for the given viewmodel.
		/// </summary>
		/// <param name="viewModel">The viewmodel.</param>
		/// <returns>The view for the given viewmodel.</returns>
		Window GetView(object viewModel);

		/// <summary>
		/// Gets a list of window positions.
		/// </summary>
		/// <param name="excludeWindow">The window to exclude.</param>
		/// <returns>A list of open window positions.</returns>
		List<WindowPosition> GetOpenedWindowPositions(Window excludeWindow = null);

		/// <summary>
		/// Finds an open window with the given viewmodel type.
		/// </summary>
		/// <typeparam name="T">The viewmodel type.</typeparam>
		/// <returns>The open window viewmodel.</returns>
		T Find<T>() where T : class;

		/// <summary>
		/// Fires when a window opens.
		/// </summary>
		event EventHandler<EventArgs<Type>> WindowOpened;

		/// <summary>
		/// Fires when a window closes.
		/// </summary>
		event EventHandler<EventArgs<Type>> WindowClosed;
	}
}