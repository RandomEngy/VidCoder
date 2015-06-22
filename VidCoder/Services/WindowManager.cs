using System.Collections.Generic;
using System.Linq;
using System.Windows;
using VidCoder.Model.WindowPlacer;
using VidCoder.Services;
using VidCoder.ViewModel;

namespace VidCoder
{
	public static class WindowManager
	{
		private static List<object> openWindows = new List<object>();
		private static List<object> openDialogs = new List<object>();

		private static IWindowLauncher windowLauncher = new WindowLauncher();

		internal static IWindowLauncher WindowLauncher
		{
			get
			{
				return windowLauncher;
			}

			set
			{
				windowLauncher = value;
			}
		}

		public static List<object> OpenWindows
		{
			get
			{
				return openWindows;
			}
		}

		public static void OpenWindow(object viewModel)
		{
			OpenWindow(viewModel, null);
		}

		public static void OpenDialog(object viewModel)
		{
			OpenDialog(viewModel, null);
		}

		public static void OpenWindow(object viewModel, object owner)
		{
			openWindows.Add(viewModel);
			WindowLauncher.OpenWindow(viewModel, owner);
		}

		public static void OpenDialog(object viewModel, object owner)
		{
			openDialogs.Add(viewModel);
			WindowLauncher.OpenDialog(viewModel, owner);
		}

		public static void Close(object viewModel)
		{
			if (openWindows.Contains(viewModel))
			{
				openWindows.Remove(viewModel);
			}
			else if (openDialogs.Contains(viewModel))
			{
				openDialogs.Remove(viewModel);
			}

			WindowLauncher.CloseWindow(viewModel);
		}

		public static void ReportClosed(object viewModel)
		{
			if (openWindows.Contains(viewModel))
			{
				openWindows.Remove(viewModel);
			}
			else if (openDialogs.Contains(viewModel))
			{
				openDialogs.Remove(viewModel);
			}
		}

		public static void ActivateWindow(object viewModel)
		{
			windowLauncher.ActivateWindow(viewModel);
		}

		public static void FocusWindow(object viewModel)
		{
			windowLauncher.FocusWindow(viewModel);
		}

		public static T FindWindow<T>() where T : class
		{
			return openWindows.SingleOrDefault(vm => vm is T) as T;
		}

		public static List<WindowPosition> GetOpenedWindowPositions(Window excludeWindow = null)
		{
			var result = new List<WindowPosition>();
			AddWindowPosition<MainViewModel>(result, excludeWindow);
			AddWindowPosition<EncodingViewModel>(result, excludeWindow);
			AddWindowPosition<PreviewViewModel>(result, excludeWindow);
			AddWindowPosition<LogViewModel>(result, excludeWindow);
			AddWindowPosition<PickerWindowViewModel>(result, excludeWindow);
			return result;
		}

		private static void AddWindowPosition<T>(List<WindowPosition> workingList, Window excludeWindow) where T : class
		{
			T windowVM = FindWindow<T>();
			if (windowVM == null)
			{
				return;
			}

			Window window = windowLauncher.GetView(windowVM);
			if (window != null && window != excludeWindow)
			{
				workingList.Add(new WindowPosition
				{
					Position = new Rect(
						(int)window.Left,
						(int)window.Top,
						(int)window.ActualWidth,
						(int)window.ActualHeight),
					ViewModelType = typeof(T)
				});
			}
		}

		internal static Window GetView(object viewModel)
		{
			return windowLauncher.GetView(viewModel);
		}
	}
}
