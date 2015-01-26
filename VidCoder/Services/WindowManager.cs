using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using GalaSoft.MvvmLight;
using VidCoder.Model.WindowPlacer;
using VidCoder.View;
using VidCoder.ViewModel;
using VidCoder.Services;
using System.Windows;
using VidCoder.ViewModel.DataModels;

namespace VidCoder
{
	public static class WindowManager
	{
		private static List<ViewModelBase> openWindows = new List<ViewModelBase>();
		private static List<ViewModelBase> openDialogs = new List<ViewModelBase>();

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

		public static List<ViewModelBase> OpenWindows
		{
			get
			{
				return openWindows;
			}
		}

		public static void OpenWindow(ViewModelBase viewModel)
		{
			OpenWindow(viewModel, null);
		}

		public static void OpenDialog(ViewModelBase viewModel)
		{
			OpenDialog(viewModel, null);
		}

		public static void OpenWindow(ViewModelBase viewModel, ViewModelBase owner)
		{
			openWindows.Add(viewModel);
			WindowLauncher.OpenWindow(viewModel, owner);
		}

		public static void OpenDialog(ViewModelBase viewModel, ViewModelBase owner)
		{
			openDialogs.Add(viewModel);
			WindowLauncher.OpenDialog(viewModel, owner);
		}

		public static void Close(ViewModelBase viewModel)
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

		public static void ReportClosed(ViewModelBase viewModel)
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

		public static void FocusWindow(ViewModelBase viewModel)
		{
			windowLauncher.FocusWindow(viewModel);
		}

		public static T FindWindow<T>() where T : ViewModelBase
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
			AddWindowPosition<PickerViewModel>(result, excludeWindow);
			return result;
		}

		private static void AddWindowPosition<T>(List<WindowPosition> workingList, Window excludeWindow) where T : ViewModelBase
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

		internal static Window GetView(ViewModelBase viewModel)
		{
			return windowLauncher.GetView(viewModel);
		}
	}
}
